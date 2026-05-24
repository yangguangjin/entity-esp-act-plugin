namespace EntityEspActPlugin.Core.Models;

public sealed class AbilityVfxCandidate
{
    public string AbilityId { get; set; } = string.Empty;
    public string AbilityName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public double Score { get; set; }
    public int RawScore { get; set; }
    public int SeenInFiles { get; set; }
    public string Classification { get; set; } = string.Empty;
    public string Status { get; set; } = "candidate";
}
