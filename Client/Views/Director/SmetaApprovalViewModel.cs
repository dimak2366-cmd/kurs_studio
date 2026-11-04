using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Client.Views.Economist;
using Shared.Dtos;

namespace Client.Views.Director;

/// <summary>
/// VM экрана «Утверждение смет». Список смет в статусе «На утверждении»,
/// кнопки «Утвердить» и «Отклонить» с причиной.
/// </summary>
public sealed class SmetaApprovalViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<SmetaDto> _smetas = new();
    private SmetaDto? _selectedSmeta;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public SmetaApprovalViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => SelectedSmeta != null);
        RejectCommand = new AsyncRelayCommand(RejectAsync, () => SelectedSmeta != null);
        _ = LoadAsync();
    }

    public ObservableCollection<SmetaDto> Smetas { get => _smetas; private set => SetProperty(ref _smetas, value); }

    public SmetaDto? SelectedSmeta
    {
        get => _selectedSmeta;
        set { if (SetProperty(ref _selectedSmeta, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка смет на утверждении...";
        try
        {
            var list = await _api.GetAsync<List<SmetaDto>>("/api/smeta?status=" + Uri.EscapeDataString("На утверждении"));
            Smetas = new ObservableCollection<SmetaDto>(list);
            StatusMessage = $"Смет на утверждении: {Smetas.Count}.";
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
        if (SelectedSmeta == null) return;

        var confirm = MessageBox.Show(
            $"Утвердить смету #{SelectedSmeta.Id} ({SelectedSmeta.FormCode}, {SelectedSmeta.Year})?\n" +
            $"План: {SelectedSmeta.TotalPlan:N2} BYN, лимит: {SelectedSmeta.TotalLimit:N2} BYN.",
            "Утверждение сметы", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.PostAsync<SmetaDetailDto>($"/api/smeta/{SelectedSmeta.Id}/approve");
            await LoadAsync();
            MessageBox.Show("Смета утверждена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
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
        if (SelectedSmeta == null) return;

        var dialog = new RejectReasonDialog(
            $"Причина отклонения сметы #{SelectedSmeta.Id} ({SelectedSmeta.FormCode}):")
        {
            Title = "Отклонение сметы"
        };
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            await _api.PostAsync<SmetaDetailDto>(
                $"/api/smeta/{SelectedSmeta.Id}/reject",
                new RejectSmetaRequest { Reason = dialog.Reason });
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
