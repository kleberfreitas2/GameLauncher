using System.Diagnostics;
using System.IO;

namespace GameLauncher.Services;

/// <summary>
/// Monitora o processo real de um jogo, mesmo quando ele é iniciado
/// por um launcher intermediário (Steam, Xbox, Epic) que sai rapidamente.
/// </summary>
public static class GameProcessMonitor
{
    /// <summary>
    /// Aguarda o jogo encerrar e retorna o tempo total jogado em minutos.
    /// </summary>
    /// <param name="executablePath">Caminho completo do executável do jogo.</param>
    /// <param name="stubProcess">Processo stub retornado pelo Process.Start (pode ser null).</param>
    /// <param name="timeout">Tempo máximo de espera total (padrão: 6 horas).</param>
    public static async Task<double> WaitForGameExitAsync(
        string executablePath,
        Process? stubProcess,
        TimeSpan? timeout = null)
    {
        var totalTimeout = timeout ?? TimeSpan.FromHours(6);
        var started = DateTime.UtcNow;
        var processName = Path.GetFileNameWithoutExtension(executablePath);

        // 1. Aguarda o stub sair (se existir)
        if (stubProcess is not null)
        {
            try
            {
                await Task.Run(() =>
                {
                    try { stubProcess.WaitForExit(5_000); } catch { }
                });
            }
            catch { }
        }

        // 2. Tenta localizar o processo real pelo nome do exe
        //    Aguarda até 30 s para ele aparecer (alguns jogos demoram para iniciar)
        Process? gameProcess = null;
        var findDeadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < findDeadline)
        {
            gameProcess = FindProcess(processName);
            if (gameProcess is not null) break;
            await Task.Delay(1_000);
        }

        if (gameProcess is not null)
        {
            // 3. Processo encontrado — aguarda ele encerrar
            try
            {
                await Task.Run(() =>
                {
                    try { gameProcess.WaitForExit(); } catch { }
                });
            }
            catch { }
        }
        else
        {
            // 4. Processo não encontrado — fallback: aguarda 10 s e volta
            await Task.Delay(10_000);
        }

        return (DateTime.UtcNow - started).TotalMinutes;
    }

    private static Process? FindProcess(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            return processes.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
