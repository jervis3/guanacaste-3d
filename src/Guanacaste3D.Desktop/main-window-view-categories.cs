using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Routes the Visibility/Graphics command of the View ribbon.</summary>
    private bool TryExecuteViewCategoryCommand(string commandId)
    {
        switch (commandId)
        {
            case "VisibilityGraphics": ShowViewCategoryDialog(); return true;
            default: return false;
        }
    }

    /// <summary>Lets the user choose which categories the active view shows; the rest stay hidden in it.</summary>
    private void ShowViewCategoryDialog()
    {
        if (ActiveProjectView() is not { } view)
        {
            StatusText.Text = GetText("Status.NoViewForCategories");
            return;
        }

        var boxes = Enum.GetValues<ViewCategory>().ToDictionary(category => category, category => new CheckBox
        {
            Content = GetText("ViewCategory." + category),
            IsChecked = !view.HiddenCategories.Contains(category),
            Margin = new Thickness(0, 2, 0, 2)
        });

        var window = new Window
        {
            Title = GetText("Dialog.ViewCategories") + " · " + view.Name,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var accept = new Button { Content = GetText("Prompt.Accept"), IsDefault = true, Width = 90, Margin = new Thickness(0, 12, 8, 0) };
        var cancel = new Button { Content = GetText("Prompt.Cancel"), IsCancel = true, Width = 90, Margin = new Thickness(0, 12, 0, 0) };
        accept.Click += (_, _) => window.DialogResult = true;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(accept);
        buttons.Children.Add(cancel);
        var layout = new StackPanel { Margin = new Thickness(16) };
        layout.Children.Add(new TextBlock { Text = GetText("Dialog.ViewCategories.Hint"), Margin = new Thickness(0, 0, 0, 8) });
        foreach (var box in boxes.Values) layout.Children.Add(box);
        layout.Children.Add(buttons);
        window.Content = new ScrollViewer { Content = layout, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        if (window.ShowDialog() != true) return;
        var hidden = boxes.Where(entry => entry.Value.IsChecked != true).Select(entry => entry.Key).ToList();
        _session.SetViewHiddenCategories(view.Id, hidden);
        ApplyViewVisibility();
        StatusText.Text = string.Format(GetText("Status.ViewCategoriesChanged"), hidden.Count);
    }
}
