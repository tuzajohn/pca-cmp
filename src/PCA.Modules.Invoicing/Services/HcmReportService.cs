using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PCA.Modules.Invoicing.Models;
using System.Drawing;

namespace PCA.Modules.Invoicing.Services;

// ── In-memory row from HCM Sheet1 ────────────────────────────────────────────

public record HcmSheetRow(
    string  EmpNumber,     // EMP_NUMBER  (= IPPS)
    string  EmployeeNo,    // EMPLOYEE_NO (= internal DB id in HCM)
    string  EmployeeName,
    string  VoteCode,
    string  VoteName,
    string  CostItem,
    decimal CostItemAmt,
    string  VendorName,
    decimal VendorAmount);

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
    int ZeroAfford);

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
        CancellationToken ct = default)
    {
        void Step(string msg) { progress?.Invoke(msg); _logger.LogInformation("HCM: {Msg}", msg); }

        // Stage 1 — parse + cross-reference
        Step("Stage 1 — Parsing files and cross-referencing IPPS numbers…");
        var stanbicIpps = CrbReportService.ParseIppsFile(stanbicFile);
        var hcmRows     = ParseHcmExcel(hcmFile);

        var stanbicSet = new HashSet<string>(
            stanbicIpps.Select(n => n.Trim().PadLeft(15, '0')),
            StringComparer.OrdinalIgnoreCase);

        var matched   = hcmRows.Where(r => stanbicSet.Contains(PadIpps(r.EmpNumber))).ToList();
        var unmatched = stanbicIpps
            .Where(n => !matched.Any(r => PadIpps(r.EmpNumber) == n.Trim().PadLeft(15, '0')))
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

        Step("Stage 4 — Querying employee records (isactive, terms)…");
        var (isActiveMap, termsMap) = await QueryEmployeeFieldsAsync(conn, empNumbers, ct);

        Step("Stage 5 — Querying deductions…");
        var dedMap        = await QueryDedAsync(conn, empNumbers, ct);
        var stanbicDedMap = await QueryStanbicDedAsync(conn, empNumbers, ct);
        Step($"Stage 5 — Deductions loaded for {dedMap.Count} employees");

        // Stage 6 — affordability + assemble output
        var outputRows    = new List<CrbOutputRow>();
        int withStat = 0, withAllow = 0, withDed = 0, withStanbic = 0, zeroAfford = 0;

        // Use matched list to preserve order; take first row per employee for bio data
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in matched)
        {
            var empNo = r.EmployeeNo;
            if (!seen.Add(empNo)) continue;

            var salary   = salaryMap.TryGetValue(empNo, out var sv) ? sv : 0m;
            var stat     = statMap.TryGetValue(empNo, out var stv)  ? stv : 0m;
            var allow    = allowMap.TryGetValue(empNo, out var av)   ? av  : 0m;
            var ded      = dedMap.TryGetValue(empNo, out var dv)     ? dv  : 0m;
            var stanbic  = stanbicDedMap.TryGetValue(empNo, out var sbv) ? sbv : 0m;
            var afford   = Math.Max(0m, salary * 0.48m - (stat + ded));

            if (stat    > 0) withStat++;
            if (allow   > 0) withAllow++;
            if (ded     > 0) withDed++;
            if (stanbic > 0) withStanbic++;
            if (afford == 0) zeroAfford++;

            outputRows.Add(new CrbOutputRow(
                Ipps:         PadIpps(r.EmpNumber),
                EmployeeId:   int.TryParse(empNo, out var eid) ? eid : 0,
                EmpName:      r.EmployeeName,
                Vote:         r.VoteCode,
                VoteName:     r.VoteName,
                Salary:       salary,
                Terms:        termsMap.TryGetValue(empNo, out var t) ? t : null,
                IsActive:     isActiveMap.TryGetValue(empNo, out var ia) ? ia : null,
                Stat:         stat,
                Allow:        allow,
                Ded:          ded,
                Stanbic:      stanbic,
                Affordability: afford,
                Notes:        string.Empty));
        }

        Step($"Stage 6 — Affordability computed for {outputRows.Count} employees");

        // Stage 7 — write HCM Excel
        Step("Stage 7 — Writing HCM output file…");
        var hcmFilePath = WriteExcel(outputRows, storageRoot, isHcm: true);
        Step("Stage 7 — HCM file written");

        // Stage 8 — Run Module 2 for unmatched IPPS
        Step($"Stage 8 — Running Module 2 for {unmatched.Count} unmatched IPPS numbers…");
        var ippsResult = await _ippsModule.GenerateAsync(unmatched, storageRoot, progress, ct);

        _logger.LogInformation(
            "HCM CRB run log:\n  Total Stanbic IPPS submitted:     {Total}\n  Matched to HCM Sheet1:            {Matched}\n  Passed to IPPS module:            {Unmatched}\n  Unknown COSTITEM/VENDOR flagged:  0 (already resolved)\n  Employees with stat > 0:          {Stat}\n  Employees with allow > 0:         {Allow}\n  Employees with ded > 0:           {Ded}\n  Employees with stanbic > 0:       {Stanbic}\n  Employees with affordability = 0: {ZeroAff}",
            stanbicIpps.Count, matched.GroupBy(r => r.EmployeeNo).Count(),
            unmatched.Count, withStat, withAllow, withDed, withStanbic, zeroAfford);

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
            ZeroAfford:           zeroAfford);
    }

    // ── Check unknowns before processing (called from controller pre-flight) ──

    public async Task<List<(string RawValue, string SourceColumn)>> CheckUnknownsAsync(
        IFormFile hcmFile, IFormFile stanbicFile)
    {
        var stanbicIpps = CrbReportService.ParseIppsFile(stanbicFile);
        var hcmRows     = ParseHcmExcel(hcmFile);

        var stanbicSet = new HashSet<string>(
            stanbicIpps.Select(n => n.Trim().PadLeft(15, '0')),
            StringComparer.OrdinalIgnoreCase);
        var matched = hcmRows.Where(r => stanbicSet.Contains(PadIpps(r.EmpNumber))).ToList();

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

    private static async Task<(Dictionary<string, string?> isActive, Dictionary<string, string?> terms)>
        QueryEmployeeFieldsAsync(MySqlConnection conn, List<string> empNumbers, CancellationToken ct)
    {
        var isActive = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var terms    = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (empNumbers.Count == 0) return (isActive, terms);

        var (inClause, cmd) = BuildInClause(empNumbers, conn);
        cmd.CommandText = $@"
            SELECT e.employeenumber, e.isactive, e.terms
            FROM employees e
            WHERE e.employeenumber IN ({inClause})";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var num = reader.GetString("employeenumber");
            isActive[num] = reader.IsDBNull(reader.GetOrdinal("isactive")) ? null : reader.GetString("isactive");
            terms[num]    = reader.IsDBNull(reader.GetOrdinal("terms"))    ? null : reader.GetString("terms");
        }
        return (isActive, terms);
    }

    private static async Task<Dictionary<string, decimal>>
        QueryDedAsync(MySqlConnection conn, List<string> empNumbers, CancellationToken ct)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (empNumbers.Count == 0) return result;

        var (inClause, cmd) = BuildInClause(empNumbers, conn);
        cmd.CommandText = $@"
            SELECT e.employeenumber,
                   SUM(CASE WHEN d.rep_amount > d.installmentamount
                            THEN d.rep_amount ELSE d.installmentamount END) AS ded
            FROM deductions d
            INNER JOIN employees e ON d.employeeid = e.id
            WHERE e.employeenumber IN ({inClause})
              AND (
                  (d.status = 'takenup'  AND d.isactive = 'Y')
                  OR (d.status = 'reserved' AND d.rep_status = 'Pending_approval' AND d.isactive = 'Y')
                  OR (d.status = 'reserved' AND (d.rep_status = '0' OR d.rep_status IS NULL OR d.rep_status = ''))
                  OR (d.status = 'reserved' AND d.is_bank_res = 'Y')
              )
            GROUP BY e.employeenumber";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString("employeenumber")] = ReadDecimal(reader, "ded");
        return result;
    }

    private static async Task<Dictionary<string, decimal>>
        QueryStanbicDedAsync(MySqlConnection conn, List<string> empNumbers, CancellationToken ct)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (empNumbers.Count == 0) return result;

        var (inClause, cmd) = BuildInClause(empNumbers, conn);
        cmd.CommandText = $@"
            SELECT e.employeenumber,
                   SUM(CASE WHEN d.rep_amount > d.installmentamount
                            THEN d.rep_amount ELSE d.installmentamount END) AS stanbic
            FROM deductions d
            INNER JOIN employees e ON d.employeeid = e.id
            WHERE e.employeenumber IN ({inClause})
              AND (
                  (d.status = 'reserved' AND d.rep_status = 'Pending_approval' AND d.isactive = 'Y')
                  OR (d.status = 'reserved' AND (d.rep_status = '0' OR d.rep_status IS NULL OR d.rep_status = ''))
                  OR (d.status = 'reserved' AND d.is_bank_res = 'Y')
              )
            GROUP BY e.employeenumber";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString("employeenumber")] = ReadDecimal(reader, "stanbic");
        return result;
    }

    // ── Stage 8: Excel output ─────────────────────────────────────────────────

    private static string WriteExcel(List<CrbOutputRow> rows, string storageRoot, bool isHcm)
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

        string[] headers =
        {
            "IPPSNO", "SALARY", "ISACTIVE", "STAT", "ALLOW",
            "DEDS", "STANBIC", "AFFORDABILITY", "PROBATION", "VOTE"
        };

        WriteHeaders(ws, headers);

        for (int i = 0; i < rows.Count; i++)
        {
            var r   = rows[i];
            var row = i + 2;
            ws.Cells[row, 1].Value  = r.Ipps;
            ws.Cells[row, 2].Value  = r.Salary;
            ws.Cells[row, 3].Value  = r.IsActive ?? "0";
            ws.Cells[row, 4].Value  = r.Stat;
            ws.Cells[row, 5].Value  = r.Allow;
            ws.Cells[row, 6].Value  = r.Ded;
            ws.Cells[row, 7].Value  = r.Stanbic;
            ws.Cells[row, 8].Value  = r.Affordability;
            ws.Cells[row, 9].Value  = r.Terms ?? "0";
            ws.Cells[row, 10].Value = r.Vote;

            foreach (int col in new[] { 2, 4, 5, 6, 7, 8 })
                ws.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
        }

        if (ws.Dimension != null)
        {
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            ws.View.FreezePanes(2, 1);
            ws.Cells[1, 1, 1, headers.Length].AutoFilter = true;
        }

        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    // ── HCM Excel parser ──────────────────────────────────────────────────────

    public static List<HcmSheetRow> ParseHcmExcel(IFormFile file)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var rows = new List<HcmSheetRow>();
        using var stream = file.OpenReadStream();
        using var pkg    = new ExcelPackage(stream);

        var ws = pkg.Workbook.Worksheets.FirstOrDefault();
        if (ws?.Dimension == null) return rows;

        // Build column index from header row
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 1; c <= ws.Dimension.Columns; c++)
        {
            var header = ws.Cells[1, c].Text?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(header))
                colIndex[header] = c;
        }

        string Col(string name) => colIndex.TryGetValue(name, out var idx)
            ? ws.Cells[2, idx].Text : string.Empty; // placeholder — fixed below

        for (int r = 2; r <= ws.Dimension.Rows; r++)
        {
            string Get(string name) => colIndex.TryGetValue(name, out var idx)
                ? ws.Cells[r, idx].Text?.Trim() ?? string.Empty : string.Empty;

            decimal GetAmt(string name)
            {
                if (!colIndex.TryGetValue(name, out var idx)) return 0m;
                var raw = ws.Cells[r, idx].Value;
                if (raw == null) return 0m;
                return decimal.TryParse(raw.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
            }

            var empNum = Get("EMP_NUMBER");
            if (string.IsNullOrWhiteSpace(empNum)) continue;

            rows.Add(new HcmSheetRow(
                EmpNumber:    empNum,
                EmployeeNo:   Get("EMPLOYEE_NO"),
                EmployeeName: Get("EMPLOYEE_NAME"),
                VoteCode:     Get("VOTE_CODE"),
                VoteName:     Get("VOTE_NAME"),
                CostItem:     Get("COSTITEM"),
                CostItemAmt:  GetAmt("COSTITEM_AMT"),
                VendorName:   Get("VENDOR_NAME"),
                VendorAmount: GetAmt("VENDOR_AMOUNT")));
        }
        return rows;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

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

    private static string PadIpps(string raw)
        => raw.Trim().PadLeft(15, '0');
}
