using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using MudBlazor.Services;
using SqlServerBackup.Services;
using MudBlazor;

namespace SqlServerBackup;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();

#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
        });
        services.AddSingleton<ConfigService>();
        services.AddSingleton<DatabaseService>();
        Resources.Add("services", services.BuildServiceProvider());
    }
}
