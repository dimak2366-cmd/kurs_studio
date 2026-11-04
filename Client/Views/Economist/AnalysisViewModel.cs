using System.Collections.ObjectModel;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Economist;

/// <summary>
/// VM экрана «Анализ»: план-фактное отклонение по смете + факторный анализ
/// по статьям «Материалы» и «Фонд оплаты труда» (метод цепных подстановок).
/// </summary>
public sealed class AnalysisViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<SmetaDto> _smetas = new();
    private ObservableCollection<ExpenseItemDto> _items = new();
    private SmetaDto? _selectedSmeta;
    private ExpenseItemDto? _selectedMaterialItem;
    private ExpenseItemDto? _selectedLaborItem;

    private SmetaDeviationReportDto? _deviation;
    private MaterialAnalysisResult? _materialResult;
    private LaborAnalysisResult? _laborResult;

    private decimal _matQPlan, _matQFact, _matPPlan, _matPFact;
    private decimal _labChPlan, _labChFact, _labZPlan, _labZFact;

    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public AnalysisViewModel(ApiClient api)
    {
        _api = api;
        LoadSmetasCommand = new AsyncRelayCommand(LoadSmetasAsync);
        ReloadDeviationCommand = new AsyncRelayCommand(ReloadDeviationAsync, () => SelectedSmeta != null);
        CalculateMaterialsCommand = new AsyncRelayCommand(CalculateMaterialsAsync, () => SelectedSmeta != null);
        CalculateLaborCommand = new AsyncRelayCommand(CalculateLaborAsync, () => SelectedSmeta != null);
        AutofillMaterialsCommand = new AsyncRelayCommand(AutofillMaterialsAsync, () => SelectedMaterialItem != null);
        AutofillLaborCommand = new AsyncRelayCommand(AutofillLaborAsync, () => SelectedLaborItem != null);
        _ = LoadSmetasAsync();
    }

    // --- Smetas / Items ---
    public ObservableCollection<SmetaDto> Smetas { get => _smetas; private set => SetProperty(ref _smetas, value); }
    public ObservableCollection<ExpenseItemDto> Items { get => _items; private set => SetProperty(ref _items, value); }

    public SmetaDto? SelectedSmeta
    {
        get => _selectedSmeta;
        set
        {
            if (SetProperty(ref _selectedSmeta, value) && value != null)
            {
                _ = ReloadSmetaDetailsAsync(value.Id);
            }
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ExpenseItemDto? SelectedMaterialItem
    {
        get => _selectedMaterialItem;
        set { if (SetProperty(ref _selectedMaterialItem, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public ExpenseItemDto? SelectedLaborItem
    {
        get => _selectedLaborItem;
        set { if (SetProperty(ref _selectedLaborItem, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    // --- Plan/fact deviation ---
    public SmetaDeviationReportDto? Deviation { get => _deviation; private set => SetProperty(ref _deviation, value); }

    // --- Materials ---
    public decimal MatQPlan { get => _matQPlan; set => SetProperty(ref _matQPlan, value); }
    public decimal MatQFact { get => _matQFact; set => SetProperty(ref _matQFact, value); }
    public decimal MatPPlan { get => _matPPlan; set => SetProperty(ref _matPPlan, value); }
    public decimal MatPFact { get => _matPFact; set => SetProperty(ref _matPFact, value); }
    public MaterialAnalysisResult? MaterialResult { get => _materialResult; private set => SetProperty(ref _materialResult, value); }

    // --- Labor ---
    public decimal LabChPlan { get => _labChPlan; set => SetProperty(ref _labChPlan, value); }
    public decimal LabChFact { get => _labChFact; set => SetProperty(ref _labChFact, value); }
    public decimal LabZPlan { get => _labZPlan; set => SetProperty(ref _labZPlan, value); }
    public decimal LabZFact { get => _labZFact; set => SetProperty(ref _labZFact, value); }
    public LaborAnalysisResult? LaborResult { get => _laborResult; private set => SetProperty(ref _laborResult, value); }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadSmetasCommand { get; }
    public ICommand ReloadDeviationCommand { get; }
    public ICommand CalculateMaterialsCommand { get; }
    public ICommand CalculateLaborCommand { get; }
    public ICommand AutofillMaterialsCommand { get; }
    public ICommand AutofillLaborCommand { get; }

    private async Task LoadSmetasAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _api.GetAsync<List<SmetaDto>>("/api/smeta");
            Smetas = new ObservableCollection<SmetaDto>(list);
            if (SelectedSmeta == null && Smetas.Count > 0)
            {
                SelectedSmeta = Smetas[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка загрузки смет: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadSmetaDetailsAsync(int smetaId)
    {
        IsBusy = true;
        try
        {
            var detail = await _api.GetAsync<SmetaDetailDto>($"/api/smeta/{smetaId}");
            Items = new ObservableCollection<ExpenseItemDto>(detail.Items);
            MaterialResult = null;
            LaborResult = null;
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка загрузки статей сметы: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
        await ReloadDeviationAsync();
    }

    private async Task ReloadDeviationAsync()
    {
        if (SelectedSmeta == null) return;
        IsBusy = true;
        try
        {
            Deviation = await _api.GetAsync<SmetaDeviationReportDto>($"/api/analysis/deviation/{SelectedSmeta.Id}");
            StatusMessage = $"План: {Deviation.TotalPlan:N2} BYN, факт: {Deviation.TotalFact:N2} BYN, " +
                            $"отклонение: {Deviation.TotalAbsolute:N2} BYN ({Deviation.TotalRelativePercent:F2}%).";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка анализа: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task AutofillMaterialsAsync()
    {
        if (SelectedMaterialItem == null) return Task.CompletedTask;
        MatQPlan = SelectedMaterialItem.Quantity;
        MatPPlan = SelectedMaterialItem.Price;
        MatQFact = SelectedMaterialItem.Quantity;
        MatPFact = SelectedMaterialItem.Quantity > 0
            ? Math.Round(SelectedMaterialItem.FactAmount / SelectedMaterialItem.Quantity, 4)
            : 0m;
        return Task.CompletedTask;
    }

    private Task AutofillLaborAsync()
    {
        if (SelectedLaborItem == null) return Task.CompletedTask;
        LabChPlan = SelectedLaborItem.Quantity;
        LabZPlan = SelectedLaborItem.Price;
        LabChFact = SelectedLaborItem.Quantity;
        LabZFact = SelectedLaborItem.Quantity > 0
            ? Math.Round(SelectedLaborItem.FactAmount / SelectedLaborItem.Quantity, 4)
            : 0m;
        return Task.CompletedTask;
    }

    private async Task CalculateMaterialsAsync()
    {
        IsBusy = true;
        try
        {
            var qs = $"?qPlan={MatQPlan}&qFact={MatQFact}&pPlan={MatPPlan}&pFact={MatPFact}";
            MaterialResult = await _api.GetAsync<MaterialAnalysisResult>("/api/analysis/materials" + qs);
            StatusMessage = $"Материалы: общее отклонение {MaterialResult.TotalDeviation:N2} BYN, " +
                            $"проверка суммы {(MaterialResult.CheckPassed ? "пройдена" : "НЕ пройдена")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CalculateLaborAsync()
    {
        IsBusy = true;
        try
        {
            var qs = $"?chPlan={LabChPlan}&chFact={LabChFact}&zPlan={LabZPlan}&zFact={LabZFact}";
            LaborResult = await _api.GetAsync<LaborAnalysisResult>("/api/analysis/labor" + qs);
            StatusMessage = $"ФОТ: общее отклонение {LaborResult.TotalDeviation:N2} BYN, " +
                            $"проверка суммы {(LaborResult.CheckPassed ? "пройдена" : "НЕ пройдена")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
