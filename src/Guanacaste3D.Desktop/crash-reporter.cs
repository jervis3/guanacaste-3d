using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Keeps the program alive and the work safe when something goes wrong. An error on the interface thread is logged,
/// the project's recovery copy is written and the program carries on, because most such errors spoil one action, not the
/// session. An error that cannot be survived still gets its log and a last recovery copy before the process ends.
/// </summary>
internal static class CrashReporter
{
    /// <summary>Errors on the interface thread survived in one session before the program stops pretending nothing happened.</summary>
    private const int SurvivableErrors = 25;

    private static int _survived;
    private static MainWindow? _window;

    /// <summary>Where the error logs are written; redirectable for checks.</summary>
    public static string LogDirectory =>
        Environment.GetEnvironmentVariable("G3D_LOG_DIR") is { Length: > 0 } redirected
            ? redirected
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guanacaste 3D", "Logs");

    /// <summary>Tells the reporter which window's project to save when something fails.</summary>
    public static void Watch(MainWindow window) => _window = window;

    /// <summary>Hooks the three places an error can escape from: the interface thread, any thread, and forgotten tasks.</summary>
    public static void Install(Application application)
    {
        application.DispatcherUnhandledException += OnInterfaceError;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log("fatal error", e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString()));
            SaveWork();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("background task", e.Exception);
            e.SetObserved();
        };
    }

    private static void OnInterfaceError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("interface", e.Exception);
        SaveWork();
        if (Interlocked.Increment(ref _survived) > SurvivableErrors) return;
        e.Handled = true;
        _window?.ReportRecoveredError(LogDirectory);
    }

    /// <summary>Writes the project's recovery copy now, ignoring any failure: this runs while things are already failing.</summary>
    private static void SaveWork()
    {
        try { _window?.EmergencySave(); }
        catch (Exception exception) { Log("saving after an error", exception); }
    }

    /// <summary>Appends an error to the day's log. Logging never throws.</summary>
    public static void Log(string where, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var version = typeof(CrashReporter).Assembly.GetName().Version;
            File.AppendAllText(Path.Combine(LogDirectory, $"errors-{DateTime.Now:yyyyMMdd}.log"),
                $"[{DateTime.Now:O}] {where} (v{version})\n{exception}\n\n");
        }
        catch (Exception)
        {
            // Nowhere left to report to.
        }
    }
}
