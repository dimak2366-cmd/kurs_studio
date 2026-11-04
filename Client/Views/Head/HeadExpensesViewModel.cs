using System.Collections.ObjectModel;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Head;

/// <summary>
/// VM «Мои расходы». Read-only просмотр фактических расходов по проектам отдела
/// (сервер сам фильтрует по DepartmentId текущего пользователя).
/// </summary>
public sealed class HeadExpensesViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<ActualExpenseDto> _expenses = new();
    private string _filterStatus = string.Empty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public HeadExpensesViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<ActualExpenseDto> Expenses { get => _expenses; private set => SetProperty(ref _expenses, value); }

    public string FilterStatus
    {
        get => _filterStatus;
        set
        {
            if (SetProperty(ref _filterStatus, value)) _ = LoadAsync();
        }
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

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка расходов...";
        try
        {
            var url = "/api/expenses";
            if (!string.IsNullOrEmpty(FilterStatus) && FilterStatus != "Все")
            {
                url += "?status=" + Uri.EscapeDataString(FilterStatus);
            }
            var list = await _api.GetAsync<List<ActualExpenseDto>>(url);
            Expenses = new ObservableCollection<ActualExpenseDto>(list);
            StatusMessage = $"Загружено расходов: {Expenses.Count}, сумма: {Expenses.Sum(e => e.Amount):N2} BYN.";
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
