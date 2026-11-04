using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Director;

/// <summary>
/// VM экрана «Решения» (Директор): регистрация управленческих решений
/// по статье и периоду, просмотр списка ранее принятых решений.
/// </summary>
public sealed class DecisionsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<DecisionDto> _decisions = new();
    private ObservableCollection<ExpenseItemDto> _items = new();
    private ObservableCollection<PeriodDto> _periods = new();
    private ObservableCollection<SmetaDto> _smetas = new();

    private SmetaDto? _selectedSmetaForItems;
    private ExpenseItemDto? _selectedItem;
    private PeriodDto? _selectedPeriod;
    private string _newDecisionText = string.Empty;

    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public DecisionsViewModel(ApiClient api)
    {
        _api = api;
        LoadAllCommand = new AsyncRelayCommand(LoadAllAsync);
        LoadItemsCommand = new AsyncRelayCommand(LoadItemsAsync, () => SelectedSmetaForItems != null);
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => SelectedItem != null && SelectedPeriod != null && !string.IsNullOrWhiteSpace(NewDecisionText));
        _ = LoadAllAsync();
    }

    public ObservableCollection<DecisionDto> Decisions { get => _decisions; private set => SetProperty(ref _decisions, value); }
    public ObservableCollection<ExpenseItemDto> Items { get => _items; private set => SetProperty(ref _items, value); }
    public ObservableCollection<PeriodDto> Periods { get => _periods; private set => SetProperty(ref _periods, value); }
    public ObservableCollection<SmetaDto> Smetas { get => _smetas; private set => SetProperty(ref _smetas, value); }

    public SmetaDto? SelectedSmetaForItems
    {
        get => _selectedSmetaForItems;
        set { if (SetProperty(ref _selectedSmetaForItems, value)) CommandManager.InvalidateRequerySuggested(); }
    }
    public ExpenseItemDto? SelectedItem
    {
        get => _selectedItem;
        set { if (SetProperty(ref _selectedItem, value)) CommandManager.InvalidateRequerySuggested(); }
    }
    public PeriodDto? SelectedPeriod
    {
        get => _selectedPeriod;
        set { if (SetProperty(ref _selectedPeriod, value)) CommandManager.InvalidateRequerySuggested(); }
    }
    public string NewDecisionText
    {
        get => _newDecisionText;
        set { if (SetProperty(ref _newDecisionText, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadAllCommand { get; }
    public ICommand LoadItemsCommand { get; }
    public ICommand CreateCommand { get; }

    private async Task LoadAllAsync()
    {
        IsBusy = true;
        try
        {
            var decisions = await _api.GetAsync<List<DecisionDto>>("/api/decisions");
            Decisions = new ObservableCollection<DecisionDto>(decisions);

            if (Smetas.Count == 0)
            {
                var smetas = await _api.GetAsync<List<SmetaDto>>("/api/smeta");
                Smetas = new ObservableCollection<SmetaDto>(smetas);
            }
            if (Periods.Count == 0)
            {
                var periods = await _api.GetAsync<List<PeriodDto>>("/api/periods");
                Periods = new ObservableCollection<PeriodDto>(periods);
            }
            StatusMessage = $"Решений: {Decisions.Count}.";
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

    private async Task LoadItemsAsync()
    {
        if (SelectedSmetaForItems == null) return;
        IsBusy = true;
        try
        {
            var detail = await _api.GetAsync<SmetaDetailDto>($"/api/smeta/{SelectedSmetaForItems.Id}");
            Items = new ObservableCollection<ExpenseItemDto>(detail.Items);
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка загрузки статей: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateAsync()
    {
        if (SelectedItem == null || SelectedPeriod == null) return;
        if (string.IsNullOrWhiteSpace(NewDecisionText))
        {
            MessageBox.Show("Введите текст решения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var req = new CreateDecisionRequest
            {
                ExpenseItemId = SelectedItem.Id,
                PeriodId = SelectedPeriod.Id,
                Decision = NewDecisionText.Trim()
            };
            await _api.PostAsync<DecisionDto>("/api/decisions", req);
            NewDecisionText = string.Empty;
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
