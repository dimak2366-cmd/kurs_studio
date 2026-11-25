using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Client.Services;
using Shared.Dtos;

namespace Client.Views.Accountant;

public partial class ActualExpenseEditDialog : Window
{
    private readonly ApiClient _api;
    private readonly ActualExpenseDto? _existing;
    private readonly ObservableCollection<SmetaDto> _smetas = new();

    public ActualExpenseEditDialog(ActualExpenseDto? existing, ApiClient api)
    {
        InitializeComponent();
        _api = api;
        _existing = existing;

        DatePicker.SelectedDate = DateTime.UtcNow.Date;
        DocDatePicker.SelectedDate = DateTime.UtcNow.Date;
        DocTypeCombo.SelectedIndex = 0;

        if (existing != null)
        {
            HeaderText.Text = $"Корректировка расхода #{existing.Id}";
            // Смета — заблокирована, показываем текущую
            SmetaPanel.Visibility = Visibility.Visible;
            AmountBox.Text = existing.Amount.ToString("F2");
            QuantityBox.Text = existing.Quantity.ToString("F3");
            DatePicker.SelectedDate = existing.Date.Date;
            DocNumberBox.Text = existing.DocumentNumber;
            DocDatePicker.SelectedDate = existing.DocumentDate.Date;
            if (!string.IsNullOrEmpty(existing.DocumentType))
            {
                DocTypeCombo.SelectedValue = existing.DocumentType;
                DocTypeCombo.Text = existing.DocumentType;
            }
        }

        if (existing == null)
            QuantityBox.Text = "1";

        _ = LoadSmetasAsync(existing?.SmetaId);
    }

    private async Task LoadSmetasAsync(int? selectedSmetaId)
    {
        try
        {
            var list = await _api.GetAsync<List<SmetaDto>>("/api/smeta");
            _smetas.Clear();
            foreach (var s in list) _smetas.Add(s);
            SmetaCombo.ItemsSource = _smetas;

            if (selectedSmetaId.HasValue)
            {
                SmetaCombo.SelectedValue = selectedSmetaId.Value;
                SmetaCombo.IsEnabled = false;
            }
            else if (_smetas.Count > 0)
            {
                SmetaCombo.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Ошибка загрузки смет: " + ex.Message;
        }
    }

    private async void SmetaCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SmetaCombo.SelectedValue is not int smetaId) return;
        if (_existing != null) return; // при редактировании статью не меняем

        try
        {
            var detail = await _api.GetAsync<SmetaDetailDto>($"/api/smeta/{smetaId}");
            ItemCombo.ItemsSource = detail.Items;
            if (detail.Items.Count > 0) ItemCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Ошибка загрузки статей: " + ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (!decimal.TryParse(QuantityBox.Text, out var qty) || qty <= 0)
        {
            ErrorText.Text = "Укажите корректное количество (положительное число).";
            return;
        }
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
        {
            ErrorText.Text = "Укажите корректную сумму (положительное число).";
            return;
        }
        if (Math.Round(amount, 2) != amount)
        {
            ErrorText.Text = "Сумма может содержать не более 2 знаков после запятой.";
            return;
        }
        if (DatePicker.SelectedDate is not DateTime date)
        {
            ErrorText.Text = "Укажите дату расхода.";
            return;
        }
        if (string.IsNullOrWhiteSpace(DocNumberBox.Text))
        {
            ErrorText.Text = "Укажите номер документа.";
            return;
        }
        if (DocDatePicker.SelectedDate is not DateTime docDate)
        {
            ErrorText.Text = "Укажите дату документа.";
            return;
        }
        var docType = (DocTypeCombo.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(docType))
        {
            ErrorText.Text = "Укажите тип документа.";
            return;
        }

        try
        {
            if (_existing == null)
            {
                if (ItemCombo.SelectedValue is not int itemId)
                {
                    ErrorText.Text = "Выберите статью расходов.";
                    return;
                }

                var req = new CreateActualExpenseRequest
                {
                    ExpenseItemId = itemId,
                    Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                    Amount = amount,
                    Quantity = qty,
                    DocumentType = docType,
                    DocumentNumber = DocNumberBox.Text.Trim(),
                    DocumentDate = DateTime.SpecifyKind(docDate, DateTimeKind.Utc)
                };
                await _api.PostAsync<ActualExpenseDto>("/api/expenses", req);
            }
            else
            {
                var req = new UpdateActualExpenseRequest
                {
                    Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                    Amount = amount,
                    Quantity = qty,
                    DocumentType = docType,
                    DocumentNumber = DocNumberBox.Text.Trim(),
                    DocumentDate = DateTime.SpecifyKind(docDate, DateTimeKind.Utc)
                };
                await _api.PutAsync<ActualExpenseDto>($"/api/expenses/{_existing.Id}", req);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
