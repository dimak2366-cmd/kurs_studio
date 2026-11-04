using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Head;

/// <summary>
/// VM экрана «Мои сметы» (Руководитель отдела). Сервер уже фильтрует по отделу
/// текущего пользователя. Экран только для чтения — создание/редактирование недоступно.
/// </summary>
public sealed class HeadSmetasViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<SmetaDto> _smetas = new();
    private SmetaDto? _selectedSmeta;
    private ObservableCollection<ExpenseItemDto> _items = new();
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public HeadSmetasViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<SmetaDto> Smetas { get => _smetas; private set => SetProperty(ref _smetas, value); }
    public ObservableCollection<ExpenseItemDto> Items { get => _items; private set => SetProperty(ref _items, value); }

    public SmetaDto? SelectedSmeta
    {
        get => _selectedSmeta;
        set
        {
            if (SetProperty(ref _selectedSmeta, value) && value != null)
            {
                _ = LoadItemsAsync(value.Id);
            }
        }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка смет...";
        try
        {
            var list = await _api.GetAsync<List<SmetaDto>>("/api/smeta");
            Smetas = new ObservableCollection<SmetaDto>(list);
            StatusMessage = $"Загружено смет отдела: {Smetas.Count}.";
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

    private async Task LoadItemsAsync(int smetaId)
    {
        IsBusy = true;
        try
        {
            var detail = await _api.GetAsync<SmetaDetailDto>($"/api/smeta/{smetaId}");
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
}
