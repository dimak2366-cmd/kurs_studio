using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// VM экрана «Проекты». CRUD проектов, привязка к отделам.
/// </summary>
public sealed class ProjectsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<ProjectDto> _projects = new();
    private ObservableCollection<DepartmentDto> _departments = new();
    private ProjectDto? _selectedProject;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public ProjectsViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedProject != null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedProject != null);
        _ = LoadAsync();
    }

    public ObservableCollection<ProjectDto> Projects { get => _projects; private set => SetProperty(ref _projects, value); }
    public ObservableCollection<DepartmentDto> Departments { get => _departments; private set => SetProperty(ref _departments, value); }

    public ProjectDto? SelectedProject
    {
        get => _selectedProject;
        set { if (SetProperty(ref _selectedProject, value)) CommandManager.InvalidateRequerySuggested(); }
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
            var list = await _api.GetAsync<List<ProjectDto>>("/api/projects");
            Projects = new ObservableCollection<ProjectDto>(list);

            if (Departments.Count == 0)
            {
                var deps = await _api.GetAsync<List<DepartmentDto>>("/api/departments");
                Departments = new ObservableCollection<DepartmentDto>(deps);
            }
            StatusMessage = $"Загружено проектов: {Projects.Count}.";
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
        var dialog = new ProjectEditDialog(null, Departments);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            _ = CreateAsync(dialog.ResultCode, dialog.ResultName, dialog.ResultDepartmentId,
                            dialog.ResultStartDate, dialog.ResultEndDate);
        }
        return Task.CompletedTask;
    }

    private async Task CreateAsync(string code, string name, int departmentId, DateTime startDate, DateTime endDate)
    {
        IsBusy = true;
        try
        {
            var req = new CreateProjectRequest
            {
                Code = code,
                Name = name,
                DepartmentId = departmentId,
                StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc)
            };
            await _api.PostAsync<ProjectDto>("/api/projects", req);
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
        if (SelectedProject == null) return Task.CompletedTask;

        var dialog = new ProjectEditDialog(SelectedProject, Departments);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            _ = UpdateAsync(SelectedProject.Id, dialog.ResultCode, dialog.ResultName,
                            dialog.ResultDepartmentId, dialog.ResultStartDate, dialog.ResultEndDate);
        }
        return Task.CompletedTask;
    }

    private async Task UpdateAsync(int id, string code, string name, int departmentId, DateTime startDate, DateTime endDate)
    {
        IsBusy = true;
        try
        {
            var req = new UpdateProjectRequest
            {
                Code = code,
                Name = name,
                DepartmentId = departmentId,
                StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc)
            };
            await _api.PutAsync<ProjectDto>($"/api/projects/{id}", req);
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
        if (SelectedProject == null) return;

        var confirm = MessageBox.Show(
            $"Удалить проект «{SelectedProject.Code} — {SelectedProject.Name}»?",
            "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.DeleteAsync($"/api/projects/{SelectedProject.Id}");
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
