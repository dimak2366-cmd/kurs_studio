using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Director;

/// <summary>
/// VM сводного бюджета для Директора: счётчики по сметам/заявкам/отчётам
/// и таблица со всеми сметами института.
/// Загрузка защищена try-catch и выполняется в UI-потоке через Dispatcher,
/// чтобы исключения не всплывали в DataTemplate и не закрывали приложение.
/// </summary>
public sealed class OverviewViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<SmetaDto> _smetas = new();
    private int _smetasPending;
    private int _reportsPending;
    private int _requestsPending;
    private decimal _totalPlan;
    private decimal _totalFact;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public OverviewViewModel(ApiClient api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        LoadCommand = new AsyncRelayCommand(LoadAsync);

        // Отложенная загрузка, чтобы конструктор не блокировал создание View
        // и чтобы исключения гарантированно не всплывали в DataTemplate.
        Application.Current?.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await LoadAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка загрузки: " + ex.Message;
                MessageBox.Show(ex.ToString(), "Ошибка загрузки сводки",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }));
    }

    public ObservableCollection<SmetaDto> Smetas
    {
        get => _smetas;
        private set => SetProperty(ref _smetas, value);
    }

    public int SmetasPending { get => _smetasPending; private set => SetProperty(ref _smetasPending, value); }
    public int ReportsPending { get => _reportsPending; private set => SetProperty(ref _reportsPending, value); }
    public int RequestsPending { get => _requestsPending; private set => SetProperty(ref _requestsPending, value); }
    public decimal TotalPlan { get => _totalPlan; private set => SetProperty(ref _totalPlan, value); }
    public decimal TotalFact { get => _totalFact; private set => SetProperty(ref _totalFact, value); }
    public decimal TotalDeviation => TotalFact - TotalPlan;

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка сводки...";
        try
        {
            List<SmetaDto> smetas;
            try
            {
                smetas = await _api.GetAsync<List<SmetaDto>>("/api/smeta");
            }
            catch (Exception ex)
            {
                // Если сметы не загрузились — это фатально, но не валим приложение.
                StatusMessage = "Ошибка загрузки смет: " + ex.Message;
                return;
            }

            Smetas = new ObservableCollection<SmetaDto>(smetas);

            // Отчёты и заявки — вспомогательные, если упадут — не критично.
            try
            {
                var reports = await _api.GetAsync<List<ReportDto>>("/api/reports");
                ReportsPending = reports.Count(r => r.Status == "На утверждении");
            }
            catch (Exception ex)
            {
                ReportsPending = 0;
                StatusMessage = "Отчёты недоступны: " + ex.Message;
            }

            try
            {
                var requests = await _api.GetAsync<List<ExpenseRequestDto>>("/api/requests");
                RequestsPending = requests.Count(r => r.Status == "На рассмотрении");
            }
            catch (Exception ex)
            {
                RequestsPending = 0;
                StatusMessage = "Заявки недоступны: " + ex.Message;
            }

            SmetasPending = smetas.Count(s => s.Status == "На утверждении");
            TotalPlan = smetas.Sum(s => s.TotalPlan);
            TotalFact = smetas.Sum(s => s.TotalFact);
            OnPropertyChanged(nameof(TotalDeviation));

            StatusMessage = $"Загружено смет: {smetas.Count}, ожидают утверждения: {SmetasPending}.";
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
}
