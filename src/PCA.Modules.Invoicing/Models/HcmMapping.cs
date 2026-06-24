namespace PCA.Modules.Invoicing.Models;

public class HcmMapping
{
    public int     Id             { get; set; }
    public string  RawValue       { get; set; } = string.Empty;
    public string? CanonicalName  { get; set; }
    public string  Classification { get; set; } = string.Empty; // BASIC_SALARY|ALLOWANCE|STATUTORY|DEDUCTION|IGNORE
    public string  SourceColumn   { get; set; } = string.Empty; // COSTITEM|VENDOR_NAME
    public string? Aliases        { get; set; }                 // comma-separated alternate raw values
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;

    public static class Classifications
    {
        public const string BasicSalary = "BASIC_SALARY";
        public const string Allowance   = "ALLOWANCE";
        public const string Statutory   = "STATUTORY";
        public const string Deduction   = "DEDUCTION";
        public const string Ignore      = "IGNORE";

        public static readonly string[] All =
            { BasicSalary, Allowance, Statutory, Deduction, Ignore };
    }

    public static class SourceColumns
    {
        public const string CostItem   = "COSTITEM";
        public const string VendorName = "VENDOR_NAME";
    }
}
