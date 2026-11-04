using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Economist;

/// <summary>
/// VM экрана «Заявки» (Главный экономист). Просмотр заявок руководителей отделов,
/// согласование, отклонение с указанием причины.
/// </summary>
public sealed class RequestsReviewViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<ExpenseRequestDto> _requests = new();
    private ExpenseRequestDto? _selectedRequest;
    private string _filterStatus = "На рассмотрении";
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public RequestsReviewViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => SelectedRequest != null && SelectedRequest.Status == "На рассмотрении");
        RejectCommand = new AsyncRelayCommand(RejectAsync, () => SelectedRequest != null && SelectedRequest.Status == "На рассмотрении");
        _ = LoadAsync();
    }

    public ObservableCollection<ExpenseRequestDto> Requests
    {
        get => _requests;
        private set => SetProperty(ref _requests, value);
    }

    public ExpenseRequestDto? SelectedRequest
    {
        get => _selectedRequest;
        set { if (SetProperty(ref _selectedRequest, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public string FilterStatus
    {
        get => _filterStatus;
        set
        {
            if (SetProperty(ref _filterStatus, value))
            {
                _ = LoadAsync();
            }
        }
    }

    public ObservableCollection<string> StatusOptions { get; } = new()
    {
        "Все",
        "На рассмотрении",
        "Согласована",
        "Отклонена"
    };

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка заявок...";
        try
        {
            var url = "/api/requests";
            if (!string.IsNullOrEmpty(FilterStatus) && FilterStatus != "Все")
            {
                url += "?status=" + Uri.EscapeDataString(FilterStatus);
            }
            var list = await _api.GetAsync<List<ExpenseRequestDto>>(url);
            Requests = new ObservableCollection<ExpenseRequestDto>(list);
            StatusMessage = $"Заявок: {Requests.Count}.";
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

    private async Task ApproveAsync()
    {
        if (SelectedRequest == null) return;

        var dialog = new RejectReasonDialog(
            $"Согласовать заявку #{SelectedRequest.Id} на {SelectedRequest.Amount:N2} BYN?\n" +
            "При желании добавьте комментарий (необязательно) — оставьте пустым, чтобы пропустить.")
        {
            Title = "Согласование заявки"
        };
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        // Для approve используем тот же диалог, но комментарий необязателен — упрощаем: согласовываем без комментария.
        // Создаём простой MessageBox для approve.
        var confirm = MessageBox.Show(
            $"Согласовать заявку #{SelectedRequest.Id} на {SelectedRequest.Amount:N2} BYN " +
            $"по статье «{SelectedRequest.ExpenseItemName}»?",
            "Согласование", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.PostAsync<ExpenseRequestDto>(
                $"/api/requests/{SelectedRequest.Id}/approve",
                new ReviewRequestRequest { Comment = "Согласовано" });
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

    private async Task RejectAsync()
    {
        if (SelectedRequest == null) return;

        var dialog = new RejectReasonDialog(
            $"Укажите причину отклонения заявки #{SelectedRequest.Id} на {SelectedRequest.Amount:N2} BYN:")
        {
            Title = "Отклонение заявки"
        };
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            await _api.PostAsync<ExpenseRequestDto>(
                $"/api/requests/{SelectedRequest.Id}/reject",
                new ReviewRequestRequest { Comment = dialog.Reason });
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
