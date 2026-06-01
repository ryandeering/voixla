using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Voixla.Api.Dtos;

namespace Voixla.Api;

public sealed class PiperService
{
    private readonly PiperOptions _opts;
    private readonly ILogger<PiperService> _log;
    private readonly SemaphoreSlim _concurrency;

    private const int LockStripes = 32;
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, LockStripes).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private int _evicting;

    public PiperService(IOptions<PiperOptions> opts, ILogger<PiperService> log)
    {
        _opts = opts.Value;
        _log = log;
        _concurrency = new SemaphoreSlim(Math.Max(1, _opts.MaxConcurrency));
        _opts.CacheDir = Path.GetFullPath(_opts.CacheDir);
        _opts.VoicesDir = Path.GetFullPath(_opts.VoicesDir);
        Directory.CreateDirectory(_opts.CacheDir);
        Directory.CreateDirectory(_opts.VoicesDir);
    }

    public static string HashChunk(string voice, string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(voice + "|" + text));
        return Convert.ToHexStringLower(bytes)[..32];
    }

    private SemaphoreSlim LockFor(string hash) => _locks[(int)((uint)hash.GetHashCode() % LockStripes)];

    public IReadOnlyList<VoiceInfo> ListVoices()
    {
        if (!Directory.Exists(_opts.VoicesDir))
        {
            return [];
        }

        var voices = new List<VoiceInfo>();
        foreach (var modelPath in Directory.EnumerateFiles(_opts.VoicesDir, "*.onnx", SearchOption.AllDirectories))
        {
            var id = Path.GetFileNameWithoutExtension(modelPath);
            var (language, quality) = ReadVoiceConfig(modelPath + ".json", id);
            voices.Add(new VoiceInfo(id, PrettifyName(id), language, quality));
        }
        return voices.OrderBy(v => v.Language).ThenBy(v => v.Id).ToList();
    }

    public bool IsValidVoice(string voice) => IsSafeVoiceId(voice) && File.Exists(Path.Combine(_opts.VoicesDir, voice + ".onnx"));

    public async Task<string> RegisterChunkAsync(string voice, string text, CancellationToken ct)
    {
        var hash = HashChunk(voice, text);
        var reqPath = Path.Combine(_opts.CacheDir, hash + ".req.json");
        if (File.Exists(reqPath))
        {
            File.SetLastWriteTimeUtc(reqPath, DateTime.UtcNow);
        }
        else
        {
            var json = JsonSerializer.Serialize(new ChunkRequest(voice, text));
            var tmpPath = reqPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
            await File.WriteAllTextAsync(tmpPath, json, ct);
            File.Move(tmpPath, reqPath, overwrite: true);
        }
        return hash;
    }

    public async Task<string?> GetOrSynthesizeAsync(string hash, CancellationToken ct)
    {
        if (!IsSafeHash(hash))
        {
            return null;
        }

        var wavPath = Path.Combine(_opts.CacheDir, hash + ".wav");
        if (File.Exists(wavPath))
        {
            return wavPath;
        }

        var reqPath = Path.Combine(_opts.CacheDir, hash + ".req.json");
        if (!File.Exists(reqPath))
        {
            return null;
        }

        var req = JsonSerializer.Deserialize<ChunkRequest>(await File.ReadAllTextAsync(reqPath, ct));
        if (req is null)
        {
            return null;
        }

        var gate = LockFor(hash);
        await gate.WaitAsync(ct);
        try
        {
            if (File.Exists(wavPath))
            {
                return wavPath;
            }

            await SynthesizeAsync(req.Voice, req.Text, wavPath, ct);
            EnforceCacheBudget();
            return File.Exists(wavPath) ? wavPath : null;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task SynthesizeAsync(string voice, string text, string wavPath, CancellationToken ct)
    {
        if (!IsSafeVoiceId(voice))
        {
            throw new FileNotFoundException($"Invalid voice id: {voice}");
        }

        var modelPath = Path.Combine(_opts.VoicesDir, voice + ".onnx");
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Voice model not found: {voice}", modelPath);
        }

        await _concurrency.WaitAsync(ct);
        var sw = Stopwatch.StartNew();
        var tmpPath = wavPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _opts.ExecutablePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(modelPath);
            psi.ArgumentList.Add("--output_file");
            psi.ArgumentList.Add(tmpPath);

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start piper process.");

            try
            {
                var stderrTask = proc.StandardError.ReadToEndAsync(ct);
                var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);

                await proc.StandardInput.WriteLineAsync(text.AsMemory(), ct);
                proc.StandardInput.Close();

                await proc.WaitForExitAsync(ct);
                var stderr = await stderrTask;
                await stdoutTask;

                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException($"piper exited {proc.ExitCode}: {stderr}");
                }

                File.Move(tmpPath, wavPath, overwrite: true);
                sw.Stop();
                _log.LogInformation("Synthesized {Chars} chars with {Voice} in {Ms}ms",
                    text.Length, voice, sw.ElapsedMilliseconds);
            }
            finally
            {
                if (!proc.HasExited)
                {
                    try
                    {
                        proc.Kill(entireProcessTree: true);
                        using var killCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await proc.WaitForExitAsync(killCts.Token);
                    }
                    catch
                    {
                    }
                }
            }
        }
        finally
        {
            _concurrency.Release();
            if (File.Exists(tmpPath))
            {
                TryDelete(tmpPath);
            }
        }
    }

    private (string Language, string Quality) ReadVoiceConfig(string configPath, string id)
    {
        try
        {
            if (File.Exists(configPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                var root = doc.RootElement;
                var language = root.TryGetProperty("language", out var lang)
                    && lang.TryGetProperty("code", out var code)
                    ? code.GetString() ?? "?"
                    : "?";
                var quality = root.TryGetProperty("audio", out var audio)
                    && audio.TryGetProperty("quality", out var q)
                    ? q.GetString() ?? GuessQuality(id)
                    : GuessQuality(id);
                return (language, quality);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not parse voice config {Path}", configPath);
        }
        return (GuessLanguage(id), GuessQuality(id));
    }

    private static string GuessLanguage(string id) =>
        id.Contains('-') ? id[..id.IndexOf('-')] : "?";

    private static string GuessQuality(string id)
    {
        foreach (var q in new[] { "x_low", "low", "medium", "high" })
        {
            if (id.EndsWith("-" + q, StringComparison.OrdinalIgnoreCase))
            {
                return q;
            }
        }

        return "?";
    }

    private static string PrettifyName(string id)
    {
        var parts = id.Split('-');
        return parts.Length >= 2
            ? char.ToUpperInvariant(parts[1][0]) + parts[1][1..] + $" ({id})"
            : id;
    }

    private static bool IsSafeHash(string hash) =>
        hash.Length is > 0 and <= 64 && hash.All(c => char.IsAsciiHexDigit(c));

    private static bool IsSafeVoiceId(string voice) =>
        !string.IsNullOrEmpty(voice) && voice.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private void EnforceCacheBudget()
    {
        if (_opts.CacheMaxMb <= 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref _evicting, 1) == 1)
        {
            return;
        }

        try
        {
            var maxBytes = (long)_opts.CacheMaxMb * 1024 * 1024;
            var wavs = new DirectoryInfo(_opts.CacheDir).EnumerateFiles("*.wav", SearchOption.TopDirectoryOnly).ToList();
            var total = wavs.Sum(f => f.Length);
            if (total <= maxBytes)
            {
                return;
            }

            var target = maxBytes * 9 / 10;
            foreach (var file in wavs.OrderBy(f => f.LastWriteTimeUtc))
            {
                if (total <= target)
                {
                    break;
                }

                try
                {
                    total -= file.Length;
                    file.Delete();
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cache eviction failed");
        }
        finally
        {
            Interlocked.Exchange(ref _evicting, 0);
        }
    }

    private sealed record ChunkRequest(string Voice, string Text);
}
