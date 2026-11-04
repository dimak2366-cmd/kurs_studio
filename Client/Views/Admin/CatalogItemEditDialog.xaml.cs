using System.Windows;
using Shared.Dtos;

namespace Client.Views.Admin;

public partial class CatalogItemEditDialog : Window
{
    private readonly CatalogExpenseItemDto? _existing;

    public CatalogItemEditDialog(CatalogExpenseItemDto? existing)
    {
        InitializeComponent();
        _existing = existing;

        if (existing != null)
        {
            HeaderText.Text = "Редактирование элемента справочника";
            NameBox.Text = existing.Name;
            KosguBox.Text = existing.KosguCode;
            IsActiveCheck.IsChecked = existing.IsActive;
        }
    }

    public string ResultName { get; private set; } = string.Empty;
    public string ResultKosguCode { get; private set; } = string.Empty;
    public bool ResultIsActive { get; private set; } = true;

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        var name = (NameBox.Text ?? string.Empty).Trim();
        var code = (KosguBox.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "Введите наименование.";
            return;
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            ErrorText.Text = "Введите код КОСГУ.";
            return;
        }
        if (code.Length > 16)
        {
            ErrorText.Text = "Код КОСГУ не должен превышать 16 символов.";
            return;
        }

        ResultName = name;
        ResultKosguCode = code;
        ResultIsActive = IsActiveCheck.IsChecked == true;

        DialogResult = true;
        Close();
    }
}
