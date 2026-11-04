using System.Collections.ObjectModel;
using System.Windows;
using Shared.Dtos;

namespace Client.Views.Economist;

/// <summary>
/// Диалог редактирования плановых сумм существующей сметы. Разрешено менять
/// PlanAmount, LimitAmount, Quantity, Price. Остальное — только просмотр.
/// </summary>
public partial class SmetaEditDialog : Window
{
    private readonly SmetaDetailDto _smeta;
    private readonly ObservableCollection<UpdateSmetaItemPlanRequest> _items = new();

    public SmetaEditDialog(SmetaDetailDto smeta)
    {
        InitializeComponent();
        _smeta = smeta;

        HeaderText.Text = $"Редактирование сметы #{smeta.Id} — {smeta.FormCode} ({smeta.Year})";
        InfoText.Text = $"Проект: {smeta.ProjectCode} — {smeta.ProjectName}. Статус: {smeta.Status}.";

        foreach (var item in smeta.Items)
        {
            _items.Add(new UpdateSmetaItemPlanRequest
            {
                Id = item.Id,
                PlanAmount = item.PlanAmount,
                LimitAmount = item.LimitAmount,
                Quantity = item.Quantity,
                Price = item.Price
            });
        }

        // Показываем наименование из исходного DTO, но редактируем только то, что нужно.
        // Для этого скомбинируем в отдельный отображаемый тип.
        var display = smeta.Items.Select(i => new EditableSmetaItem
        {
            Id = i.Id,
            Name = i.Name,
            KosguCode = i.KosguCode,
            PlanAmount = i.PlanAmount,
            LimitAmount = i.LimitAmount,
            Quantity = i.Quantity,
            Price = i.Price
        }).ToList();

        ItemsGrid.ItemsSource = display;
    }

    public UpdateSmetaPlanRequest Result { get; private set; } = new();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        var display = ItemsGrid.ItemsSource as List<EditableSmetaItem>;
        if (display == null) return;

        foreach (var item in display)
        {
            if (item.PlanAmount < 0 || item.LimitAmount < 0 || item.Quantity < 0 || item.Price < 0)
            {
                ErrorText.Text = "Суммы и количества не могут быть отрицательными.";
                return;
            }
        }

        Result = new UpdateSmetaPlanRequest
        {
            Items = display.Select(d => new UpdateSmetaItemPlanRequest
            {
                Id = d.Id,
                PlanAmount = d.PlanAmount,
                LimitAmount = d.LimitAmount,
                Quantity = d.Quantity,
                Price = d.Price
            }).ToList()
        };

        DialogResult = true;
        Close();
    }
}

/// <summary>Вспомогательный класс для отображения в DataGrid: наименование + редактируемые суммы.</summary>
public sealed class EditableSmetaItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public decimal PlanAmount { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}
