using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Economist;

/// <summary>
/// VM экрана «Сметы» (Главный экономист). Просмотр списка смет,
/// создание новых, редактирование плановых сумм, формирование изменений.
/// </summary>
public sealed class SmetasViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private ObservableCollection<SmetaDto> _smetas = new();
    private ObservableCollection<ProjectDto> _projects = new();
    private SmetaDto? _selectedSmeta;
    private ProjectDto? _filterProject;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public SmetasViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedSmeta != null && SelectedSmeta.Status != "Утверждена" && SelectedSmeta.Status != "Закрыта");
        ChangesCommand = new AsyncRelayCommand(ChangesAsync, () => SelectedSmeta != null && SelectedSmeta.Status == "Утверждена");
        _ = LoadAsync();
    }

    public ObservableCollection<SmetaDto> Smetas { get => _smetas; private set => SetProperty(ref _smetas, value); }
    public ObservableCollection<ProjectDto> Projects { get => _projects; private set => SetProperty(ref _projects, value); }

    public SmetaDto? SelectedSmeta
    {
        get => _selectedSmeta;
        set { if (SetProperty(ref _selectedSmeta, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public ProjectDto? FilterProject
    {
        get => _filterProject;
        set { if (SetProperty(ref _filterProject, value)) _ = LoadAsync(); }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand LoadCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ChangesCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка смет...";
        try
        {
            if (Projects.Count == 0)
            {
                var proj = await _api.GetAsync<List<ProjectDto>>("/api/projects");
                Projects = new ObservableCollection<ProjectDto>(proj);
            }

            var url = FilterProject != null ? $"/api/smeta?projectId={FilterProject.Id}" : "/api/smeta";
            var list = await _api.GetAsync<List<SmetaDto>>(url);
            Smetas = new ObservableCollection<SmetaDto>(list);
            StatusMessage = $"Загружено смет: {Smetas.Count}.";
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
        var dialog = new SmetaCreateDialog(Projects);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            IsBusy = true;
            try
            {
                await _api.PostAsync<SmetaDetailDto>("/api/smeta", dialog.Result);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка создания сметы", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private async Task EditAsync()
    {
        if (SelectedSmeta == null) return;

        IsBusy = true;
        SmetaDetailDto detail;
        try
        {
            detail = await _api.GetAsync<SmetaDetailDto>($"/api/smeta/{SelectedSmeta.Id}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
            IsBusy = false;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        var dialog = new SmetaEditDialog(detail);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            IsBusy = true;
            try
            {
                await _api.PutAsync<SmetaDetailDto>($"/api/smeta/{SelectedSmeta.Id}", dialog.Result);
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
    }

    private async Task ChangesAsync()
    {
        if (SelectedSmeta == null) return;

        var confirm = MessageBox.Show(
            $"Сформировать изменение к смете #{SelectedSmeta.Id} ({SelectedSmeta.FormCode})?",
            "Изменение к смете", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.PostAsync<SmetaDetailDto>($"/api/smeta/{SelectedSmeta.Id}/changes");
            await LoadAsync();
            MessageBox.Show("Изменение создано. Оно находится в статусе «На утверждении».",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
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
