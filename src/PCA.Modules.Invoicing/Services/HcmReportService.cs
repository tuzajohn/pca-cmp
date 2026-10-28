using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Delegates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PCA.Modules.Invoicing.Models;
using System.Drawing;
using System.Globalization;

namespace PCA.Modules.Invoicing.Services;

// ── In-memory row from HCM Sheet1 ────────────────────────────────────────────

public record HcmSheetRow(
    string  EmployeeNo,    // EMPLOYEE_NO (= IPPS number)
    string  EmployeeName,
    string  VoteCode,
    string  VoteName,
    string  CostItem,
    decimal CostItemAmt,
    string  VendorName,
    decimal VendorAmount);

// ── HCM file read result ──────────────────────────────────────────────────────

public record HcmParseResult(
    List<HcmSheetRow> Rows,   // only rows whose EMPLOYEE_NO is on the Stanbic list
    int TotalRows,            // every data row in the file
    int UnparsedAmounts,      // non-blank amounts on kept rows that weren't numbers (counted as 0)
    int RaggedRows);          // CSV rows whose column count differs from the header

// ── Result returned when unknowns need classification ─────────────────────────

public record HcmUnknownCheckResult(
    List<(string RawValue, string SourceColumn)> Unknowns,
    string PendingRunId);   // folder key — caller saves files here

// ── Final run result ──────────────────────────────────────────────────────────

public record HcmRunResult(
    string HcmFilePath,
    string HcmFileName,
    string IppsFilePath,
    string IppsFileName,
    // log counters
    int TotalStanbicSubmitted,
    int MatchedToHcm,
    int PassedToIpps,
    int UnknownsFlagged,
    int WithStat,
    int WithAllow,
    int WithDed,
    int WithStanbic,
    int ZeroAfford,
    int? WithOtherRes = null);

// ── Service ───────────────────────────────────────────────────────────────────

public class HcmReportService
{
    private readonly ExternalDbSettings _hcmDb;
    private readonly HcmMappingService  _mappings;
    private readonly CrbReportService   _ippsModule;
    private readonly ILogger<HcmReportService> _logger;

