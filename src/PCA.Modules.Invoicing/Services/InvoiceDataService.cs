using Microsoft.Extensions.Logging;
using MySqlConnector;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace PCA.Modules.Invoicing.Services;

public record DeductionRow(
    long EmployeeNumber,
    string ReferenceCode,
    string DeductionType,
    decimal InstallmentAmount,
    DateTime DateCreated,
    string Source);

public record CompanyRow(int Id, string CompanyName, string DeductionType);

public class InvoiceDataService
{
    public ExternalDbSettings IppsSettings { get; }
    public ExternalDbSettings HcmSettings { get; }
    private readonly ILogger<InvoiceDataService> _logger;

    public InvoiceDataService(ExternalDbSettings ippsSettings, ExternalDbSettings hcmSettings,
        ILogger<InvoiceDataService> logger)
    {
        IppsSettings = ippsSettings;
        HcmSettings  = hcmSettings;
        _logger      = logger;
    }

    public async Task<(List<DeductionRow> Rows, int IppsCount, int HcmCount)> FetchMergedDataAsync(
        string deductionCode, CancellationToken ct = default)
    {
        _logger.LogInformation("FetchMergedData: starting for deduction code {DeductionCode}", deductionCode);

        var ippsRows = await FetchDeductionsFromSourceAsync(IppsSettings, deductionCode, "IPPS", ct);
        var hcmRows  = await FetchDeductionsFromSourceAsync(HcmSettings,  deductionCode, "HCM",  ct);

        var merged = ippsRows.Concat(hcmRows)
            .GroupBy(r => r.EmployeeNumber)
            .Select(g => g.OrderByDescending(r => r.InstallmentAmount).First())
            .OrderBy(r => r.EmployeeNumber)
            .ToList();

        _logger.LogInformation(
            "FetchMergedData: IPPS={IppsCount} rows, HCM={HcmCount} rows, merged={MergedCount} rows after dedup",
            ippsRows.Count, hcmRows.Count, merged.Count);

        return (merged, ippsRows.Count, hcmRows.Count);
    }

    public async Task<List<DeductionRow>> FetchDeductionsFromSourceAsync(
        ExternalDbSettings cfg, string deductionCode, string source, CancellationToken ct)
    {
        _logger.LogInformation("FetchDeductions [{Source}]: opening tunnel to {DbHost}/{Database}",
            source, cfg.DbHost, cfg.Database);

        using var tunnel = await SshTunnelService.OpenAsync(cfg, _logger);
        var rows = new List<DeductionRow>();

        const string sql = @"
            SELECT e.employeenumber,
                   d.referencecode,
                   d.deductiontype,
                   d.installmentamount,
                   d.datecreated
            FROM deductions d
            INNER JOIN employees e ON d.employeeid = e.id
            WHERE d.deductiontype = @code
              AND d.status = 'reserved'
              AND d.is_bank_res = 'Y'
            ORDER BY d.datecreated";

        using var cmd = new MySqlCommand(sql, tunnel.Connection);
        cmd.Parameters.AddWithValue("@code", deductionCode);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var amountRaw = reader.GetValue(reader.GetOrdinal("installmentamount"));
            var amount = amountRaw == DBNull.Value
                ? 0m
                : decimal.TryParse(amountRaw.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                    ? parsed : 0m;

            var empRaw = reader.GetString("employeenumber");
            if (!long.TryParse(empRaw, out var empNumber))
                _logger.LogWarning("FetchDeductions [{Source}]: could not parse employee number '{Raw}' — skipping row",
                    source, empRaw);

            rows.Add(new DeductionRow(
                EmployeeNumber:    empNumber,
                ReferenceCode:     reader.GetString("referencecode"),
                DeductionType:     reader.GetString("deductiontype"),
                InstallmentAmount: amount,
                DateCreated:       reader.GetDateTime("datecreated"),
                Source:            source));
        }

        _logger.LogInformation("FetchDeductions [{Source}]: fetched {Count} rows for code {DeductionCode}",
            source, rows.Count, deductionCode);

        return rows;
    }

