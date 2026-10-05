using System.Windows;

namespace OldSound.Gui;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show(
                $"Критическая ошибка приложения:\n\n{e.Exception.Message}\n\nСтек вызова:\n{e.Exception.StackTrace}",
                "OldSound Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}
