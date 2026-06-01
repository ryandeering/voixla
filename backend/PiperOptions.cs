namespace Voixla.Api;

public sealed class PiperOptions
{
    public const string SectionName = "Piper";

    public string ExecutablePath { get; set; } = "piper";

    public string VoicesDir { get; set; } = "voices";

    public string CacheDir { get; set; } = "cache";

    public int MaxConcurrency { get; set; } = 2;

    public int MaxChunkChars { get; set; } = 350;

    public int MaxTextChars { get; set; } = 100_000;

    public int CacheMaxMb { get; set; } = 500;

    public int CacheTtlMinutes { get; set; } = 30;
}
