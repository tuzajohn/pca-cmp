namespace PCA.Modules.AccessManagement.Models;

public class AccessSequence
{
    public int Id { get; set; }
    public string Prefix { get; set; } = string.Empty;  // AR, SRA, DR
    public int Year { get; set; }
    public int Month { get; set; }
    public int LastSequence { get; set; }
}
