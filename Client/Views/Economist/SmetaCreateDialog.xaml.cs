using System.Collections.ObjectModel;
using System.Windows;
using Shared.Dtos;

namespace Client.Views.Economist;

public partial class SmetaCreateDialog : Window
{
    private readonly ObservableCollection<CreateSmetaItemRequest> _items = new();

    public SmetaCreateDialog(ObservableCollection<ProjectDto> projects)
    {
        InitializeComponent();
        ProjectCombo.ItemsSource = projects;
        YearBox.Text = DateTime.UtcNow.Year.ToString();
        ItemsGrid.ItemsSource = _items;

        // Добавим одну пустую строку по умолчанию, чтобы пользователь видел, что нужно вводить.
        _items.Add(new CreateSmetaItemRequest { Name = string.Empty, KosguCode = string.Empty, PlanAmount = 0, LimitAmount = 0, Quantity = 0, Price = 0 });
    }

    public CreateSmetaRequest Result { get; private set; } = new();

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        _items.Add(new CreateSmetaItemRequest { Name = string.Empty, KosguCode = string.Empty });
    }

    private void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is CreateSmetaItemRequest item)
        {
            _items.Remove(item);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (ProjectCombo.SelectedValue is not int projectId)
        {
            ErrorText.Text = "Выберите проект.";
            return;
        }
        if (string.IsNullOrWhiteSpace(FormCodeBox.Text))
        {
            ErrorText.Text = "Укажите форму сметы.";
            return;
        }
        if (!int.TryParse(YearBox.Text, out var year) || year < 2000 || year > 2100)
        {
            ErrorText.Text = "Некорректный год (2000–2100).";
            return;
        }
        if (_items.Count == 0)
        {
            ErrorText.Text = "Добавьте хотя бы одну статью.";
            return;
        }
        foreach (var item in _items)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.KosguCode))
            {
                ErrorText.Text = "У каждой статьи должны быть заполнены Наименование и КОСГУ.";
                return;
            }
            if (item.PlanAmount < 0 || item.LimitAmount < 0 || item.Quantity < 0 || item.Price < 0)
            {
                ErrorText.Text = "Суммы и количества не могут быть отрицательными.";
                return;
            }
        }

        Result = new CreateSmetaRequest
        {
            ProjectId = projectId,
            FormCode = FormCodeBox.Text.Trim(),
            Year = year,
            Items = _items.ToList()
        };

        DialogResult = true;
        Close();
    }
}
