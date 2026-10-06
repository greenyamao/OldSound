using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OldSound.Gui;

public partial class App : Application
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int NativeMessageBox(IntPtr hWnd, string text, string caption, uint type);

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                LogAndShowException(ex);
            else
                LogAndShowException(new Exception($"Unhandled error object: {e.ExceptionObject}"));
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogAndShowException(e.Exception);
            e.SetObserved();
        };

        DispatcherUnhandledException += (s, e) =>
        {
            LogAndShowException(e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            LogAndShowException(ex);
            Shutdown(1);
        }
    }

    private static void LogAndShowException(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"OldSound Fatal Error - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"OS: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
        sb.AppendLine($"CLR: {Environment.Version}");
        sb.AppendLine();

        var cur = ex;
        while (cur != null)
        {
            sb.AppendLine("=== EXCEPTION ===");
            sb.AppendLine(cur.GetType().FullName);
            sb.AppendLine(cur.Message);
            sb.AppendLine(cur.StackTrace);
            cur = cur.InnerException;
        }

        string crashText = sb.ToString();

        // 1. Write to %LOCALAPPDATA%\OldSound\crash.log
        try
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OldSound");
            Directory.CreateDirectory(appData);
            File.WriteAllText(Path.Combine(appData, "crash.log"), crashText);
        }
        catch { }

        // 2. Write next to executable
        try
        {
            string localLog = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.WriteAllText(localLog, crashText);
        }
        catch { }

        // 3. Show dialog to user: try WPF MessageBox first, fallback to Win32 NativeMessageBox
        try
        {
            MessageBox.Show(
                crashText,
                "OldSound Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            NativeMessageBox(IntPtr.Zero, crashText, "OldSound Fatal Error", 0x00000010 /* MB_ICONERROR */);
        }
    }
}