    /// <summary>
    /// Returns all companies of a given type from the IPPS companies table.
    /// Used to populate the lender creation form before saving.
    /// </summary>
    public async Task<List<CompanyRow>> FetchCompaniesByTypeAsync(
        string companyType, CancellationToken ct = default)
    {
        _logger.LogInformation("FetchCompaniesByType [{CompanyType}]: opening tunnel to {DbHost}/{Database}",
            companyType, IppsSettings.DbHost, IppsSettings.Database);

        using var tunnel = await SshTunnelService.OpenAsync(IppsSettings, _logger);
        var rows = new List<CompanyRow>();

        const string sql = @"
            SELECT id, companyname, deductiontype
            FROM companies
            WHERE companytype = @type
            ORDER BY companyname";

        using var cmd = new MySqlCommand(sql, tunnel.Connection);
        cmd.Parameters.AddWithValue("@type", companyType);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new CompanyRow(
                reader.GetInt32("id"),
                reader.GetString("companyname"),
                reader.GetString("deductiontype")));
        }
        
        _logger.LogInformation("FetchCompaniesByType [{CompanyType}]: fetched {Count} rows", companyType, rows.Count);
        return rows;
    }

    /// <summary>
    /// Returns the deduction code for a lender during an invoice run.
    /// Uses the lender's stored DeductionCode — no DB lookup needed at run time.
    /// Kept for cases where a direct lookup is required.
    /// </summary>
    public static async Task<string> LookupDeductionCodeAsync(
        ExternalDbSettings cfg, string companyType, CancellationToken ct = default)
    {
        using var tunnel = await SshTunnelService.OpenAsync(cfg);

        const string sql = "SELECT deductiontype FROM companies WHERE companytype = @type LIMIT 1";
        using var cmd = new MySqlCommand(sql, tunnel.Connection);
        cmd.Parameters.AddWithValue("@type", companyType);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result?.ToString() ?? throw new InvalidOperationException(
            $"No company found with companytype '{companyType}'.");
    }

    public (List<DeductionRow> IppsSheet, List<DeductionRow> HcmSheet) SplitRows(
        List<DeductionRow> ippsRows, List<DeductionRow> hcmRows, string refFilePath)
    {
        _logger.LogInformation("SplitRows: reading HCM ref file {RefFilePath}", refFilePath);
        var refNumbers = ReadHcmRefNumbers(refFilePath);
        _logger.LogInformation("SplitRows: ref file contains {RefCount} IPPS numbers", refNumbers.Count);

        // listA (HCM DB) in ref → HCM sheet
        var trueHcm    = hcmRows.Where(r =>  refNumbers.Contains(r.EmployeeNumber)).ToList();
        // listB (IPPS DB) in ref → dropped
        var droppedIpps = ippsRows.Count(r => refNumbers.Contains(r.EmployeeNumber));

        // listA (HCM DB)  not in ref → listD
        var listD_hcm  = hcmRows.Where(r =>  !refNumbers.Contains(r.EmployeeNumber)).ToList();
        // listB (IPPS DB) not in ref → listD
        var listD_ipps = ippsRows.Where(r => !refNumbers.Contains(r.EmployeeNumber)).ToList();

        _logger.LogInformation(
            "SplitRows: trueHCM={TrueHcm}, droppedIPPS={DroppedIpps}, listD_hcm={DHcm}, listD_ipps={DIpps}",
            trueHcm.Count, droppedIpps, listD_hcm.Count, listD_ipps.Count);

        // HCM sheet: HCM DB records matched to ref, deduped by (EmployeeNumber, ReferenceCode)
        var hcmSheet = trueHcm
            .GroupBy(r => (r.EmployeeNumber, r.ReferenceCode))
            .Select(g => g.OrderByDescending(r => r.InstallmentAmount).First())
            .OrderBy(r => r.EmployeeNumber)
            .ToList();

        // IPPS sheet: listD deduped by (EmployeeNumber, ReferenceCode) — same loan in both
        // sources keeps the higher amount; different loans for same employee are all kept
        var ippsSheet = listD_hcm.Concat(listD_ipps)
            .GroupBy(r => (r.EmployeeNumber, r.ReferenceCode))
            .Select(g => g.OrderByDescending(r => r.InstallmentAmount).First())
            .OrderBy(r => r.EmployeeNumber)
            .ToList();

        _logger.LogInformation(
            "SplitRows: final IPPS sheet={IppsSheet} rows, HCM sheet={HcmSheet} rows",
            ippsSheet.Count, hcmSheet.Count);

        return (ippsSheet, hcmSheet);
    }

    private static HashSet<long> ReadHcmRefNumbers(string filePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var result = new HashSet<long>();
        using var pkg = new ExcelPackage(new FileInfo(filePath));
        var ws = pkg.Workbook.Worksheets.FirstOrDefault();
        if (ws == null) return result;

        int rows = ws.Dimension?.Rows ?? 0;
        for (int r = 1; r <= rows; r++)
        {
            var cell = ws.Cells[r, 1];
            // Numeric cells: EPPlus stores as double; .Text may render as scientific notation
            if (cell.Value is double d)
            {
                result.Add((long)d);
                continue;
            }
            // Text cells: padded strings like "000000000123456"
            var raw = cell.Text?.Trim();
            if (!string.IsNullOrEmpty(raw) && long.TryParse(raw, out var num))
                result.Add(num);
        }
        return result;
    }

    public static string BuildExcel(List<DeductionRow> rows, string lenderName, string storageRoot,
        List<DeductionRow>? hcmRows = null)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var now = DateTime.UtcNow;
        var monthYear = now.ToString("MMMMyyyy");
        var fileName  = $"{SanitizeFileName(lenderName)}_Invoice_Breakdown_{monthYear}.xlsx";

        var dir = Path.Combine(storageRoot, "invoices", now.Year.ToString(), now.ToString("MM"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, fileName);

        using var pkg = new ExcelPackage();

        WriteSheet(pkg, "IPPS", rows);
        if (hcmRows != null)
            WriteSheet(pkg, "HCM", hcmRows);

        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    private static void WriteSheet(ExcelPackage pkg, string sheetName, List<DeductionRow> rows)
    {
        var ws = pkg.Workbook.Worksheets.Add(sheetName);

        string[] headers = { "IPPS", "RefCode", "DedCode", "Amount", "DateCreated" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 78, 121));
            cell.Style.Font.Color.SetColor(Color.White);
        }

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            ws.Cells[r + 2, 1].Value = row.EmployeeNumber;
            ws.Cells[r + 2, 1].Style.Numberformat.Format = "0";
            ws.Cells[r + 2, 2].Value = row.ReferenceCode;
            ws.Cells[r + 2, 3].Value = row.DeductionType;
            ws.Cells[r + 2, 4].Value = row.InstallmentAmount;
            ws.Cells[r + 2, 4].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[r + 2, 5].Value = row.DateCreated;
            ws.Cells[r + 2, 5].Style.Numberformat.Format = "dd-mmm-yyyy";
        }

        if (ws.Dimension != null)
        {
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            ws.View.FreezePanes(2, 1);
        }
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
}
