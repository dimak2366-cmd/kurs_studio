using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Head;

/// <summary>
/// VM «Заявки» для Руководителя отдела. Показывает свои заявки и позволяет
/// подать новую (только по проектам своего отдела и статьям этого проекта).
/// </summary>
public sealed class HeadRequestsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<ExpenseRequestDto> _requests = new();
    private ObservableCollection<ProjectDto> _projects = new();
    private ExpenseRequestDto? _selectedRequest;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public HeadRequestsViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAllAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        _ = LoadAllAsync();
    }

    public ObservableCollection<ExpenseRequestDto> Requests { get => _requests; private set => SetProperty(ref _requests, value); }
    public ObservableCollection<ProjectDto> Projects { get => _projects; private set => SetProperty(ref _projects, value); }

    public ExpenseRequestDto? SelectedRequest
    {
        get => _selectedRequest;
        set { if (SetProperty(ref _selectedRequest, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand AddCommand { get; }

    private async Task LoadAllAsync()
    {
        IsBusy = true;
        try
        {
            var reqs = await _api.GetAsync<List<ExpenseRequestDto>>("/api/requests");
            Requests = new ObservableCollection<ExpenseRequestDto>(reqs);

            if (Projects.Count == 0)
            {
                var proj = await _api.GetAsync<List<ProjectDto>>("/api/projects");
                Projects = new ObservableCollection<ProjectDto>(proj);
            }

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

    private async Task AddAsync()
    {
        if (Projects.Count == 0)
        {
            MessageBox.Show("Нет проектов вашего отдела для подачи заявки.", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new CreateRequestDialog(Projects, _api);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            await LoadAllAsync();
        }
    }
}
