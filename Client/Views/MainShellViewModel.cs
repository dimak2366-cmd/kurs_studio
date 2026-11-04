using System.Collections.ObjectModel;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Client.Views.Accountant;
using Client.Views.Admin;
using Client.Views.Director;
using Client.Views.Economist;
using Client.Views.Head;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Views;

public sealed class MainShellViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ApiClient _api;
    private readonly IServiceProvider _serviceProvider;

    private MenuEntry? _selectedEntry;
    private object? _currentView;

    public MainShellViewModel(SessionService session, ApiClient api, IServiceProvider serviceProvider)
    {
        _session = session;
        _api = api;
        _serviceProvider = serviceProvider;

        MenuItems = new ObservableCollection<MenuEntry>(BuildMenu(session.RoleName));
        SelectedEntry = MenuItems.FirstOrDefault();
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
    }

    public ObservableCollection<MenuEntry> MenuItems { get; }
    public string Header => $"{_session.FullName} ({_session.RoleName})";

    public MenuEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value) && value != null)
            {
                CurrentView = SafeResolveView(_session.RoleName, value.Title);
            }
        }
    }

    public object? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public ICommand LogoutCommand { get; }
    public event EventHandler? LogoutRequested;

    private object SafeResolveView(string role, string title)
    {
        try
        {
            return ResolveView(role, title) ?? new PlaceholderViewModel(title, "Экран пуст.");
        }
        catch (Exception ex)
        {
            // Показываем заглушку с текстом ошибки вместо падения приложения.
            return new PlaceholderViewModel(
                title,
                "Не удалось открыть экран. Причина: " + ex.Message);
        }
    }

    private object? ResolveView(string role, string title)
    {
        switch (title)
        {
            case "Пользователи":    return _serviceProvider.GetRequiredService<UsersViewModel>();
            case "Отделы":          return _serviceProvider.GetRequiredService<DepartmentsViewModel>();
            case "Проекты":         return _serviceProvider.GetRequiredService<ProjectsViewModel>();
            case "Справочники":     return _serviceProvider.GetRequiredService<CatalogViewModel>();
            case "Журналы":         return _serviceProvider.GetRequiredService<LogsViewModel>();
            case "Резервные копии": return _serviceProvider.GetRequiredService<BackupViewModel>();
            case "Настройки":       return _serviceProvider.GetRequiredService<SettingsViewModel>();

            case "Лимиты": return _serviceProvider.GetRequiredService<LimitsViewModel>();
            case "Анализ": return _serviceProvider.GetRequiredService<AnalysisViewModel>();
        }

        if (role == "Главный экономист")
        {
            switch (title)
            {
                case "Сметы":  return _serviceProvider.GetRequiredService<SmetasViewModel>();
                case "Заявки": return _serviceProvider.GetRequiredService<RequestsReviewViewModel>();
                case "Отчёты": return _serviceProvider.GetRequiredService<ReportsViewModel>();
            }
        }
        else if (role == "Директор")
        {
            switch (title)
            {
                case "Сводный бюджет":   return _serviceProvider.GetRequiredService<OverviewViewModel>();
                case "Утверждение смет": return _serviceProvider.GetRequiredService<SmetaApprovalViewModel>();
                case "Отчёты":           return _serviceProvider.GetRequiredService<ReportsApprovalViewModel>();
                case "Решения":          return _serviceProvider.GetRequiredService<DecisionsViewModel>();
            }
        }
        else if (role == "Руководитель отдела")
        {
            switch (title)
            {
                case "Мои сметы":   return _serviceProvider.GetRequiredService<HeadSmetasViewModel>();
                case "Мои расходы": return _serviceProvider.GetRequiredService<HeadExpensesViewModel>();
                case "Заявки":      return _serviceProvider.GetRequiredService<HeadRequestsViewModel>();
                case "Отчёты":      return _serviceProvider.GetRequiredService<HeadReportsViewModel>();
            }
        }
        else if (role == "Бухгалтер")
        {
            switch (title)
            {
                case "Ввод расходов": return _serviceProvider.GetRequiredService<ExpensesViewModel>();
                case "Реестр":        return _serviceProvider.GetRequiredService<ExpensesViewModel>();
                case "Периоды":       return _serviceProvider.GetRequiredService<PeriodsViewModel>();
            }
        }

        return new PlaceholderViewModel(title, "Экран не реализован.");
    }

    private async Task LogoutAsync()
    {
        try { await _api.LogoutAsync(); } catch { /* не критично */ }
        _session.Clear();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private static IEnumerable<MenuEntry> BuildMenu(string role) => role switch
    {
        "Администратор" => new[]
        {
            new MenuEntry("Пользователи",    "Управление учётными записями, сброс пароля, блокировка."),
            new MenuEntry("Отделы",          "Справочник подразделений института."),
            new MenuEntry("Проекты",         "Справочник научно-исследовательских проектов."),
            new MenuEntry("Справочники",     "Статьи расходов и формы смет."),
            new MenuEntry("Журналы",         "Журнал действий и журнал ошибок."),
            new MenuEntry("Резервные копии", "Создание и восстановление резервных копий базы данных."),
            new MenuEntry("Настройки",       "Параметры сервера и политики безопасности."),
        },
        "Директор" => new[]
        {
            new MenuEntry("Сводный бюджет",   "Обзор всех проектов и смет института."),
            new MenuEntry("Утверждение смет", "Сметы, ожидающие утверждения."),
            new MenuEntry("Лимиты",           "Лимиты бюджетных обязательств и контроль превышений."),
            new MenuEntry("Анализ",           "План-фактный и факторный анализ."),
            new MenuEntry("Отчёты",           "Аналитические отчёты, ожидающие утверждения."),
            new MenuEntry("Решения",          "Управленческие решения по статьям и периодам."),
        },
        "Главный экономист" => new[]
        {
            new MenuEntry("Сметы",   "Создание, редактирование и согласование смет."),
            new MenuEntry("Лимиты",  "Формирование лимитов бюджетных обязательств."),
            new MenuEntry("Анализ",  "Факторный анализ отклонений по статьям."),
            new MenuEntry("Заявки",  "Заявки руководителей отделов."),
            new MenuEntry("Отчёты",  "Формирование аналитических отчётов."),
        },
        "Руководитель отдела" => new[]
        {
            new MenuEntry("Мои сметы",   "Сметы проектов вашего отдела."),
            new MenuEntry("Мои расходы", "Фактические расходы по проектам отдела."),
            new MenuEntry("Анализ",      "Отклонения и факторный анализ по отделу."),
            new MenuEntry("Заявки",      "Подача и отслеживание заявок на расход."),
            new MenuEntry("Отчёты",      "Утверждённые аналитические отчёты."),
        },
        "Бухгалтер" => new[]
        {
            new MenuEntry("Ввод расходов", "Ввод фактических расходов с привязкой к документам."),
            new MenuEntry("Реестр",        "Реестр фактических расходов с фильтрами."),
            new MenuEntry("Периоды",       "Открытие/закрытие отчётных периодов."),
        },
        _ => new[] { new MenuEntry("Главная", "Добро пожаловать в систему.") }
    };
}

public sealed class MenuEntry
{
    public MenuEntry(string title, string description)
    {
        Title = title;
        Description = description;
    }
    public string Title { get; }
    public string Description { get; }
}
