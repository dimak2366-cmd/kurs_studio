using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Accountant;

/// <summary>
/// VM экрана «Периоды» (Бухгалтер). Список периодов, закрытие с проверкой
/// (все расходы должны быть сверены — гарантирует сервер).
/// </summary>
public sealed class PeriodsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<PeriodDto> _periods = new();
    private PeriodDto? _selectedPeriod;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public PeriodsViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => SelectedPeriod != null && !SelectedPeriod.IsClosed);
        _ = LoadAsync();
    }

    public ObservableCollection<PeriodDto> Periods
    {
        get => _periods;
        private set => SetProperty(ref _periods, value);
    }

    public PeriodDto? SelectedPeriod
    {
        get => _selectedPeriod;
        set { if (SetProperty(ref _selectedPeriod, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка периодов...";
        try
        {
            var list = await _api.GetAsync<List<PeriodDto>>("/api/periods");
            Periods = new ObservableCollection<PeriodDto>(list);
            StatusMessage = $"Периодов: {Periods.Count}.";
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

    private async Task CloseAsync()
    {
        if (SelectedPeriod == null) return;

        var confirm = MessageBox.Show(
            $"Закрыть период {SelectedPeriod.Year:D4}/{SelectedPeriod.Month:D2}?\n\n" +
            $"Записей: {SelectedPeriod.ExpenseCount}, несверенных: {SelectedPeriod.UnverifiedCount}, " +
            $"сумма: {SelectedPeriod.TotalAmount:N2} BYN.\n\n" +
            "После закрытия ввод и корректировка расходов за этот период будут невозможны.",
            "Закрытие периода", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.PostAsync<PeriodDto>($"/api/periods/{SelectedPeriod.Id}/close");
            await LoadAsync();
            MessageBox.Show("Период закрыт.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
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
