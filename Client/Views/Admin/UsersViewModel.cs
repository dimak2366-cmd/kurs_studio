using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Client.Services;
using Client.ViewModels;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// VM экрана «Пользователи». Позволяет загружать список, создавать, редактировать,
/// блокировать и удалять учётные записи, сбрасывать пароль.
/// </summary>
public sealed class UsersViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SessionService _session;

    private ObservableCollection<UserDto> _users = new();
    private ObservableCollection<RoleDto> _roles = new();
    private ObservableCollection<DepartmentDto> _departments = new();
    private UserDto? _selectedUser;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public UsersViewModel(ApiClient api, SessionService session)
    {
        _api = api;
        _session = session;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedUser != null);
        BlockCommand = new AsyncRelayCommand(BlockAsync, () => SelectedUser != null && !SelectedUser.IsBlocked);
        UnblockCommand = new AsyncRelayCommand(UnblockAsync, () => SelectedUser != null && SelectedUser.IsBlocked);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => SelectedUser != null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedUser != null);

        _ = LoadAsync();
    }

    public ObservableCollection<UserDto> Users { get => _users; private set => SetProperty(ref _users, value); }
    public ObservableCollection<RoleDto> Roles { get => _roles; private set => SetProperty(ref _roles, value); }
    public ObservableCollection<DepartmentDto> Departments { get => _departments; private set => SetProperty(ref _departments, value); }

    public UserDto? SelectedUser
    {
        get => _selectedUser;
        set { if (SetProperty(ref _selectedUser, value)) CommandManager.InvalidateRequerySuggested(); }
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
    public ICommand BlockCommand { get; }
    public ICommand UnblockCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand DeleteCommand { get; }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка...";
        try
        {
            var users = await _api.GetAsync<List<UserDto>>("/api/users");
            Users = new ObservableCollection<UserDto>(users);

            if (Roles.Count == 0)
            {
                var roles = await _api.GetAsync<List<RoleDto>>("/api/catalog/roles");
                Roles = new ObservableCollection<RoleDto>(roles);
            }
            if (Departments.Count == 0)
            {
                var deps = await _api.GetAsync<List<DepartmentDto>>("/api/departments");
                Departments = new ObservableCollection<DepartmentDto>(deps);
            }

            StatusMessage = $"Загружено пользователей: {Users.Count}.";
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
        var dialog = new UserEditDialog(null, Roles, Departments);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            _ = CreateAsync(dialog.ResultLogin, dialog.ResultPassword, dialog.ResultFullName, dialog.ResultRoleId, dialog.ResultDepartmentId);
        }
        return Task.CompletedTask;
    }

    private async Task CreateAsync(string login, string password, string fullName, int roleId, int? departmentId)
    {
        IsBusy = true;
        StatusMessage = "Создание пользователя...";
        try
        {
            var request = new CreateUserRequest
            {
                Login = login,
                Password = password,
                FullName = fullName,
                RoleId = roleId,
                DepartmentId = departmentId
            };
            await _api.PostAsync<UserDto>("/api/users", request);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task EditAsync()
    {
        if (SelectedUser == null) return Task.CompletedTask;

        var dialog = new UserEditDialog(SelectedUser, Roles, Departments);
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner != null) dialog.Owner = owner;

        if (dialog.ShowDialog() == true)
        {
            _ = UpdateAsync(SelectedUser.Id, dialog.ResultFullName, dialog.ResultRoleId, dialog.ResultDepartmentId);
        }
        return Task.CompletedTask;
    }

    private async Task UpdateAsync(int id, string fullName, int roleId, int? departmentId)
    {
        IsBusy = true;
        try
        {
            var request = new UpdateUserRequest { FullName = fullName, RoleId = roleId, DepartmentId = departmentId };
            await _api.PutAsync<UserDto>($"/api/users/{id}", request);
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

    private async Task BlockAsync()
    {
        if (SelectedUser == null) return;
        IsBusy = true;
        try
        {
            await _api.PostAsync<object>($"/api/users/{SelectedUser.Id}/block");
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

    private async Task UnblockAsync()
    {
        if (SelectedUser == null) return;
        IsBusy = true;
        try
        {
            await _api.PostAsync<object>($"/api/users/{SelectedUser.Id}/unblock");
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

    private async Task ResetPasswordAsync()
    {
        if (SelectedUser == null) return;

        var newPassword = "Temp" + Guid.NewGuid().ToString("N").Substring(0, 8) + "!";
        var confirm = MessageBox.Show(
            $"Сбросить пароль пользователя «{SelectedUser.Login}»?\nНовый пароль будет: {newPassword}",
            "Сброс пароля", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var request = new ResetPasswordRequest { NewPassword = newPassword };
            await _api.PostAsync<ResetPasswordResponse>($"/api/users/{SelectedUser.Id}/reset-password", request);
            MessageBox.Show(
                $"Пароль сброшен. Новый пароль: {newPassword}\nСообщите его пользователю.",
                "Пароль сброшен", MessageBoxButton.OK, MessageBoxImage.Information);
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
        if (SelectedUser == null) return;

        var confirm = MessageBox.Show(
            $"Удалить пользователя «{SelectedUser.Login}»? Это действие необратимо.",
            "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.DeleteAsync($"/api/users/{SelectedUser.Id}");
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
