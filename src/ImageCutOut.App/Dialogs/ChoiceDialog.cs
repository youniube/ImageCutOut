using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ImageCutOut.Dialogs;

public sealed record DialogChoice(string Text, string Result, bool IsDefault = false);

public sealed class ChoiceDialog : Window
{
    private ChoiceDialog(string title, string message, IReadOnlyList<DialogChoice> choices)
    {
        Title = title;
        Width = 440;
        MinWidth = 360;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        Button? defaultButton = null;
        foreach (DialogChoice choice in choices)
        {
            var button = new Button
            {
                Content = choice.Text,
                MinWidth = 84,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            string result = choice.Result;
            button.Click += (_, _) => Close(result);
            buttonPanel.Children.Add(button);
            if (choice.IsDefault)
            {
                defaultButton = button;
            }
        }

        var contentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 24
        };
        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15,
            MaxWidth = 520
        };
        Grid.SetRow(messageBlock, 0);
        Grid.SetRow(buttonPanel, 1);
        contentGrid.Children.Add(messageBlock);
        contentGrid.Children.Add(buttonPanel);

        Content = new Border
        {
            Padding = new Thickness(24),
            Child = contentGrid
        };

        Opened += (_, _) => (defaultButton ?? buttonPanel.Children.OfType<Button>().FirstOrDefault())?.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && defaultButton is not null)
            {
                defaultButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
            }
        };
    }

    public static Task<string?> ShowAsync(
        Window owner,
        string title,
        string message,
        params DialogChoice[] choices)
    {
        var dialog = new ChoiceDialog(title, message, choices);
        return dialog.ShowDialog<string?>(owner);
    }

    public static Task<string?> ShowErrorAsync(Window owner, string message) =>
        ShowAsync(owner, "无法完成操作", message, new DialogChoice("确定", "ok", true));
}
