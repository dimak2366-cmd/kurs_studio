using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// VM экрана «Настройки сервера». Показывает текущие значения из appsettings.json,
/// позволяет отредактировать 4 параметра безопасности.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private string _address = string.Empty;
    private int _port;
    private int _maxFailedAttempts;
    private int _lockoutMinutes;
    private int _sessionTimeoutMinutes;
    private string _backupPath = string.Empty;
    private string _pgDumpPath = string.Empty;
    private string _pgRestorePath = string.Empty;

    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ResetCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public string Address { get => _address; private set => SetProperty(ref _address, value); }
    public int Port { get => _port; private set => SetProperty(ref _port, value); }
    public string BackupPath { get => _backupPath; private set => SetProperty(ref _backupPath, value); }
    public string PgDumpPath { get => _pgDumpPath; private set => SetProperty(ref _pgDumpPath, value); }
    public string PgRestorePath { get => _pgRestorePath; private set => SetProperty(ref _pgRestorePath, value); }

    public int MaxFailedAttempts { get => _maxFailedAttempts; set => SetProperty(ref _maxFailedAttempts, value); }
    public int LockoutMinutes { get => _lockoutMinutes; set => SetProperty(ref _lockoutMinutes, value); }
    public int SessionTimeoutMinutes { get => _sessionTimeoutMinutes; set => SetProperty(ref _sessionTimeoutMinutes, value); }
    public string BackupPathEdit { get => _backupPath; set => SetProperty(ref _backupPath, value); }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка настроек...";
        try
        {
            var s = await _api.GetAsync<ServerSettingsDto>("/api/settings");
            Address = s.Address;
            Port = s.Port;
            MaxFailedAttempts = s.MaxFailedAttempts;
            LockoutMinutes = s.LockoutMinutes;
            SessionTimeoutMinutes = s.SessionTimeoutMinutes;
            BackupPath = s.BackupPath;
            PgDumpPath = s.PgDumpPath;
            PgRestorePath = s.PgRestorePath;
            StatusMessage = "Настройки загружены.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка загрузки: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = "Сохранение...";
        try
        {
            var request = new UpdateSettingsRequest
            {
                MaxFailedAttempts = MaxFailedAttempts,
                LockoutMinutes = LockoutMinutes,
                SessionTimeoutMinutes = SessionTimeoutMinutes,
                BackupPath = BackupPath
            };
            var updated = await _api.PutAsync<ServerSettingsDto>("/api/settings", request);

            MaxFailedAttempts = updated.MaxFailedAttempts;
            LockoutMinutes = updated.LockoutMinutes;
            SessionTimeoutMinutes = updated.SessionTimeoutMinutes;
            BackupPath = updated.BackupPath;

            MessageBox.Show(
                "Настройки сохранены. Изменения MaxFailedAttempts, LockoutMinutes и SessionTimeoutMinutes " +
                "применяются сразу. Адрес и порт изменятся после перезапуска сервера.",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);

            StatusMessage = "Настройки сохранены.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
