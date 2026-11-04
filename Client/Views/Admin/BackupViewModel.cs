using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// VM экрана «Резервные копии». Просмотр списка, создание, восстановление.
/// Восстановление — опасная операция (перезаписывает текущее состояние БД),
/// поэтому требует явного подтверждения.
/// </summary>
public sealed class BackupViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<BackupFileDto> _backups = new();
    private BackupFileDto? _selectedBackup;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public BackupViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => SelectedBackup != null);
        _ = LoadAsync();
    }

    public ObservableCollection<BackupFileDto> Backups { get => _backups; private set => SetProperty(ref _backups, value); }

    public BackupFileDto? SelectedBackup
    {
        get => _selectedBackup;
        set { if (SetProperty(ref _selectedBackup, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand RestoreCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка списка копий...";
        try
        {
            var list = await _api.GetAsync<List<BackupFileDto>>("/api/backup/list");
            Backups = new ObservableCollection<BackupFileDto>(list);
            StatusMessage = $"Резервных копий: {Backups.Count}.";
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

    private async Task CreateAsync()
    {
        IsBusy = true;
        StatusMessage = "Создание резервной копии (pg_dump)...";
        try
        {
            var result = await _api.PostAsync<BackupResultDto>("/api/backup/create");
            if (result.Success && result.File != null)
            {
                StatusMessage = $"Копия создана: {result.File.FileName} ({result.File.SizeBytes / 1024 / 1024.0:F2} МБ).";
            }
            else
            {
                StatusMessage = "Копия создана, но сервер вернул пустые данные.";
            }
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка создания копии", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestoreAsync()
    {
        if (SelectedBackup == null) return;

        var confirm = MessageBox.Show(
            $"Восстановить БД из файла «{SelectedBackup.FileName}»?\n\n" +
            "ВНИМАНИЕ: текущее состояние базы данных будет перезаписано данными из копии.\n" +
            "Все изменения, сделанные после создания этой копии, будут потеряны.\n\n" +
            "Продолжить?",
            "Восстановление базы данных",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusMessage = "Восстановление из копии (pg_restore)...";
        try
        {
            await _api.PostAsync<BackupResultDto>("/api/backup/restore", new RestoreRequest { FileName = SelectedBackup.FileName });
            MessageBox.Show(
                "База данных восстановлена. Рекомендуется перезапустить сервер.",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            StatusMessage = "Восстановление выполнено.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка восстановления", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
