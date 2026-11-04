using System.Windows;
using Client.ViewModels;
using Client.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Client;

public partial class MainWindow : Window
{
    private readonly LoginViewModel _vm;

    public MainWindow(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.LoginSucceeded += OnLoginSucceeded;
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is System.Windows.Controls.PasswordBox pb)
        {
            vm.Password = pb.Password;
        }
    }

    private void OnLoginSucceeded(object? sender, Shared.Dtos.LoginResponse response)
    {
        // Открываем шелл и закрываем окно входа.
        var shellVm = App.Services.GetRequiredService<MainShellViewModel>();
        var shell = new MainShell(shellVm);
        shell.Show();
        this.Close();
    }
}
