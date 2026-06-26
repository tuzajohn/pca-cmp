using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace PCA.Modules.Invoicing.Services;

// ── Records ──────────────────────────────────────────────────────────────────

public record CrbEmployeeRow(
    long   EmployeeId,
    string Ipps,
    string EmpName,
    string Vote,
    string VoteName,
    decimal Salary,
    string? Terms,
    string? IsActive);

public record CrbOutputRow(
    string  Ipps,
    long    EmployeeId,
    string  EmpName,
    string  Vote,
    string  VoteName,
    decimal Salary,
    string? Terms,
    string? IsActive,
    decimal Stat,
    decimal Allow,
    decimal Ded,
    decimal Stanbic,
    decimal Affordability,
    string  Notes);

public record CrbReportResult(
    string FilePath,
    string FileName,
    int    TotalSubmitted,
    int    Matched,
    int    Unmatched,
    int    WithStat,
    int    WithAllow,
    int    AllowDateMismatches,
    int    WithDed,
    int    WithStanbic,
    int    ZeroAfford);

// ── Service ───────────────────────────────────────────────────────────────────

public class CrbReportService
{
    private readonly ExternalDbSettings _ipps;
    private readonly ILogger<CrbReportService> _logger;

    public CrbReportService(ExternalDbSettings ippsSettings, ILogger<CrbReportService> logger)
    {
        _ipps   = ippsSettings;
        _logger = logger;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    public async Task<CrbReportResult> GenerateAsync(
        List<string> rawIppsNumbers,
        string storageRoot,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        void Step(string msg) { progress?.Invoke(msg); _logger.LogInformation("CRB: {Msg}", msg); }

        var totalSubmitted = rawIppsNumbers.Count;

        var paddedList = rawIppsNumbers
            .Select(n => n.Trim().PadLeft(15, '0'))
            .Distinct()
            .ToList();

        Step("Module 2 — Opening IPPS database connection…");
        using var tunnel = await SshTunnelService.OpenAsync(_ipps, _logger);
        var conn = tunnel.Connection;

        Step($"Module 2 — Matching {totalSubmitted} IPPS numbers to employee records…");
        var (empMap, unmatched) = await Stage1_MatchEmployeesAsync(conn, paddedList, ct);
        Step($"Module 2 — {empMap.Count} matched, {unmatched.Count} unmatched");

        var employeeIds = empMap.Keys.ToList();

        Step("Module 2 — Querying statutory deductions…");
        var statMap = await Stage2_StatutoryAsync(conn, employeeIds, ct);

        Step("Module 2 — Querying allowances…");
        var (allowMap, mismatches) = await Stage3_AllowancesAsync(conn, employeeIds, statMap, ct);

        Step("Module 2 — Querying loan deductions…");
        var dedMap = await Stage4_DeductionsAsync(conn, employeeIds, ct);

        Step("Module 2 — Computing affordability and writing output file…");
        var (outputRows, result) = BuildOutput(
            paddedList, empMap, statMap, allowMap, mismatches, dedMap, unmatched);

        string filePath;
        try
        {
            filePath = WriteExcel(outputRows, unmatched, storageRoot);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"All database queries completed successfully ({result.Matched} matched, {result.Unmatched} unmatched) " +
                $"but the Excel file could not be written: {ex.Message}. " +
                "Re-running will skip the long DB phase — please retry.", ex);
        }
        var fileName = Path.GetFileName(filePath);

        _logger.LogInformation("CRB: complete — {FilePath}", filePath);
        _logger.LogInformation(
            "CRB run log:\n  Total IPPS submitted:       {Total}\n  Matched employees:          {Matched}\n  Unmatched IPPS:             {Unmatched}\n  Employees with stat > 0:    {Stat}\n  Employees with allow > 0:   {Allow}\n  Allowance date mismatches:  {Mis}\n  Employees with ded > 0:     {Ded}\n  Employees with stanbic > 0: {Stanbic}\n  Employees with afford = 0:  {ZeroAff}",
            totalSubmitted, result.Matched, result.Unmatched,
            result.WithStat, result.WithAllow, result.AllowDateMismatches,
            result.WithDed, result.WithStanbic, result.ZeroAfford);

        return result with { FilePath = filePath, FileName = fileName };
    }

    // ── Stage 1 ───────────────────────────────────────────────────────────────

