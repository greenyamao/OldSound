using System;
using System.IO;
using System.Text;
using System.Windows;

namespace OldSound.Gui;

public partial class App : Application
{
    public App()
    {
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
        var cur = ex;
        while (cur != null)
        {
            sb.AppendLine("=== EXCEPTION ===");
            sb.AppendLine(cur.GetType().FullName);
            sb.AppendLine(cur.Message);
            sb.AppendLine(cur.StackTrace);
            cur = cur.InnerException;
        }

        try
        {
            File.WriteAllText(@"c:\all\programming\old_sound\crash.txt", sb.ToString());
        }
        catch { }

        MessageBox.Show(
            sb.ToString(),
            "OldSound Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
