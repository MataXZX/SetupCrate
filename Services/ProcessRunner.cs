using System.Diagnostics;
using System.IO;

namespace SetupCrate.Services;

public static class ProcessRunner
{
    /// <summary>Запускает установщик и ждёт завершения. Пустые args = обычный интерактивный запуск.</summary>
    public static async Task<int> RunAsync(string filePath, string args, CancellationToken ct)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        ProcessStartInfo psi;
        if (ext == ".msi" && !string.IsNullOrWhiteSpace(args))
        {
            psi = new ProcessStartInfo("msiexec.exe")
            {
                UseShellExecute = true,
                Arguments = $"/i \"{filePath}\" {args}"
            };
        }
        else
        {
            psi = new ProcessStartInfo(filePath)
            {
                UseShellExecute = true,
                Arguments = args ?? ""
            };
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить установщик.");
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        try { return process.ExitCode; }
        catch { return 0; }
    }
}
