using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Views;

public partial class MainShell : Window
{
    private readonly MainShellViewModel _vm;

    public MainShell(MainShellViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.LogoutRequested += OnLogoutRequested;
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        var loginWindow = App.Services.GetRequiredService<MainWindow>();
        loginWindow.Show();
        this.Close();
    }
}
