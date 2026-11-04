using System.Collections.ObjectModel;
using System.Windows;
using Shared.Dtos;

namespace Client.Views.Admin;

public partial class ProjectEditDialog : Window
{
    private readonly ProjectDto? _existing;

    public ProjectEditDialog(ProjectDto? existing, ObservableCollection<DepartmentDto> departments)
    {
        InitializeComponent();
        _existing = existing;
        DepartmentCombo.ItemsSource = departments;

        if (existing == null)
        {
            HeaderText.Text = "Новый проект";
            StartDatePicker.SelectedDate = DateTime.UtcNow.Date;
            EndDatePicker.SelectedDate = DateTime.UtcNow.Date.AddMonths(6);
        }
        else
        {
            HeaderText.Text = "Редактирование проекта";
            CodeBox.Text = existing.Code;
            NameBox.Text = existing.Name;
            DepartmentCombo.SelectedValue = existing.DepartmentId;
            StartDatePicker.SelectedDate = existing.StartDate.Date;
            EndDatePicker.SelectedDate = existing.EndDate.Date;
        }
    }

    public string ResultCode { get; private set; } = string.Empty;
    public string ResultName { get; private set; } = string.Empty;
    public int ResultDepartmentId { get; private set; }
    public DateTime ResultStartDate { get; private set; }
    public DateTime ResultEndDate { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(CodeBox.Text))
        {
            ErrorText.Text = "Введите код проекта.";
            return;
        }
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Введите название проекта.";
            return;
        }
        if (DepartmentCombo.SelectedValue is not int depId)
        {
            ErrorText.Text = "Выберите отдел.";
            return;
        }
        if (StartDatePicker.SelectedDate is not DateTime start)
        {
            ErrorText.Text = "Укажите дату начала.";
            return;
        }
        if (EndDatePicker.SelectedDate is not DateTime end)
        {
            ErrorText.Text = "Укажите дату окончания.";
            return;
        }
        if (end <= start)
        {
            ErrorText.Text = "Дата окончания должна быть позже даты начала.";
            return;
        }

        ResultCode = CodeBox.Text.Trim();
        ResultName = NameBox.Text.Trim();
        ResultDepartmentId = depId;
        ResultStartDate = start;
        ResultEndDate = end;

        DialogResult = true;
        Close();
    }
}