    public HcmReportService(
        ExternalDbSettings hcmDbSettings,
        HcmMappingService mappings,
        CrbReportService ippsModule,
        ILogger<HcmReportService> logger)
    {
        _hcmDb      = hcmDbSettings;
        _mappings   = mappings;
        _ippsModule = ippsModule;
        _logger     = logger;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full combined HCM + IPPS report.
    /// Caller is responsible for saving the uploaded files to the pending folder
    /// BEFORE calling this if it is a resume after classification.
    /// </summary>
    public async Task<HcmRunResult> RunAsync(
        IFormFile hcmFile,
        IFormFile stanbicFile,
        string storageRoot,
        Action<string>? progress = null,
        bool includeOtherReservations = false,
        CancellationToken ct = default)
    {
        void Step(string msg) { progress?.Invoke(msg); _logger.LogInformation("HCM: {Msg}", msg); }

        // Stage 1 — parse + cross-reference
        Step("Stage 1 — Parsing Stanbic IPPS file…");
        var stanbicIpps = CrbReportService.ParseIppsFile(stanbicFile);
        var stanbicSet  = new HashSet<string>(
            stanbicIpps.Select(NormalizeIpps),
            StringComparer.OrdinalIgnoreCase);

        Step($"Stage 1 — Reading HCM file and cross-referencing against {stanbicIpps.Count} Stanbic IPPS numbers…");
        var hcm     = ParseHcmFile(hcmFile, stanbicSet);
        var matched = hcm.Rows;
        Step($"Stage 1 — Read {hcm.TotalRows} HCM rows, {matched.Count} belong to Stanbic employees");
        if (hcm.RaggedRows > 0)
            Step($"Stage 1 — Warning: {hcm.RaggedRows} HCM rows had a different number of columns from the header — check the file for stray quotes or delimiters");
        if (hcm.UnparsedAmounts > 0)
            Step($"Stage 1 — Warning: {hcm.UnparsedAmounts} COSTITEM_AMT/VENDOR_AMOUNT values on Stanbic rows were not numbers and were counted as 0");

        var matchedIpps   = new HashSet<string>(matched.Select(r => NormalizeIpps(r.EmployeeNo)), StringComparer.OrdinalIgnoreCase);
        var unmatched     = stanbicIpps
            .Where(n => !matchedIpps.Contains(NormalizeIpps(n)))
            .ToList();

        Step($"Stage 1 — Cross-reference complete: {matched.GroupBy(r => r.EmployeeNo).Count()} matched, {unmatched.Count} unmatched");

        // Stage 2 — mapping check (caller must have already resolved unknowns)
        Step("Stage 2 — Verifying all mappings are classified…");
        await _mappings.EnsureLoadedAsync();
        var allCostItems   = matched.Select(r => r.CostItem).Where(v => !string.IsNullOrWhiteSpace(v));
        var allVendorNames = matched.Select(r => r.VendorName).Where(v => !string.IsNullOrWhiteSpace(v));
        var unknowns       = await _mappings.FindUnknownsAsync(allCostItems, allVendorNames);
        if (unknowns.Count > 0)
            throw new InvalidOperationException(
                $"Unclassified values remain: {string.Join(", ", unknowns.Select(u => $"{u.SourceColumn}={u.RawValue}"))}");

        Step("Stage 2 — All mappings verified");

        // Stage 3 — salary, stat, allow from HCM file data
        Step("Stage 3 — Computing salary, statutory deductions and allowances from HCM data…");
        var empRows = matched
            .GroupBy(r => r.EmployeeNo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var classEntries = BuildClassEntries(empRows);
        var salaryMap    = ComputeSalary(classEntries);
        var statMap      = ComputeStat(classEntries);
        var allowMap     = ComputeAllow(classEntries);

        Step($"Stage 3 — Computed salary/stat/allow for {salaryMap.Count} employees");

        // Stage 4 — query HCM DB
        Step("Stage 4 — Opening HCM database connection…");
        var empNumbers = empRows.Keys.ToList();
        using var tunnel = await SshTunnelService.OpenAsync(_hcmDb, _logger);
        var conn = tunnel.Connection;

        Step("Stage 5 — Querying employee fields and deductions…");
        var empInfoMap    = await QueryEmployeeFieldsAsync(conn, empNumbers, ct);
        var dedMap        = await QueryDedAsync(conn, empNumbers, ct);
        var stanbicDedMap = await QueryStanbicDedAsync(conn, empNumbers, ct);
        var otherResMap   = includeOtherReservations
            ? await QueryOtherReservationsAsync(conn, empNumbers, ct)
            : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        Step($"Stage 5 — Employee info loaded for {empInfoMap.Count}, deductions for {dedMap.Count} employees");

        // Stage 6 — affordability + assemble output
        var outputRows    = new List<CrbOutputRow>();
        int withStat = 0, withAllow = 0, withDed = 0, withStanbic = 0, zeroAfford = 0, withOtherRes = 0;

        // Use matched list to preserve order; take first row per employee for bio data
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in matched)
        {
            var empNo = r.EmployeeNo;
            if (!seen.Add(empNo)) continue;

            var normalizedKey = NormalizeIpps(empNo);
            var empInfo  = empInfoMap.TryGetValue(normalizedKey, out var ei) ? ei : null;
            var salary   = salaryMap.TryGetValue(empNo, out var sv) ? sv : 0m;
            var stat     = statMap.TryGetValue(empNo, out var stv)  ? stv : 0m;
            var allow    = allowMap.TryGetValue(empNo, out var av)   ? av  : 0m;
            var ded      = dedMap.TryGetValue(normalizedKey, out var dv)     ? dv  : 0m;
            var stanbic  = stanbicDedMap.TryGetValue(normalizedKey, out var sbv) ? sbv : 0m;
            var otherRes = otherResMap.TryGetValue(normalizedKey, out var orv) ? orv : 0m;
            var afford   = Math.Max(0m, salary * 0.48m - (stat + ded));

            if (stat    > 0) withStat++;
            if (allow   > 0) withAllow++;
            if (ded     > 0) withDed++;
            if (stanbic > 0) withStanbic++;
            if (afford == 0) zeroAfford++;
            if (includeOtherReservations && otherRes > 0) withOtherRes++;

            outputRows.Add(new CrbOutputRow(
                Ipps:         normalizedKey,
                EmployeeId:   empInfo?.Id ?? 0L,
                EmpName:      r.EmployeeName,
                Vote:         r.VoteCode,
                VoteName:     r.VoteName,
                Salary:       salary,
                Terms:        empInfo?.Terms,
                IsActive:     empInfo?.IsActive ?? "Y",
                Stat:         stat,
                Allow:        allow,
                Ded:          ded,
                Stanbic:      stanbic,
                Affordability: afford,
                Notes:        string.Empty,
                OtherReservations: includeOtherReservations ? otherRes : null));
        }

        Step($"Stage 6 — Affordability computed for {outputRows.Count} employees");

        // Stage 7 — write HCM Excel
        Step("Stage 7 — Writing HCM output file…");
        string hcmFilePath;
        try
        {
            hcmFilePath = WriteExcel(outputRows, storageRoot, isHcm: true, includeOtherReservations);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"All database queries completed successfully ({outputRows.Count} employees processed) " +
                $"but the HCM Excel file could not be written: {ex.Message}. " +
                "Re-running will skip the long DB phase — please retry.", ex);
        }
        Step("Stage 7 — HCM file written");

        // Stage 8 — Run Module 2 for unmatched IPPS
        Step($"Stage 8 — Running Module 2 for {unmatched.Count} unmatched IPPS numbers…");
        var ippsResult = await _ippsModule.GenerateAsync(unmatched, storageRoot, progress, includeOtherReservations, ct);

        _logger.LogInformation(
            "HCM CRB run log:\n  Total Stanbic IPPS submitted:     {Total}\n  Matched to HCM Sheet1:            {Matched}\n  Passed to IPPS module:            {Unmatched}\n  Unknown COSTITEM/VENDOR flagged:  0 (already resolved)\n  Employees with stat > 0:          {Stat}\n  Employees with allow > 0:         {Allow}\n  Employees with ded > 0:           {Ded}\n  Employees with stanbic > 0:       {Stanbic}\n  Employees with affordability = 0: {ZeroAff}\n  Employees with other_res > 0:     {OtherRes}",
            stanbicIpps.Count, matched.GroupBy(r => r.EmployeeNo).Count(),
            unmatched.Count, withStat, withAllow, withDed, withStanbic, zeroAfford, withOtherRes);

        return new HcmRunResult(
            HcmFilePath:          hcmFilePath,
            HcmFileName:          Path.GetFileName(hcmFilePath),
            IppsFilePath:         ippsResult.FilePath,
            IppsFileName:         ippsResult.FileName,
            TotalStanbicSubmitted: stanbicIpps.Count,
            MatchedToHcm:         seen.Count,
            PassedToIpps:         unmatched.Count,
            UnknownsFlagged:      0,
            WithStat:             withStat,
            WithAllow:            withAllow,
            WithDed:              withDed,
            WithStanbic:          withStanbic,
            ZeroAfford:           zeroAfford,
            WithOtherRes:         includeOtherReservations ? withOtherRes : null);
    }

