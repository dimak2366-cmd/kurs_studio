using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Microsoft.Win32;
using Shared.Dtos;

namespace Client.Views.Economist;

/// <summary>
/// VM экрана «Отчёты» (Главный экономист). Список отчётов, создание по выбранной смете,
/// скачивание CSV. Утверждение отчёта делает Директор (этап 13).
/// </summary>
public sealed class ReportsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<ReportDto> _reports = new();
    private ObservableCollection<SmetaDto> _smetas = new();
    private ReportDto? _selectedReport;
    private SmetaDto? _smetaForCreate;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public ReportsViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => SmetaForCreate != null);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => SelectedReport != null);
        _ = LoadAsync();
    }

    public ObservableCollection<ReportDto> Reports { get => _reports; private set => SetProperty(ref _reports, value); }
    public ObservableCollection<SmetaDto> Smetas { get => _smetas; private set => SetProperty(ref _smetas, value); }

    public ReportDto? SelectedReport
    {
        get => _selectedReport;
        set { if (SetProperty(ref _selectedReport, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public SmetaDto? SmetaForCreate
    {
        get => _smetaForCreate;
        set { if (SetProperty(ref _smetaForCreate, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand DownloadCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка отчётов...";
        try
        {
            var reports = await _api.GetAsync<List<ReportDto>>("/api/reports");
            Reports = new ObservableCollection<ReportDto>(reports);

            if (Smetas.Count == 0)
            {
                var smetas = await _api.GetAsync<List<SmetaDto>>("/api/smeta");
                Smetas = new ObservableCollection<SmetaDto>(smetas);
            }

            StatusMessage = $"Отчётов: {Reports.Count}, доступных смет: {Smetas.Count}.";
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
        if (SmetaForCreate == null) return;

        IsBusy = true;
        try
        {
            var report = await _api.PostAsync<ReportDetailDto>("/api/reports",
                new CreateReportRequest { SmetaId = SmetaForCreate.Id });

            MessageBox.Show(
                $"Отчёт #{report.Id} создан. Длина содержимого: {report.Content.Length} символов.\n" +
                "Статус: «На утверждении». Утверждение выполняет Директор.",
                "Отчёт создан", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка создания", MessageBoxButton.OK, MessageBoxImage.Error);
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
            // Получаем детальную запись отчёта (там есть Content) и сохраняем в файл.
            var detail = await _api.GetAsync<ReportDetailDto>($"/api/reports/{SelectedReport.Id}");
            var sfd = new SaveFileDialog
            {
                Title = "Сохранить отчёт как CSV",
                Filter = "CSV-файл (*.csv)|*.csv|Все файлы (*.*)|*.*",
                FileName = $"report_smeta{detail.SmetaId}_{detail.CreatedAt:yyyyMMdd_HHmmss}.csv"
            };
            if (sfd.ShowDialog() != true) return;

            // UTF-8 с BOM — чтобы Excel корректно открыл кириллицу.
            await File.WriteAllTextAsync(sfd.FileName, detail.Content, new System.Text.UTF8Encoding(true));
            StatusMessage = $"Отчёт сохранён: {Path.GetFileName(sfd.FileName)} ({new FileInfo(sfd.FileName).Length} байт).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка скачивания", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
