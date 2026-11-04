using System.Windows;

namespace Client.Views.Economist;

public partial class RejectReasonDialog : Window
{
    public RejectReasonDialog(string prompt)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        Loaded += (_, _) => ReasonBox.Focus();
    }

    public string Reason { get; private set; } = string.Empty;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            ErrorText.Text = "Причина обязательна.";
            return;
        }
        Reason = ReasonBox.Text.Trim();
        DialogResult = true;
        Close();
    }
}
