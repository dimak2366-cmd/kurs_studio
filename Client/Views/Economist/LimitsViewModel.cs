using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Economist;

/// <summary>
/// VM экрана «Лимиты». Позволяет выбрать смету, увидеть текущие планы/лимиты/факты,
/// отредактировать лимиты (только для неутверждённых смет) и проверить превышения.
/// </summary>
public sealed class LimitsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<SmetaDto> _smetas = new();
    private ObservableCollection<LimitCheckItemDto> _items = new();
    private SmetaDto? _selectedSmeta;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public LimitsViewModel(ApiClient api)
    {
        _api = api;
        LoadSmetasCommand = new AsyncRelayCommand(LoadSmetasAsync);
        ReloadCheckCommand = new AsyncRelayCommand(ReloadCheckAsync, () => SelectedSmeta != null);
        SaveLimitsCommand = new AsyncRelayCommand(SaveLimitsAsync, () => SelectedSmeta != null && CanEdit(SelectedSmeta));
        _ = LoadSmetasAsync();
    }

    public ObservableCollection<SmetaDto> Smetas { get => _smetas; private set => SetProperty(ref _smetas, value); }
    public ObservableCollection<LimitCheckItemDto> Items { get => _items; private set => SetProperty(ref _items, value); }

    public SmetaDto? SelectedSmeta
    {
        get => _selectedSmeta;
        set
        {
            if (SetProperty(ref _selectedSmeta, value) && value != null)
            {
                _ = ReloadCheckAsync();
            }
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool CanEditCurrent => SelectedSmeta != null && CanEdit(SelectedSmeta);
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadSmetasCommand { get; }
    public ICommand ReloadCheckCommand { get; }
    public ICommand SaveLimitsCommand { get; }

    private static bool CanEdit(SmetaDto smeta) =>
        smeta.Status != "Утверждена" && smeta.Status != "Закрыта";

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
            StatusMessage = $"Загружено смет: {Smetas.Count}.";
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

    private async Task ReloadCheckAsync()
    {
        if (SelectedSmeta == null) return;
        IsBusy = true;
        try
        {
            var check = await _api.GetAsync<LimitCheckDto>($"/api/limits/{SelectedSmeta.Id}/check");
            Items = new ObservableCollection<LimitCheckItemDto>(check.Items);
            StatusMessage = check.HasExcess
                ? $"ВНИМАНИЕ: превышение лимита по {check.Items.Count(i => i.Exceeds)} статьям."
                : "Превышений лимитов нет.";
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

    private async Task SaveLimitsAsync()
    {
        if (SelectedSmeta == null) return;
        IsBusy = true;
        try
        {
            var req = new SetLimitsRequest
            {
                Items = Items.Select(i => new SetLimitItemRequest
                {
                    ExpenseItemId = i.ExpenseItemId,
                    LimitAmount = i.LimitAmount
                }).ToList()
            };
            await _api.PutAsync<List<ExpenseItemDto>>($"/api/limits/{SelectedSmeta.Id}", req);
            await ReloadCheckAsync();
            MessageBox.Show("Лимиты сохранены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
