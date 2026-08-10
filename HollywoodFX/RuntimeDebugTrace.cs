using System;
using System.IO;
using BepInEx;

namespace HollywoodFX;

internal static class RuntimeDebugTrace
{
    private static readonly object Sync = new();

    private static bool _sessionStarted;
    private static bool _writeFailed;

    private static string ConfigDirectory => Path.Combine(BepInEx.Paths.BepInExRootPath, "config");

    public static string FilePath => Path.Combine(ConfigDirectory, "com.janky.hollywoodfx.runtime.log");

    public static void StartSession()
    {
        if (!Plugin.DebugLoggingEnabled)
            return;

        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(
                    FilePath,
                    $"{DateTime.Now:O} HollywoodFX runtime trace started.{Environment.NewLine}"
                );
                _sessionStarted = true;
                _writeFailed = false;
            }
        }
        catch (Exception exception)
        {
            ReportFailureOnce(exception);
        }
    }

    public static void Write(string message)
    {
        if (!Plugin.DebugLoggingEnabled)
            return;

        try
        {
            lock (Sync)
            {
                if (!_sessionStarted)
                    StartSession();

                File.AppendAllText(FilePath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception exception)
        {
            ReportFailureOnce(exception);
        }
    }

    private static void ReportFailureOnce(Exception exception)
    {
        if (_writeFailed)
            return;

        _writeFailed = true;
        Plugin.Log?.LogWarning($"HollywoodFX runtime trace failed: {exception.Message}");
    }
}
