using System.Diagnostics;
using System.Text.Json;

namespace DashboardBackend.Wearable.Services;

// Audio files live only in ignored local storage and are removed after each request.
public sealed class WearableSpeechService(string contentRoot)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<string?> TranscribeAsync(byte[] audio, CancellationToken cancellation)
    {
        string root = Path.Combine(contentRoot, "wearable", "local", "speech");
        string python = Path.Combine(root, "venv", "Scripts", "python.exe");
        string script = Path.Combine(contentRoot, "wearable", "speech", "transcribe.py");
        if (!File.Exists(python) || !File.Exists(script) || !Directory.Exists(Path.Combine(root, "models"))) return null;
        if (!await gate.WaitAsync(0, cancellation)) return null;
        string? path = null;
        try
        {
            string requests = Path.Combine(root, "requests");
            Directory.CreateDirectory(requests);
            path = Path.Combine(requests, Guid.NewGuid().ToString("N") + ".wav");
            await File.WriteAllBytesAsync(path, audio, cancellation);
            var start = new ProcessStartInfo(python)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(path);
            start.Environment["HF_HUB_OFFLINE"] = "1";
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
            var stderr = process.StandardError.ReadToEndAsync(cancellation);
            try
            {
                await process.WaitForExitAsync(cancellation);
                string result = await stdout;
                await stderr;
                if (process.ExitCode != 0 || result.Length > 32000) return null;
                using var parsed = JsonDocument.Parse(result);
                if (!parsed.RootElement.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String) return null;
                return text.GetString();
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
        }
        catch (Exception error) when (error is IOException or JsonException or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            if (path is not null && File.Exists(path)) File.Delete(path);
            gate.Release();
        }
    }
}