    // ── Check unknowns before processing (called from controller pre-flight) ──

    public async Task<List<(string RawValue, string SourceColumn)>> CheckUnknownsAsync(
        IFormFile hcmFile, IFormFile stanbicFile)
    {
        var stanbicIpps = CrbReportService.ParseIppsFile(stanbicFile);
        var stanbicSet  = new HashSet<string>(
            stanbicIpps.Select(NormalizeIpps),
            StringComparer.OrdinalIgnoreCase);
        var matched = ParseHcmFile(hcmFile, stanbicSet).Rows;

        await _mappings.EnsureLoadedAsync();
        var costItems   = matched.Select(r => r.CostItem).Where(v => !string.IsNullOrWhiteSpace(v));
        var vendorNames = matched.Select(r => r.VendorName).Where(v => !string.IsNullOrWhiteSpace(v));
        return await _mappings.FindUnknownsAsync(costItems, vendorNames);
    }

    // ── Unified classification entry ──────────────────────────────────────────
    // Each HcmSheetRow yields up to 2 entries — one from COSTITEM, one from VENDOR_NAME.

    private record ClassEntry(string EmpNo, string Canonical, decimal Amount, string Classification);

    private List<ClassEntry> BuildClassEntries(Dictionary<string, List<HcmSheetRow>> empRows)
    {
        var entries = new List<ClassEntry>();
        foreach (var (empNo, rows) in empRows)
        {
            foreach (var r in rows)
            {
                if (!string.IsNullOrWhiteSpace(r.CostItem))
                {
                    var cls = _mappings.GetClassification(r.CostItem, HcmMapping.SourceColumns.CostItem);
                    if (cls != null && cls != HcmMapping.Classifications.Ignore)
                        entries.Add(new ClassEntry(
                            empNo,
                            _mappings.GetCanonical(r.CostItem, HcmMapping.SourceColumns.CostItem),
                            r.CostItemAmt,
                            cls));
                }

                if (!string.IsNullOrWhiteSpace(r.VendorName))
                {
                    var cls = _mappings.GetClassification(r.VendorName, HcmMapping.SourceColumns.VendorName);
                    if (cls != null && cls != HcmMapping.Classifications.Ignore)
                        entries.Add(new ClassEntry(
                            empNo,
                            _mappings.GetCanonical(r.VendorName, HcmMapping.SourceColumns.VendorName),
                            r.VendorAmount,
                            cls));
                }
            }
        }
        return entries;
    }

