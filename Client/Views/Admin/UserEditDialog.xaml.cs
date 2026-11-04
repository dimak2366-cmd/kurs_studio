using System.Collections.ObjectModel;
using System.Windows;
using Shared.Dtos;

namespace Client.Views.Admin;

/// <summary>
/// Диалог создания/редактирования пользователя. Режим определяется тем,
/// передан ли <paramref name="existingUser"/>: null — создание, иначе — редактирование.
/// </summary>
public partial class UserEditDialog : Window
{
    private readonly UserDto? _existing;

    public UserEditDialog(
        UserDto? existingUser,
        ObservableCollection<RoleDto> roles,
        ObservableCollection<DepartmentDto> departments)
    {
        InitializeComponent();
        _existing = existingUser;

        RoleCombo.ItemsSource = roles;
        DepartmentCombo.ItemsSource = departments;

        if (existingUser == null)
        {
            HeaderText.Text = "Новый пользователь";
            LoginPanel.Visibility = Visibility.Visible;
            PasswordPanel.Visibility = Visibility.Visible;
        }
        else
        {
            HeaderText.Text = "Редактирование пользователя";
            LoginPanel.Visibility = Visibility.Collapsed;
            PasswordPanel.Visibility = Visibility.Collapsed;

            LoginBox.Text = existingUser.Login;
            FullNameBox.Text = existingUser.FullName;
            RoleCombo.SelectedValue = existingUser.RoleId;
            DepartmentCombo.SelectedValue = existingUser.DepartmentId;
        }
    }

    public string ResultLogin { get; private set; } = string.Empty;
    public string ResultPassword { get; private set; } = string.Empty;
    public string ResultFullName { get; private set; } = string.Empty;
    public int ResultRoleId { get; private set; }
    public int? ResultDepartmentId { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(FullNameBox.Text))
        {
            ErrorText.Text = "Введите ФИО.";
            return;
        }

        if (RoleCombo.SelectedValue is not int roleId)
        {
            ErrorText.Text = "Выберите роль.";
            return;
        }

        if (_existing == null)
        {
            if (string.IsNullOrWhiteSpace(LoginBox.Text) || LoginBox.Text.Trim().Length < 3)
            {
                ErrorText.Text = "Логин обязателен, минимум 3 символа.";
                return;
            }
            if (string.IsNullOrWhiteSpace(PasswordInput.Password) || PasswordInput.Password.Length < 6)
            {
                ErrorText.Text = "Пароль обязателен, минимум 6 символов.";
                return;
            }
            ResultLogin = LoginBox.Text.Trim();
            ResultPassword = PasswordInput.Password;
        }

        ResultFullName = FullNameBox.Text.Trim();
        ResultRoleId = roleId;
        ResultDepartmentId = DepartmentCombo.SelectedValue as int?;

        DialogResult = true;
        Close();
    }
}
