using System.Windows.Input;
using Client.Services;
using Shared.Dtos;

namespace Client.ViewModels;

/// <summary>
/// VM окна входа. Хранит логин, пароль, сообщение об ошибке.
/// После успешной аутентификации вызывает событие LoginSucceeded.
/// </summary>
public sealed class LoginViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SessionService _session;

    private string _login = "admin";
    private string _password = "Admin123!";
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public LoginViewModel(ApiClient api, SessionService session)
    {
        _api = api;
        _session = session;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
    }

    public string Login
    {
        get => _login;
        set => SetProperty(ref _login, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand LoginCommand { get; }

    public event EventHandler<LoginResponse>? LoginSucceeded;

    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введите логин и пароль.";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await _api.LoginAsync(Login.Trim(), Password);
            _session.SetSession(response);
            LoginSucceeded?.Invoke(this, response);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось соединиться с сервером: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
