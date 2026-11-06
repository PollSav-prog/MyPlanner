using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MyPlanner.Models;
using MyPlanner.Services;

namespace MyPlanner.Views;

/// <summary>Кнопка «Повторение» на правой панели: настройка повторения выбранной задачи.</summary>
public class RepeatDialog : Window
{
    public RepeatDialog(TaskItem task)
    {
        Title = Loc.I["rep_title"];
        Width = 400;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        SetResourceReference(BackgroundProperty, "AppBg");
        SetResourceReference(ForegroundProperty, "FgPrimary");

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = task.Title, FontWeight = FontWeights.SemiBold, FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 12) });
        var rp = new RepeatPanel();
        rp.Load(task);
        panel.Children.Add(rp);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = Dialogs.MakeButton(Loc.I["btn_cancel"]);
        cancel.IsCancel = true;
        var ok = Dialogs.MakeButton(Loc.I["btn_save"], true);
        ok.IsDefault = true;
        ok.Click += (_, _) =>
        {
            if (!rp.Apply(task, out var err)) { Dialogs.Info(err, this); return; }
            DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        Content = panel;
    }
}