    private static async Task<(Dictionary<long, CrbEmployeeRow> empMap, List<string> unmatched)>
        Stage1_MatchEmployeesAsync(MySqlConnection conn, List<string> padded, CancellationToken ct)
    {
        var empMap  = new Dictionary<long, CrbEmployeeRow>();
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (padded.Count == 0)
            return (empMap, padded);

        foreach (var batch in Batch(padded))
        {
            var (inClause, cmd) = BuildInClause(batch, conn);
            cmd.CommandText = $@"
                SELECT e.id AS employeeid,
                       e.employeenumber AS ipps,
                       CONCAT_WS(' ', e.firstname, e.lastname) AS emp_name,
                       dep.code AS vote,
                       dep.description AS votename,
                       e.salary,
                       e.terms,
                       e.isactive
                FROM employees e
                INNER JOIN departments dep ON e.department = dep.code
                WHERE e.employeenumber IN ({inClause})";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id   = reader.GetInt64(reader.GetOrdinal("employeeid"));
                var ipps = reader.GetString("ipps");
                empMap[id] = new CrbEmployeeRow(
                    EmployeeId: id,
                    Ipps:       ipps,
                    EmpName:    reader.GetString("emp_name"),
                    Vote:       reader.GetString("vote"),
                    VoteName:   reader.GetString("votename"),
                    Salary:     ReadDecimal(reader, "salary"),
                    Terms:      reader.IsDBNull(reader.GetOrdinal("terms"))    ? "" : reader.GetString("terms"),
                    IsActive:   reader.IsDBNull(reader.GetOrdinal("isactive")) ? "" : reader.GetString("isactive"));
                matched.Add(ipps);
            }
        }

