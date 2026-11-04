using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Client.Services;
using Client.ViewModels;
using Client.Views;
using Client.Views.Accountant;
using Client.Views.Admin;
using Client.Views.Director;
using Client.Views.Economist;
using Client.Views.Head;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Client;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Глобальный обработчик неперехваченных исключений UI-потока.
        // Показывает диалог с полным стектрейсом вместо молчаливого закрытия.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                args.Exception.ToString(),
                "Неперехваченное исключение",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Критическая ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        var serverUrl = configuration["ServerUrl"] ?? "http://localhost:5155";
        var timeoutSeconds = int.TryParse(configuration["HttpTimeoutSeconds"], out var t) ? t : 30;

        services.AddSingleton<SessionService>();
        services.AddSingleton(sp =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            return new ApiClient(http, sp.GetRequiredService<SessionService>(), serverUrl);
        });

        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<MainShellViewModel>();

        services.AddTransient<UsersViewModel>();
        services.AddTransient<DepartmentsViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<CatalogViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<SmetasViewModel>();
        services.AddTransient<LimitsViewModel>();
        services.AddTransient<AnalysisViewModel>();
        services.AddTransient<RequestsReviewViewModel>();
        services.AddTransient<ReportsViewModel>();

        services.AddTransient<OverviewViewModel>();
        services.AddTransient<SmetaApprovalViewModel>();
        services.AddTransient<ReportsApprovalViewModel>();
        services.AddTransient<DecisionsViewModel>();

        services.AddTransient<HeadSmetasViewModel>();
        services.AddTransient<HeadExpensesViewModel>();
        services.AddTransient<HeadRequestsViewModel>();
        services.AddTransient<HeadReportsViewModel>();

        services.AddTransient<ExpensesViewModel>();
        services.AddTransient<PeriodsViewModel>();

        Services = services.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
