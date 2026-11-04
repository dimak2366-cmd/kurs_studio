using System.Collections.ObjectModel;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// VM экрана «Журналы»: журнал действий и журнал ошибок.
/// </summary>
public sealed class LogsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<AuditLogDto> _auditLogs = new();
    private ObservableCollection<ErrorLogDto> _errorLogs = new();

    private string _auditEntityFilter = string.Empty;
    private string _auditActionFilter = string.Empty;
    private string _errorLevelFilter = string.Empty;

    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public LogsViewModel(ApiClient api)
    {
        _api = api;
        LoadAuditCommand = new AsyncRelayCommand(LoadAuditAsync);
        LoadErrorsCommand = new AsyncRelayCommand(LoadErrorsAsync);
        LoadAllCommand = new AsyncRelayCommand(LoadAllAsync);
        _ = LoadAllAsync();
    }

    public ObservableCollection<AuditLogDto> AuditLogs { get => _auditLogs; private set => SetProperty(ref _auditLogs, value); }
    public ObservableCollection<ErrorLogDto> ErrorLogs { get => _errorLogs; private set => SetProperty(ref _errorLogs, value); }

    public string AuditEntityFilter { get => _auditEntityFilter; set => SetProperty(ref _auditEntityFilter, value); }
    public string AuditActionFilter { get => _auditActionFilter; set => SetProperty(ref _auditActionFilter, value); }
    public string ErrorLevelFilter { get => _errorLevelFilter; set => SetProperty(ref _errorLevelFilter, value); }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadAuditCommand { get; }
    public ICommand LoadErrorsCommand { get; }
    public ICommand LoadAllCommand { get; }

    private async Task LoadAllAsync()
    {
        await LoadAuditAsync();
        await LoadErrorsAsync();
    }

    private async Task LoadAuditAsync()
    {
        IsBusy = true;
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(AuditEntityFilter)) query.Add($"entity={Uri.EscapeDataString(AuditEntityFilter)}");
            if (!string.IsNullOrWhiteSpace(AuditActionFilter)) query.Add($"action={Uri.EscapeDataString(AuditActionFilter)}");
            var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

            var list = await _api.GetAsync<List<AuditLogDto>>("/api/logs/audit" + qs);
            AuditLogs = new ObservableCollection<AuditLogDto>(list);
            StatusMessage = $"Журнал действий: {AuditLogs.Count} записей.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка загрузки аудита: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadErrorsAsync()
    {
        IsBusy = true;
        try
        {
            var qs = !string.IsNullOrWhiteSpace(ErrorLevelFilter)
                ? "?level=" + Uri.EscapeDataString(ErrorLevelFilter)
                : string.Empty;

            var list = await _api.GetAsync<List<ErrorLogDto>>("/api/logs/errors" + qs);
            ErrorLogs = new ObservableCollection<ErrorLogDto>(list);
            StatusMessage = $"Журнал ошибок: {ErrorLogs.Count} записей.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка загрузки ошибок: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