        var unmatched = padded.Where(p => !matched.Contains(p)).ToList();
        return (empMap, unmatched);
    }

    // ── Stage 2 ───────────────────────────────────────────────────────────────

    private static async Task<Dictionary<long, (decimal Total, DateTime? Date)>>
        Stage2_StatutoryAsync(MySqlConnection conn, List<long> ids, CancellationToken ct)
    {
        var result = new Dictionary<long, (decimal, DateTime?)>();
        if (ids.Count == 0) return result;

        foreach (var batch in Batch(ids))
        {
            var (inClause, cmd) = BuildInClause(batch, conn);
            cmd.CommandText = $@"
                SELECT stat.employeeid,
                       SUM(stat.deductionamount) AS total_statutory,
                       MAX(stat.payrolldate)     AS stat_payrolldate
                FROM statutorydeductions stat
                INNER JOIN (
                    SELECT employeeid, MAX(payrolldate) AS max_date
                    FROM statutorydeductions
                    WHERE employeeid IN ({inClause})
                    GROUP BY employeeid
                ) latest ON stat.employeeid = latest.employeeid
                        AND stat.payrolldate = latest.max_date
                WHERE stat.employeeid IN ({inClause})
                GROUP BY stat.employeeid";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id    = reader.GetInt64(reader.GetOrdinal("employeeid"));
                var total = ReadDecimal(reader, "total_statutory");
                var date  = reader.IsDBNull(reader.GetOrdinal("stat_payrolldate")) ? (DateTime?)null : reader.GetDateTime("stat_payrolldate");
                result[id] = (total, date);
            }
        }
        return result;
    }

    // ── Stage 3 ───────────────────────────────────────────────────────────────

    private static async Task<(Dictionary<long, decimal> allowMap, HashSet<long> mismatches)>
        Stage3_AllowancesAsync(
            MySqlConnection conn,
            List<long> ids,
            Dictionary<long, (decimal Total, DateTime? Date)> statMap,
            CancellationToken ct)
    {
        var allowMap   = new Dictionary<long, decimal>();
        var mismatches = new HashSet<long>();
        if (ids.Count == 0) return (allowMap, mismatches);

        foreach (var batch in Batch(ids))
        {
            var (inClause, cmd) = BuildInClause(batch, conn);
            cmd.CommandText = $@"
                SELECT stat.employeeid,
                       SUM(stat.amount)      AS total_allowance,
                       MAX(stat.payrolldate) AS allow_payrolldate
                FROM employeeallowances stat
                LEFT JOIN systemcodes s ON stat.code = s.code
                INNER JOIN (
                    SELECT employeeid, MAX(payrolldate) AS max_date
                    FROM employeeallowances
                    WHERE employeeid IN ({inClause})
                    GROUP BY employeeid
                ) latest ON stat.employeeid = latest.employeeid
                        AND stat.payrolldate = latest.max_date
                WHERE stat.employeeid IN ({inClause})
                  AND s.IsRecurring = '1'
                GROUP BY stat.employeeid";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id        = reader.GetInt64(reader.GetOrdinal("employeeid"));
                var total     = ReadDecimal(reader, "total_allowance");
                var allowDate = reader.IsDBNull(reader.GetOrdinal("allow_payrolldate")) ? (DateTime?)null : reader.GetDateTime("allow_payrolldate");
                var statDate  = statMap.TryGetValue(id, out var s) ? s.Date : null;

                if (allowDate.HasValue && statDate.HasValue &&
                    allowDate.Value.Date != statDate.Value.Date)
                {
                    mismatches.Add(id);
                    allowMap[id] = 0m;
                }
                else
                {
                    allowMap[id] = total;
                }
            }
        }
        return (allowMap, mismatches);
    }

    // ── Stage 4 ───────────────────────────────────────────────────────────────

    private static async Task<Dictionary<long, (decimal Ded, decimal Stanbic)>>
        Stage4_DeductionsAsync(MySqlConnection conn, List<long> ids, CancellationToken ct)
    {
        var result = new Dictionary<long, (decimal, decimal)>();
        if (ids.Count == 0) return result;

        foreach (var batch in Batch(ids))
        {
            var (inClause, cmd) = BuildInClause(batch, conn);
            cmd.CommandText = $@"
                SELECT d.employeeid,
                       SUM(CASE WHEN d.rep_amount > d.installmentamount
                                THEN d.rep_amount
                                ELSE d.installmentamount END) AS ded,
                       SUM(CASE WHEN d.deductiontype = '265'
                                THEN CASE WHEN d.rep_amount > d.installmentamount
                                          THEN d.rep_amount
                                          ELSE d.installmentamount END
                                ELSE 0 END) AS stanbic
                FROM deductions d
                WHERE d.employeeid IN ({inClause})
                  AND (
                      (d.status = 'takenup'  AND d.isactive = 'Y')
                      OR (d.status = 'reserved' AND d.rep_status = 'Pending_approval' AND d.isactive = 'Y')
                      OR (d.status = 'reserved' AND (d.rep_status = '0' OR d.rep_status IS NULL OR d.rep_status = ''))
                      OR (d.status = 'reserved' AND d.is_bank_res = 'Y')
                  )
                GROUP BY d.employeeid";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id      = reader.GetInt64(reader.GetOrdinal("employeeid"));
                var ded     = ReadDecimal(reader, "ded");
                var stanbic = ReadDecimal(reader, "stanbic");
                result[id] = (ded, stanbic);
            }
        }
        return result;
    }

    // ── Stage 5 + 6 — build output rows ───────────────────────────────────────

    private static (List<CrbOutputRow> rows, CrbReportResult stats) BuildOutput(
        List<string> paddedList,
        Dictionary<long, CrbEmployeeRow> empMap,
        Dictionary<long, (decimal Total, DateTime? Date)> statMap,
        Dictionary<long, decimal> allowMap,
        HashSet<long> mismatches,
        Dictionary<long, (decimal Ded, decimal Stanbic)> dedMap,
        List<string> unmatched)
    {
        var rows         = new List<CrbOutputRow>();
        int withStat     = 0, withAllow = 0, withDed = 0, withStanbic = 0, zeroAfford = 0;

        // Iterate in original submission order
        var ippsToEmp = new Dictionary<string, CrbEmployeeRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in empMap.Values) ippsToEmp[e.Ipps] = e;

        foreach (var ipps in paddedList)
        {
            if (!ippsToEmp.TryGetValue(ipps, out var emp)) continue;

            var id       = emp.EmployeeId;
            var stat     = statMap.TryGetValue(id, out var sv) ? sv.Total  : 0m;
            var allow    = allowMap.TryGetValue(id, out var av) ? av       : 0m;
            var ded      = dedMap.TryGetValue(id,  out var dv) ? dv.Ded    : 0m;
            var stanbic  = dedMap.TryGetValue(id,  out dv)     ? dv.Stanbic: 0m;

            var afford   = Math.Max(0m, emp.Salary * 0.48m - (stat + ded));
            var notes    = mismatches.Contains(id) ? "allowance_date_mismatch" : string.Empty;

            if (stat     > 0) withStat++;
            if (allow    > 0) withAllow++;
            if (ded      > 0) withDed++;
            if (stanbic  > 0) withStanbic++;
            if (afford  == 0) zeroAfford++;

            rows.Add(new CrbOutputRow(
                Ipps:         emp.Ipps,
                EmployeeId:   id,
                EmpName:      emp.EmpName,
                Vote:         emp.Vote,
                VoteName:     emp.VoteName,
                Salary:       emp.Salary,
                Terms:        emp.Terms,
                IsActive:     emp.IsActive,
                Stat:         stat,
                Allow:        allow,
                Ded:          ded,
                Stanbic:      stanbic,
                Affordability: afford,
                Notes:        notes));
        }

        var stats = new CrbReportResult(
            FilePath:           string.Empty,
            FileName:           string.Empty,
            TotalSubmitted:     paddedList.Count,
            Matched:            empMap.Count,
            Unmatched:          unmatched.Count,
            WithStat:           withStat,
            WithAllow:          withAllow,
            AllowDateMismatches: mismatches.Count,
            WithDed:            withDed,
            WithStanbic:        withStanbic,
            ZeroAfford:         zeroAfford);

        return (rows, stats);
    }

    // ── Stage 6 — Excel ───────────────────────────────────────────────────────

    private static string WriteExcel(List<CrbOutputRow> rows, List<string> unmatched, string storageRoot)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var now      = DateTime.UtcNow;
        var fileName = $"CRB_Deductions_{now:yyyyMMdd}.xlsx";
        var dir      = Path.Combine(storageRoot, "crb", now.Year.ToString(), now.ToString("MM"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, fileName);

        using var pkg = new ExcelPackage();

        // Main sheet
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
            ws.Cells[row, 3].Value  = r.IsActive ?? string.Empty;
            ws.Cells[row, 4].Value  = r.Stat;
            ws.Cells[row, 5].Value  = r.Allow;
            ws.Cells[row, 6].Value  = r.Ded;
            ws.Cells[row, 7].Value  = r.Stanbic;
            ws.Cells[row, 8].Value  = r.Affordability;
            ws.Cells[row, 9].Value  = r.Terms ?? string.Empty;
            ws.Cells[row, 10].Value = r.Vote;

            // Currency format for numeric columns
            foreach (int col in new[] { 2, 4, 5, 6, 7, 8 })
                ws.Cells[row, col].Style.Numberformat.Format = "#,##0.00";

            // Highlight mismatch rows
            if (!string.IsNullOrEmpty(r.Notes))
            {
                var fill = ws.Cells[row, 1, row, headers.Length].Style.Fill;
                fill.PatternType = ExcelFillStyle.Solid;
                fill.BackgroundColor.SetColor(Color.FromArgb(255, 235, 156));
            }
        }

        FinalizeSheet(ws, headers.Length);

        // Unmatched sheet
        if (unmatched.Count > 0)
        {
            var ws2 = pkg.Workbook.Worksheets.Add("Unmatched IPPS");
            WriteHeaders(ws2, new[] { "IPPS Number" });
            for (int i = 0; i < unmatched.Count; i++)
                ws2.Cells[i + 2, 1].Value = unmatched[i];
            FinalizeSheet(ws2, 1);
        }

        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private const int BatchSize = 1000;

    private static IEnumerable<List<T>> Batch<T>(List<T> source)
    {
        for (int i = 0; i < source.Count; i += BatchSize)
            yield return source.GetRange(i, Math.Min(BatchSize, source.Count - i));
    }

    private static int ReadInt(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0;
        var raw = reader.GetValue(ordinal)?.ToString();
        return int.TryParse(raw, out var v) ? v : 0;
    }

    private static decimal ReadDecimal(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return 0m;
        return decimal.TryParse(
            reader.GetValue(ordinal)?.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var v) ? v : 0m;
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

    private static void FinalizeSheet(ExcelWorksheet ws, int colCount)
    {
        if (ws.Dimension != null)
        {
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            ws.View.FreezePanes(2, 1);
        }
        ws.Cells[1, 1, 1, colCount].AutoFilter = true;
    }

    // ── Public IPPS file parser (used by controller) ──────────────────────────

    public static List<string> ParseIppsFile(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is ".xlsx" or ".xls")
            return ParseIppsExcel(file);
        return ParseIppsText(file);
    }

    private static List<string> ParseIppsText(IFormFile file)
    {
        var numbers = new List<string>();
        using var reader = new System.IO.StreamReader(file.OpenReadStream());
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            // Support comma-separated or one-per-line
            foreach (var part in line.Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0 && trimmed.All(char.IsDigit))
                    numbers.Add(trimmed);
            }
        }
        return numbers;
    }

    private static List<string> ParseIppsExcel(IFormFile file)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var numbers = new List<string>();
        using var stream = file.OpenReadStream();
        using var pkg    = new ExcelPackage(stream);
        var ws = pkg.Workbook.Worksheets.FirstOrDefault();
        if (ws == null) return numbers;

        int rows = ws.Dimension?.Rows ?? 0;
        for (int r = 1; r <= rows; r++)
        {
            var cell = ws.Cells[r, 1];
            // Numeric cells: EPPlus stores as double; .Text may render as scientific notation
            if (cell.Value is double d)
            {
                numbers.Add(((long)Math.Round(d)).ToString());
                continue;
            }
            var raw = cell.Text?.Trim();
            if (string.IsNullOrEmpty(raw)) continue;
            var clean = raw.Replace(",", "");
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                numbers.Add(((long)parsed).ToString());
        }
        return numbers;
    }
}
