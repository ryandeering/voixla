using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Voixla.Api;

public sealed class PiperServerHost(IOptions<PiperOptions> opts, ILogger<PiperServerHost> log) : BackgroundService
{
    private readonly PiperOptions _opts = opts.Value;
    private readonly ILogger<PiperServerHost> _log = log;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var script = string.IsNullOrWhiteSpace(_opts.ServerScriptPath)
            ? Path.Combine(AppContext.BaseDirectory, "piper_server.py")
            : Path.GetFullPath(_opts.ServerScriptPath);
        var voicesDir = Path.GetFullPath(_opts.VoicesDir);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var proc = Start(script, voicesDir);
            _log.LogInformation("piper server started (pid {Pid}) on {Host}:{Port}",
                proc.Id, _opts.ServerHost, _opts.ServerPort);

            try
            {
                await proc.WaitForExitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }

            if (stoppingToken.IsCancellationRequested)
            {
                Kill(proc);
                break;
            }

            _log.LogWarning("piper server exited (code {Code}); restarting", proc.ExitCode);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private Process Start(string script, string voicesDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _opts.PythonPath,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("--voices-dir");
        psi.ArgumentList.Add(voicesDir);
        psi.ArgumentList.Add("--host");
        psi.ArgumentList.Add(_opts.ServerHost);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(_opts.ServerPort.ToString());
        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start piper server.");
    }

    private static void Kill(Process proc)
    {
        try
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
