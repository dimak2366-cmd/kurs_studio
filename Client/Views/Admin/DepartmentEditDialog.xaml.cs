using System.Windows;
using Shared.Dtos;

namespace Client.Views.Admin;

public partial class DepartmentEditDialog : Window
{
    private readonly DepartmentDto? _existing;

    public DepartmentEditDialog(DepartmentDto? existing)
    {
        InitializeComponent();
        _existing = existing;

        if (existing != null)
        {
            HeaderText.Text = "Редактирование отдела";
            NameBox.Text = existing.Name;
        }
    }

    public string ResultName { get; private set; } = string.Empty;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Введите название отдела.";
            return;
        }

        ResultName = NameBox.Text.Trim();
        DialogResult = true;
        Close();
    }
}
