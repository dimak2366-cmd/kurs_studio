using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Client.Services;
using Shared.Dtos;

namespace Client.Views.Head;

public partial class CreateRequestDialog : Window
{
    private readonly ApiClient _api;

    public CreateRequestDialog(ObservableCollection<ProjectDto> projects, ApiClient api)
    {
        InitializeComponent();
        _api = api;
        ProjectCombo.ItemsSource = projects;
        if (projects.Count > 0)
        {
            ProjectCombo.SelectedIndex = 0;
        }
    }

    private async void ProjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectCombo.SelectedValue is not int projectId) return;

        ErrorText.Text = string.Empty;
        StatusText.Text = "Загрузка статей...";
        ItemCombo.ItemsSource = null;

        try
        {
            var items = await _api.GetAsync<List<ExpenseItemDto>>($"/api/smeta/items/by-project/{projectId}");
            ItemCombo.ItemsSource = items;
            if (items.Count > 0) ItemCombo.SelectedIndex = 0;
            StatusText.Text = $"Доступно статей: {items.Count}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Ошибка загрузки статей: " + ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (ProjectCombo.SelectedValue is not int projectId)
        {
            ErrorText.Text = "Выберите проект.";
            return;
        }
        if (ItemCombo.SelectedValue is not int itemId)
        {
            ErrorText.Text = "Выберите статью расходов.";
            return;
        }
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
        {
            ErrorText.Text = "Укажите корректную сумму (положительное число).";
            return;
        }

        try
        {
            var req = new CreateRequestRequest
            {
                ProjectId = projectId,
                ExpenseItemId = itemId,
                Amount = amount,
                Comment = string.IsNullOrWhiteSpace(CommentBox.Text) ? null : CommentBox.Text.Trim()
            };
            await _api.PostAsync<ExpenseRequestDto>("/api/requests", req);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
