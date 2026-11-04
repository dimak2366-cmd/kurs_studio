using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// VM экрана «Справочники»: редактируемый справочник статей (CatalogExpenseItem)
/// + read-only срезы по ролям, статьям из смет и формам смет.
/// </summary>
public sealed class CatalogViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<RoleDto> _roles = new();
    private ObservableCollection<CatalogItemDto> _items = new();
    private ObservableCollection<SmetaFormDto> _forms = new();
    private ObservableCollection<CatalogExpenseItemDto> _catalogItems = new();
    private CatalogExpenseItemDto? _selectedCatalogItem;

    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public CatalogViewModel(ApiClient api)
    {
        _api = api;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedCatalogItem != null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedCatalogItem != null);

        _ = LoadAsync();
    }

    public ObservableCollection<RoleDto> Roles { get => _roles; private set => SetProperty(ref _roles, value); }
    public ObservableCollection<CatalogItemDto> ExpenseItems { get => _items; private set => SetProperty(ref _items, value); }
    public ObservableCollection<SmetaFormDto> SmetaForms { get => _forms; private set => SetProperty(ref _forms, value); }
    public ObservableCollection<CatalogExpenseItemDto> CatalogItems { get => _catalogItems; private set => SetProperty(ref _catalogItems, value); }

    public CatalogExpenseItemDto? SelectedCatalogItem
    {
        get => _selectedCatalogItem;
        set { if (SetProperty(ref _selectedCatalogItem, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }

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

            var catalog = await _api.GetAsync<List<CatalogExpenseItemDto>>("/api/catalog/catalog-items");
            CatalogItems = new ObservableCollection<CatalogExpenseItemDto>(catalog);

            StatusMessage = $"Справочник: {CatalogItems.Count} записей. Роли: {Roles.Count}, статьи смет: {ExpenseItems.Count}, формы: {SmetaForms.Count}.";
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
        var dialog = new CatalogItemEditDialog(null);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            var request = new CreateCatalogItemRequest
            {
                Name = dialog.ResultName,
                KosguCode = dialog.ResultKosguCode,
                IsActive = dialog.ResultIsActive
            };
            await _api.PostAsync<CatalogExpenseItemDto>("/api/catalog/catalog-items", request);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка создания", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EditAsync()
    {
        if (SelectedCatalogItem == null) return;

        var dialog = new CatalogItemEditDialog(SelectedCatalogItem);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            var request = new UpdateCatalogItemRequest
            {
                Name = dialog.ResultName,
                KosguCode = dialog.ResultKosguCode,
                IsActive = dialog.ResultIsActive
            };
            await _api.PutAsync<CatalogExpenseItemDto>(
                $"/api/catalog/catalog-items/{SelectedCatalogItem.Id}", request);
            await LoadAsync();
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

    private async Task DeleteAsync()
    {
        if (SelectedCatalogItem == null) return;

        var confirm = MessageBox.Show(
            $"Удалить элемент справочника «{SelectedCatalogItem.KosguCode} / {SelectedCatalogItem.Name}»?\n" +
            "Это действие необратимо.",
            "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.DeleteAsync($"/api/catalog/catalog-items/{SelectedCatalogItem.Id}");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