    // ── Stage 3: salary ───────────────────────────────────────────────────────
    // First BASIC_SALARY entry per employee (no dedup needed — salary is a single item).

    private static Dictionary<string, decimal> ComputeSalary(List<ClassEntry> entries)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries.Where(e => e.Classification == HcmMapping.Classifications.BasicSalary))
            result.TryAdd(e.EmpNo, e.Amount);
        return result;
    }

    // ── Stage 4: stat ─────────────────────────────────────────────────────────
    // Sources: any COSTITEM or VENDOR_NAME classified STATUTORY.
    // Step 1 — deduplicate by (empNo, canonical): keep MAX amount per canonical group.
    // Step 2 — sum the deduplicated canonicals per employee.

    private static Dictionary<string, decimal> ComputeStat(List<ClassEntry> entries)
        => AggregateByCanonical(entries, HcmMapping.Classifications.Statutory);

    // ── Stage 5: allow ────────────────────────────────────────────────────────
    // Same two-step logic as stat but for ALLOWANCE classification.

    private static Dictionary<string, decimal> ComputeAllow(List<ClassEntry> entries)
        => AggregateByCanonical(entries, HcmMapping.Classifications.Allowance);

    // Step 1: for each (empNo, canonical) keep MAX amount (removes duplicates/aliases).
    // Step 2: sum the deduplicated canonicals per employee.
    private static Dictionary<string, decimal> AggregateByCanonical(
        List<ClassEntry> entries, string classification)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var byEmp  = entries
            .Where(e => e.Classification == classification)
            .GroupBy(e => e.EmpNo, StringComparer.OrdinalIgnoreCase);

        foreach (var empGroup in byEmp)
        {
            var total = empGroup
                .GroupBy(e => e.Canonical, StringComparer.OrdinalIgnoreCase)
                .Sum(g => g.Max(e => e.Amount));
            result[empGroup.Key] = total;
        }
        return result;
    }

    // ── Stage 6: HCM DB queries ───────────────────────────────────────────────
    // All queries normalize both sides to 15-char padded via LPAD so that raw
    // Excel values ("14560") match however the DB stores the employeenumber.
    // Results are keyed by NormalizeIpps(employeenumber) for consistent lookup.

    private record EmployeeInfo(long Id, string? IsActive, string? Terms);

    private static async Task<Dictionary<string, EmployeeInfo>>
        QueryEmployeeFieldsAsync(MySqlConnection conn, List<string> empNumbers, CancellationToken ct)
    {
        var result = new Dictionary<string, EmployeeInfo>(StringComparer.OrdinalIgnoreCase);
        if (empNumbers.Count == 0) return result;

        // Normalize to 15-char padded before passing to IN clause
        var padded = empNumbers.Select(n => NormalizeIpps(n)).Distinct().ToList();

        foreach (var batch in Batch(padded))
        {
            var (inClause, cmd) = BuildInClause(batch, conn);
            cmd.CommandText = $@"
                SELECT e.id, e.employeenumber, e.isactive, e.terms
                FROM employees e
                WHERE LPAD(e.employeenumber, 15, '0') IN ({inClause})";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var key      = NormalizeIpps(reader.GetString("employeenumber"));
                var id       = reader.GetInt64(reader.GetOrdinal("id"));
                var isActive = reader.IsDBNull(reader.GetOrdinal("isactive")) ? null : reader.GetString("isactive");
                var terms    = reader.IsDBNull(reader.GetOrdinal("terms"))    ? null : reader.GetString("terms");
                result[key]  = new EmployeeInfo(id, isActive, terms);
            }
        }
        return result;
    }

    private static async Task<Dictionary<string, decimal>>
        QueryDedAsync(MySqlConnection conn, List<string> empNumbers, CancellationToken ct)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (empNumbers.Count == 0) return result;

        var padded = empNumbers.Select(n => NormalizeIpps(n)).Distinct().ToList();

        foreach (var batch in Batch(padded))
        {
            var (inClause, cmd) = BuildInClause(batch, conn);
            cmd.CommandText = $@"
                SELECT LPAD(e.employeenumber, 15, '0') AS empkey,
                       SUM(CASE WHEN d.rep_amount > d.installmentamount
                                THEN d.rep_amount ELSE d.installmentamount END) AS ded
                FROM deductions d
                INNER JOIN employees e ON d.employeeid = e.id
                WHERE LPAD(e.employeenumber, 15, '0') IN ({inClause})
                  AND (
                      (d.status = 'takenup'  AND d.isactive = 'Y')
                      OR (d.status = 'reserved' AND d.rep_status = 'Pending_approval' AND d.isactive = 'Y')
                      OR (d.status = 'reserved' AND (d.rep_status = '0' OR d.rep_status IS NULL OR d.rep_status = ''))
                      OR (d.status = 'reserved' AND d.is_bank_res = 'Y')
                  )
                GROUP BY LPAD(e.employeenumber, 15, '0')";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result[reader.GetString("empkey")] = ReadDecimal(reader, "ded");
        }
        return result;
    }

    private static async Task<Dictionary<string, decimal>>
        QueryStanbicDedAsync(MySqlConnection conn, List<string> empNumbers, CancellationToken ct)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (empNumbers.Count == 0) return result;

        var padded = empNumbers.Select(n => NormalizeIpps(n)).Distinct().ToList();

        foreach (var batch in Batch(padded))
        {
            var (inClause, cmd) = BuildInClause(batch, conn);
            cmd.CommandText = $@"
                SELECT LPAD(e.employeenumber, 15, '0') AS empkey,
                       SUM(CASE WHEN d.rep_amount > d.installmentamount
                                THEN d.rep_amount ELSE d.installmentamount END) AS stanbic
                FROM deductions d
                INNER JOIN employees e ON d.employeeid = e.id
                WHERE LPAD(e.employeenumber, 15, '0') IN ({inClause})
                  AND d.deductiontype = '265'
                  AND (
                      (d.status = 'reserved' AND d.rep_status = 'Pending_approval' AND d.isactive = 'Y')
                      OR (d.status = 'reserved' AND (d.rep_status = '0' OR d.rep_status IS NULL OR d.rep_status = ''))
                      OR (d.status = 'reserved' AND d.is_bank_res = 'Y')
                  )
                GROUP BY LPAD(e.employeenumber, 15, '0')";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result[reader.GetString("empkey")] = ReadDecimal(reader, "stanbic");
        }
        return result;
    }

    private static async Task<Dictionary<string, decimal>>
        QueryOtherReservationsAsync(MySqlConnection conn, List<string> empNumbers, CancellationToken ct)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (empNumbers.Count == 0) return result;

        var padded = empNumbers.Select(n => NormalizeIpps(n)).Distinct().ToList();

        foreach (var batch in Batch(padded))
        {
            var (inClause, cmd) = BuildInClause(batch, conn);
            cmd.CommandText = $@"
                SELECT LPAD(e.employeenumber, 15, '0') AS empkey,
                       SUM(CASE WHEN d.rep_amount > d.installmentamount
                                THEN d.rep_amount ELSE d.installmentamount END) AS other_res
                FROM deductions d
                INNER JOIN employees e ON d.employeeid = e.id
                WHERE LPAD(e.employeenumber, 15, '0') IN ({inClause})
                  AND d.deductiontype <> '265'
                  AND d.datecreated >= DATE_SUB(CURDATE(), INTERVAL 2 MONTH)
                  AND (
                      (d.status = 'reserved' AND d.rep_status = 'Pending_approval' AND d.isactive = 'Y')
                      OR (d.status = 'reserved' AND (d.rep_status = '0' OR d.rep_status IS NULL OR d.rep_status = ''))
                      OR (d.status = 'reserved' AND d.is_bank_res = 'Y')
                  )
                GROUP BY LPAD(e.employeenumber, 15, '0')";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result[reader.GetString("empkey")] = ReadDecimal(reader, "other_res");
        }
        return result;
    }

    // ── Stage 8: Excel output ─────────────────────────────────────────────────

    private static string WriteExcel(
        List<CrbOutputRow> rows, string storageRoot, bool isHcm, bool includeOtherReservations)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var now      = DateTime.UtcNow;
        var prefix   = isHcm ? "CRB_HCM" : "CRB_IPPS";
        var fileName = $"{prefix}_{now:yyyyMMdd}.xlsx";
        var dir      = Path.Combine(storageRoot, "crb", now.Year.ToString(), now.ToString("MM"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, fileName);

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("CRB Deductions");

        var headers = new List<string>
        {
            "IPPSNO", "SALARY", "ISACTIVE", "STAT", "ALLOW",
            "DEDS", "STANBIC", "AFFORDABILITY", "PROBATION", "VOTE", "VOTENAME"
        };
        if (includeOtherReservations) headers.Add("OTHER_RES");

        WriteHeaders(ws, headers.ToArray());

        for (int i = 0; i < rows.Count; i++)
        {
            var r   = rows[i];
            var row = i + 2;
            ws.Cells[row, 1].Value  = r.Ipps;
            ws.Cells[row, 2].Value  = r.Salary;
            ws.Cells[row, 3].Value  = r.IsActive ?? string.Empty;
            ws.Cells[row, 4].Value  = r.Stat;
            ws.Cells[row, 5].Value  = r.Allow;
            ws.Cells[row, 6].Value  = r.Ded;
            ws.Cells[row, 7].Value  = r.Stanbic;
            ws.Cells[row, 8].Value  = r.Affordability;
            ws.Cells[row, 9].Value  = r.Terms ?? string.Empty;
            ws.Cells[row, 10].Value = r.Vote;
            ws.Cells[row, 11].Value = r.VoteName;
            if (includeOtherReservations)
                ws.Cells[row, 12].Value = r.OtherReservations ?? 0m;

            //foreach (int col in new[] { 2, 4, 5, 6, 7, 8 })
            //    ws.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
        }

        if (ws.Dimension != null)
        {
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            ws.View.FreezePanes(2, 1);
            ws.Cells[1, 1, 1, headers.Count].AutoFilter = true;
        }

        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    // ── HCM source parser ─────────────────────────────────────────────────────
    // The HCM export outgrew Excel's 1,048,576-row limit and now arrives as a
    // CSV/TSV; .xlsx is still accepted. Only rows for Stanbic employees are kept,
    // so a multi-million-row file is streamed rather than held in memory.

    private static readonly string[] RequiredHcmColumns =
    {
        "EMPLOYEE_NO", "EMPLOYEE_NAME", "VOTE_CODE", "VOTE_NAME",
        "COSTITEM", "COSTITEM_AMT", "VENDOR_NAME", "VENDOR_AMOUNT"
    };

    public static HcmParseResult ParseHcmFile(IFormFile file, IReadOnlySet<string> keepIpps)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext is ".xlsx" or ".xls"
            ? ParseHcmExcel(file, keepIpps)
            : ParseHcmDelimited(file, keepIpps);
    }

    private static HcmParseResult ParseHcmDelimited(IFormFile file, IReadOnlySet<string> keepIpps)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,
            GetDelimiter    = DetectHeaderDelimiter,
            BadDataFound    = null   // stray quote inside a field: keep the text as-is
        };
        // StreamReader strips a UTF-8/UTF-16 BOM, which would otherwise end up in the first header
        using var parser = new CsvParser(new StreamReader(file.OpenReadStream()), config);

        if (!parser.Read()) throw new InvalidOperationException("The HCM file is empty.");
        var headerCount = parser.Count;
        var colIndex    = BuildHcmColumnIndex(
            Enumerable.Range(0, headerCount).Select(i => parser[i]), firstIndex: 0);

        Func<string, string> get = name =>
        {
            var idx = colIndex[name];
            return idx < parser.Count ? parser[idx].Trim() : string.Empty;
        };

        var collector = new HcmRowCollector(keepIpps);
        int ragged    = 0;
        while (parser.Read())
        {
            if (parser.Count != headerCount) ragged++;
            collector.Add(get("EMPLOYEE_NO"), get, get);
        }
        return collector.ToResult(ragged);
    }

    // Decide from the header line alone: data rows can carry commas (names,
    // formatted amounts) that would skew a count across the whole buffer.
    private static string DetectHeaderDelimiter(GetDelimiterArgs args)
    {
        var text   = args.Text;
        var end    = text.IndexOfAny(new[] { '\r', '\n' });
        var header = end >= 0 ? text[..end] : text;
        return new[] { "\t", ",", ";", "|" }.MaxBy(d => header.Split(d).Length)!;
    }

    private static HcmParseResult ParseHcmExcel(IFormFile file, IReadOnlySet<string> keepIpps)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var stream = file.OpenReadStream();
        using var pkg    = new ExcelPackage(stream);

        var ws = pkg.Workbook.Worksheets.FirstOrDefault();
        if (ws?.Dimension == null) throw new InvalidOperationException("The HCM file is empty.");

        var colIndex = BuildHcmColumnIndex(
            Enumerable.Range(1, ws.Dimension.Columns).Select(c => ws.Cells[1, c].Text), firstIndex: 1);

        var collector = new HcmRowCollector(keepIpps);
        for (int r = 2; r <= ws.Dimension.Rows; r++)
        {
            string Get(string name) => ws.Cells[r, colIndex[name]].Text?.Trim() ?? string.Empty;
            string GetAmt(string name) =>
                Convert.ToString(ws.Cells[r, colIndex[name]].Value, CultureInfo.InvariantCulture) ?? string.Empty;

            // Read EMPLOYEE_NO (= IPPS) via .Value to avoid scientific notation / thousand separators
            var cell       = ws.Cells[r, colIndex["EMPLOYEE_NO"]];
            var employeeNo = cell.Value is double d
                ? ((long)Math.Round(d)).ToString()
                : cell.Text?.Trim() ?? string.Empty;

            collector.Add(employeeNo, Get, GetAmt);
        }
        return collector.ToResult();
    }

    // Header name → column position. A missing column throws: before, a missing
    // EMPLOYEE_NO skipped every row and sent all Stanbic numbers to Module 2 silently.
    private static Dictionary<string, int> BuildHcmColumnIndex(IEnumerable<string?> headers, int firstIndex)
    {
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var c = firstIndex;
        foreach (var h in headers)
        {
            var header = h?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(header))
                colIndex[header] = c;
            c++;
        }

        var missing = RequiredHcmColumns.Where(name => !colIndex.ContainsKey(name)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"The HCM file is missing required column(s): {string.Join(", ", missing)}.");
        return colIndex;
    }

    // Shared by both readers: counts every data row, builds only the Stanbic ones.
    private sealed class HcmRowCollector(IReadOnlySet<string> keepIpps)
    {
        private readonly List<HcmSheetRow> _rows = new();
        private int _totalRows, _unparsedAmounts;

        public void Add(string employeeNo, Func<string, string> text, Func<string, string> amount)
        {
            if (string.IsNullOrWhiteSpace(employeeNo)) return;
            _totalRows++;
            if (!keepIpps.Contains(NormalizeIpps(employeeNo))) return;

            _rows.Add(new HcmSheetRow(
                EmployeeNo:   employeeNo,
                EmployeeName: text("EMPLOYEE_NAME"),
                VoteCode:     text("VOTE_CODE"),
                VoteName:     text("VOTE_NAME"),
                CostItem:     HcmMappingService.Normalize(text("COSTITEM")),
                CostItemAmt:  ParseAmount(amount("COSTITEM_AMT")),
                VendorName:   HcmMappingService.Normalize(text("VENDOR_NAME")),
                VendorAmount: ParseAmount(amount("VENDOR_AMOUNT"))));
        }

        private decimal ParseAmount(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0m;
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            _unparsedAmounts++;
            return 0m;
        }

        public HcmParseResult ToResult(int raggedRows = 0)
            => new(_rows, _totalRows, _unparsedAmounts, raggedRows);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private const int BatchSize = 1000;

    private static IEnumerable<List<T>> Batch<T>(List<T> source)
    {
        for (int i = 0; i < source.Count; i += BatchSize)
            yield return source.GetRange(i, Math.Min(BatchSize, source.Count - i));
    }

    private static (string clause, MySqlCommand cmd) BuildInClause<T>(
        List<T> values, MySqlConnection conn)
    {
        var cmd        = new MySqlCommand("", conn);
        var paramNames = new List<string>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            var name = $"@p{i}";
            paramNames.Add(name);
            cmd.Parameters.AddWithValue(name, values[i]);
        }
        return (string.Join(",", paramNames), cmd);
    }

    private static decimal ReadDecimal(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0m;
        return decimal.TryParse(
            reader.GetValue(ordinal)?.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private static void WriteHeaders(ExcelWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 78, 121));
            cell.Style.Font.Color.SetColor(Color.White);
        }
    }

    // Strip commas, parse as decimal, then PadLeft(15).
    // Handles: "14,560" → "000000000014560", "14560.0" → "000000000014560".
    private static string NormalizeIpps(string raw)
    {
        var clean = raw.Trim().Replace(",", "");
        if (decimal.TryParse(clean,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return ((long)d).ToString().PadLeft(15, '0');
        return clean.PadLeft(15, '0');
    }
}
