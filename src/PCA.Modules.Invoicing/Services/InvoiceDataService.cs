using MySqlConnector;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace PCA.Modules.Invoicing.Services;

public record DeductionRow(
    string EmployeeNumber,
    string ReferenceCode,
    string DeductionType,
    decimal InstallmentAmount,
    DateTime DateCreated,
    string Source);

public record CompanyRow(int Id, string CompanyName, string DeductionType);

public class InvoiceDataService
{
    public ExternalDbSettings IppsSettings { get; }
    private readonly ExternalDbSettings _hcmSettings;

    public InvoiceDataService(ExternalDbSettings ippsSettings, ExternalDbSettings hcmSettings)
    {
        IppsSettings = ippsSettings;
        _hcmSettings = hcmSettings;
    }

    public async Task<(List<DeductionRow> Rows, int IppsCount, int HcmCount)> FetchMergedDataAsync(
        string deductionCode, CancellationToken ct = default)
    {
        var ippsRows = await FetchDeductionsAsync(IppsSettings, deductionCode, "IPPS", ct);
        var hcmRows  = await FetchDeductionsAsync(_hcmSettings, deductionCode, "HCM", ct);

        var merged = ippsRows.Concat(hcmRows)
            .GroupBy(r => (r.EmployeeNumber, r.ReferenceCode))
            .Select(g => g.OrderByDescending(r => r.InstallmentAmount).First())
            .OrderBy(r => r.EmployeeNumber)
            .ThenByDescending(r => r.InstallmentAmount)
            .ToList();

        return (merged, ippsRows.Count, hcmRows.Count);
    }

    private static async Task<List<DeductionRow>> FetchDeductionsAsync(
        ExternalDbSettings cfg, string deductionCode, string source, CancellationToken ct)
    {
        using var tunnel = await SshTunnelService.OpenAsync(cfg);
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

            rows.Add(new DeductionRow(
                EmployeeNumber:    reader.GetString("employeenumber"),
                ReferenceCode:     reader.GetString("referencecode"),
                DeductionType:     reader.GetString("deductiontype"),
                InstallmentAmount: amount,
                DateCreated:       reader.GetDateTime("datecreated"),
                Source:            source));
        }

        return rows;
    }

    /// <summary>
    /// Returns all companies of a given type from the IPPS companies table.
    /// Used to populate the lender creation form before saving.
    /// </summary>
    public async Task<List<CompanyRow>> FetchCompaniesByTypeAsync(
        string companyType, CancellationToken ct = default)
    {
        using var tunnel = await SshTunnelService.OpenAsync(IppsSettings);
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

    public static string BuildExcel(List<DeductionRow> rows, string lenderName, string storageRoot)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var now = DateTime.UtcNow;
        var monthFolder = now.ToString("yyyy-MM");
        var monthYear   = now.ToString("MMMMyyyy");           // e.g. June2026
        var fileName    = $"{SanitizeFileName(lenderName)}_Invoice_Breakdown_{monthYear}.xlsx";

        var dir = Path.Combine(storageRoot, "invoices", monthFolder);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, fileName);

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Invoice");

        // Headers
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

        // Data
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            ws.Cells[r + 2, 1].Value = row.EmployeeNumber;
            ws.Cells[r + 2, 2].Value = row.ReferenceCode;
            ws.Cells[r + 2, 3].Value = row.DeductionType;
            ws.Cells[r + 2, 4].Value = row.InstallmentAmount;
            ws.Cells[r + 2, 4].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[r + 2, 5].Value = row.DateCreated;
            ws.Cells[r + 2, 5].Style.Numberformat.Format = "dd-mmm-yyyy";
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        ws.View.FreezePanes(2, 1);

        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
}
