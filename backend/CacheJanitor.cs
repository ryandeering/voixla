using Microsoft.Extensions.Options;

namespace Voixla.Api;

public sealed class CacheJanitor : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private const int SidecarRetentionMultiplier = 8;

    private readonly PiperOptions _opts;
    private readonly ILogger<CacheJanitor> _log;

    public CacheJanitor(IOptions<PiperOptions> opts, ILogger<CacheJanitor> log)
    {
        _opts = opts.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_opts.CacheTtlMinutes <= 0)
        {
            return;
        }

        var cacheDir = Path.GetFullPath(_opts.CacheDir);
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            Sweep(cacheDir);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private void Sweep(string cacheDir)
    {
        try
        {
            if (!Directory.Exists(cacheDir))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var wavCutoff = now.AddMinutes(-_opts.CacheTtlMinutes);
            var sidecarCutoff = now.AddMinutes(-_opts.CacheTtlMinutes * SidecarRetentionMultiplier);
            foreach (var path in Directory.EnumerateFiles(cacheDir))
            {
                try
                {
                    var cutoff = path.EndsWith(".wav", StringComparison.Ordinal) ? wavCutoff : sidecarCutoff;
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cache sweep failed");
        }
    }
}
