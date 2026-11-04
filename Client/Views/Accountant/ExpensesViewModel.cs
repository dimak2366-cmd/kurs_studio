using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Accountant;

/// <summary>
/// VM экрана «Ввод расходов» (Бухгалтер). Объединяет ввод/корректировку/сверку расходов
/// с реестром. Все операции разрешены только в открытых периодах.
/// </summary>
public sealed class ExpensesViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<ActualExpenseDto> _expenses = new();
    private ActualExpenseDto? _selectedExpense;
    private string _filterStatus = string.Empty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public ExpensesViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedExpense != null);
        VerifyCommand = new AsyncRelayCommand(VerifyAsync, () => SelectedExpense != null && SelectedExpense.Status != "Сверен");
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedExpense != null);
        _ = LoadAsync();
    }

    public ObservableCollection<ActualExpenseDto> Expenses
    {
        get => _expenses;
        private set => SetProperty(ref _expenses, value);
    }

    public ActualExpenseDto? SelectedExpense
    {
        get => _selectedExpense;
        set { if (SetProperty(ref _selectedExpense, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public string FilterStatus
    {
        get => _filterStatus;
        set { if (SetProperty(ref _filterStatus, value)) _ = LoadAsync(); }
    }

    public ObservableCollection<string> StatusOptions { get; } = new()
    {
        "Все",
        "Введён",
        "Скорректирован",
        "Сверен"
    };

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand VerifyCommand { get; }
    public ICommand DeleteCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка реестра расходов...";
        try
        {
            var url = "/api/expenses";
            if (!string.IsNullOrEmpty(FilterStatus) && FilterStatus != "Все")
            {
                url += "?status=" + Uri.EscapeDataString(FilterStatus);
            }
            var list = await _api.GetAsync<List<ActualExpenseDto>>(url);
            Expenses = new ObservableCollection<ActualExpenseDto>(list);
            StatusMessage = $"Записей: {Expenses.Count}, сумма: {Expenses.Sum(e => e.Amount):N2} BYN.";
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

    private async Task AddAsync()
    {
        var dialog = new ActualExpenseEditDialog(null, _api);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            await LoadAsync();
        }
    }

    private async Task EditAsync()
    {
        if (SelectedExpense == null) return;

        var dialog = new ActualExpenseEditDialog(SelectedExpense, _api);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            await LoadAsync();
        }
    }

    private async Task VerifyAsync()
    {
        if (SelectedExpense == null) return;

        var confirm = MessageBox.Show(
            $"Сверить расход #{SelectedExpense.Id} ({SelectedExpense.Amount:N2} BYN, {SelectedExpense.DocumentType} № {SelectedExpense.DocumentNumber})?",
            "Сверка с первичным документом", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.PostAsync<ActualExpenseDto>($"/api/expenses/{SelectedExpense.Id}/verify");
            await LoadAsync();
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

    private async Task DeleteAsync()
    {
        if (SelectedExpense == null) return;

        var confirm = MessageBox.Show(
            $"Удалить запись о расходе #{SelectedExpense.Id} на {SelectedExpense.Amount:N2} BYN?\n" +
            "Это действие необратимо.",
            "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.DeleteAsync($"/api/expenses/{SelectedExpense.Id}");
            await LoadAsync();
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
