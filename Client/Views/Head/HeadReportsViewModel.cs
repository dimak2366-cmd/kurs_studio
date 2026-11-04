using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Microsoft.Win32;
using Shared.Dtos;

namespace Client.Views.Head;

/// <summary>
/// VM «Отчёты» для Руководителя отдела. Показывает утверждённые отчёты,
/// позволяет скачать CSV.
/// </summary>
public sealed class HeadReportsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<ReportDto> _reports = new();
    private ReportDto? _selectedReport;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public HeadReportsViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => SelectedReport != null);
        _ = LoadAsync();
    }

    public ObservableCollection<ReportDto> Reports { get => _reports; private set => SetProperty(ref _reports, value); }

    public ReportDto? SelectedReport
    {
        get => _selectedReport;
        set { if (SetProperty(ref _selectedReport, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand DownloadCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка отчётов...";
        try
        {
            var list = await _api.GetAsync<List<ReportDto>>("/api/reports");
            Reports = new ObservableCollection<ReportDto>(list.Where(r => r.Status == "Утверждён"));
            StatusMessage = $"Утверждённых отчётов: {Reports.Count}.";
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

    private async Task DownloadAsync()
    {
        if (SelectedReport == null) return;

        IsBusy = true;
        try
        {
            var detail = await _api.GetAsync<ReportDetailDto>($"/api/reports/{SelectedReport.Id}");
            var sfd = new SaveFileDialog
            {
                Title = "Сохранить отчёт как CSV",
                Filter = "CSV-файл (*.csv)|*.csv|Все файлы (*.*)|*.*",
                FileName = $"report_smeta{detail.SmetaId}_{detail.CreatedAt:yyyyMMdd_HHmmss}.csv"
            };
            if (sfd.ShowDialog() != true) return;

            await File.WriteAllTextAsync(sfd.FileName, detail.Content, new System.Text.UTF8Encoding(true));
            StatusMessage = $"Сохранено: {Path.GetFileName(sfd.FileName)} ({new FileInfo(sfd.FileName).Length} байт).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка скачивания", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
