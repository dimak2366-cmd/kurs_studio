using Client.ViewModels;

namespace Client.Views;

/// <summary>VM-заглушка: содержит заголовок и описание экрана, который будет реализован дальше.</summary>
public sealed class PlaceholderViewModel : ViewModelBase
{
    public PlaceholderViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }
    public string Description { get; }
}
