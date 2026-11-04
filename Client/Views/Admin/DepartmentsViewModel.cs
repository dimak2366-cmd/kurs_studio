using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// VM экрана «Отделы». CRUD подразделений.
/// </summary>
public sealed class DepartmentsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<DepartmentDto> _departments = new();
    private DepartmentDto? _selectedDepartment;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public DepartmentsViewModel(ApiClient api)
    {
        _api = api;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedDepartment != null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedDepartment != null);

        _ = LoadAsync();
    }

    public ObservableCollection<DepartmentDto> Departments
    {
        get => _departments;
        private set => SetProperty(ref _departments, value);
    }

    public DepartmentDto? SelectedDepartment
    {
        get => _selectedDepartment;
        set { if (SetProperty(ref _selectedDepartment, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

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
            var list = await _api.GetAsync<List<DepartmentDto>>("/api/departments");
            Departments = new ObservableCollection<DepartmentDto>(list);
            StatusMessage = $"Загружено отделов: {Departments.Count}.";
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

    private Task AddAsync()
    {
        var dialog = new DepartmentEditDialog(null);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            _ = CreateAsync(dialog.ResultName);
        }
        return Task.CompletedTask;
    }

    private async Task CreateAsync(string name)
    {
        IsBusy = true;
        try
        {
            await _api.PostAsync<DepartmentDto>("/api/departments", new CreateDepartmentRequest { Name = name });
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

    private Task EditAsync()
    {
        if (SelectedDepartment == null) return Task.CompletedTask;

        var dialog = new DepartmentEditDialog(SelectedDepartment);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            _ = UpdateAsync(SelectedDepartment.Id, dialog.ResultName);
        }
        return Task.CompletedTask;
    }

    private async Task UpdateAsync(int id, string name)
    {
        IsBusy = true;
        try
        {
            await _api.PutAsync<DepartmentDto>($"/api/departments/{id}", new UpdateDepartmentRequest { Name = name });
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
        if (SelectedDepartment == null) return;

        var confirm = MessageBox.Show(
            $"Удалить отдел «{SelectedDepartment.Name}»?",
            "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.DeleteAsync($"/api/departments/{SelectedDepartment.Id}");
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
