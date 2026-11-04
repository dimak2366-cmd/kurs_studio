using System.Collections.ObjectModel;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// VM экрана «Справочники»: роли, статьи расходов (уникальные), формы смет.
/// Только чтение — редактирование справочников вынесено в бизнес-логику.
/// </summary>
public sealed class CatalogViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<RoleDto> _roles = new();
    private ObservableCollection<CatalogItemDto> _items = new();
    private ObservableCollection<SmetaFormDto> _forms = new();
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public CatalogViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<RoleDto> Roles { get => _roles; private set => SetProperty(ref _roles, value); }
    public ObservableCollection<CatalogItemDto> ExpenseItems { get => _items; private set => SetProperty(ref _items, value); }
    public ObservableCollection<SmetaFormDto> SmetaForms { get => _forms; private set => SetProperty(ref _forms, value); }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка...";
        try
        {
            var roles = await _api.GetAsync<List<RoleDto>>("/api/catalog/roles");
            Roles = new ObservableCollection<RoleDto>(roles);

            var items = await _api.GetAsync<List<CatalogItemDto>>("/api/catalog/expense-items");
            ExpenseItems = new ObservableCollection<CatalogItemDto>(items);

            var forms = await _api.GetAsync<List<SmetaFormDto>>("/api/catalog/smeta-forms");
            SmetaForms = new ObservableCollection<SmetaFormDto>(forms);

            StatusMessage = $"Роли: {Roles.Count}, статьи: {ExpenseItems.Count}, формы: {SmetaForms.Count}.";
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
