using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MyPlanner.Models;
using MyPlanner.Services;

namespace MyPlanner.Views;

/// <summary>
/// Голосовое создание задачи: говорим → распознаём → показываем результат → подтверждаем (при желании голосом) → сохраняем.
/// Текст можно и напечатать: так работает и без микрофона.
/// </summary>
public class VoiceWindow : Window
{
    private readonly DateTime _fallbackDate;
    private readonly ComboBox _lang = new() { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.8, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBox _phrase = new() { AcceptsReturn = false, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _mic;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _result = new() { Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _rTitle = new() { FontWeight = FontWeights.SemiBold, FontSize = 16, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _rWhen = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _rRepeat = new() { Margin = new Thickness(0, 2, 0, 0), Opacity = 0.8 };
    private readonly StackPanel _ambig = new() { Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Button _save, _fix, _speak;
    private ParsedTask? _parsed;
    private bool _listening;

    public VoiceWindow(DateTime selectedDate)
    {
        _fallbackDate = selectedDate;
        Title = Loc.I["voice_title"];
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        SetResourceReference(BackgroundProperty, "AppBg");
        SetResourceReference(ForegroundProperty, "FgPrimary");

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(Cap(Loc.I["voice_lang"]));
        foreach (var l in Loc.I.Languages)
            _lang.Items.Add(new ComboBoxItem { Content = Dialogs.LanguageHeader(l), Tag = l.Code });
        var cur = string.IsNullOrEmpty(Svc.Settings.SpeechLanguage) ? Loc.I.Current : Svc.Settings.SpeechLanguage;
        foreach (ComboBoxItem it in _lang.Items) if ((string)it.Tag == cur) _lang.SelectedItem = it;
        if (_lang.SelectedItem == null && _lang.Items.Count > 0) _lang.SelectedIndex = 0;
        _lang.SelectionChanged += (_, _) =>
        {
            Svc.Settings.SpeechLanguage = SpeechCode;   // язык речи выбирается отдельно от языка интерфейса
            UpdateInfo();
        };
        root.Children.Add(_lang);
        root.Children.Add(_info);

        root.Children.Add(Cap(Loc.I["voice_say"]));
        _mic = Dialogs.MakeButton(Loc.I["btn_speak"], true, 120);
        _mic.Margin = new Thickness(0, 4, 0, 0);
        _mic.HorizontalAlignment = HorizontalAlignment.Left;
        _mic.Click += async (_, _) => await ListenAsync();
        root.Children.Add(_mic);

        root.Children.Add(Cap(Loc.I["voice_or_type"]));
        root.Children.Add(_phrase);
        var parse = Dialogs.MakeButton(Loc.I["btn_parse"]);
        parse.Margin = new Thickness(0, 8, 0, 0);
        parse.HorizontalAlignment = HorizontalAlignment.Left;
        parse.Click += (_, _) => ParseText();
        _phrase.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) ParseText(); };
        root.Children.Add(parse);
        root.Children.Add(_status);

        // результат распознавания
        var card = new Border { Padding = new Thickness(14), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "CardBg");
        card.SetResourceReference(Border.BorderBrushProperty, "Line");
        var cardBody = new StackPanel();
        cardBody.Children.Add(_rTitle);
        cardBody.Children.Add(_rWhen);
        cardBody.Children.Add(_rRepeat);
        cardBody.Children.Add(_ambig);
        card.Child = cardBody;
        _result.Children.Add(card);

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        _speak = Dialogs.MakeButton(Loc.I["btn_speak_back"], false, 0);
        _speak.Click += (_, _) => SpeakConfirm();
        _fix = Dialogs.MakeButton(Loc.I["btn_fix"], false, 0);
        _fix.Click += (_, _) => FixInDialog();
        _save = Dialogs.MakeButton(Loc.I["btn_save"], true, 0);
        _save.Click += (_, _) => SaveParsed();
        var cancel = Dialogs.MakeButton(Loc.I["btn_cancel"], false, 0);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(_speak);
        buttons.Children.Add(_fix);
        buttons.Children.Add(_save);
        buttons.Children.Add(cancel);
        _result.Children.Add(buttons);
        root.Children.Add(_result);

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        UpdateInfo();
        Closed += (_, _) => Svc.Speech.Stop();
    }

    private static TextBlock Cap(string t) => new() { Text = t, Margin = new Thickness(0, 12, 0, 0), Opacity = 0.75, FontSize = 12 };

    private string SpeechCode => (_lang.SelectedItem as ComboBoxItem)?.Tag as string ?? Loc.I.Current;

    private void UpdateInfo() =>
        _info.Text = Svc.Speech.HasLocalRecognizer(SpeechCode) ? Loc.I["speech_local"] : Loc.I["speech_online"];

    // ---------- Распознавание ----------
    private async Task ListenAsync()
    {
        if (_listening) return;
        _listening = true;
        _mic.IsEnabled = false;
        _status.Text = Loc.I["voice_listening"];
        try
        {
            var res = await Svc.Speech.ListenAsync(SpeechCode);
            if (res.Ok)
            {
                _phrase.Text = res.Text;
                _status.Text = Loc.I["voice_heard"];
                ParseText();
            }
            else
            {
                _status.Text = Loc.I[res.ErrorKey] + " " + Loc.I["voice_type_instead"];
            }
        }
        catch (Exception ex)
        {
            Logger.Write("Listen failed", ex);
            _status.Text = Loc.I["speech_unsupported"] + " " + Loc.I["voice_type_instead"];
        }
        finally
        {
            _listening = false;
            _mic.IsEnabled = true;
        }
    }

    // ---------- Разбор ----------
    private void ParseText()
    {
        var text = _phrase.Text.Trim();
        if (text.Length == 0) { _status.Text = Loc.I["voice_empty"]; return; }
        var p = NaturalDateParser.Parse(text, SpeechCode, DateTime.Now);
        if (string.IsNullOrWhiteSpace(p.Title)) { _status.Text = Loc.I["voice_no_title"]; _result.Visibility = Visibility.Collapsed; return; }
        if (p.Date == null) p.Date = _fallbackDate.Date;
        _parsed = p;
        _status.Text = "";
        ShowResult();
        if (!p.AmbiguousHour && Svc.Settings.TtsEnabled) SpeakConfirm();
    }

    private void ShowResult()
    {
        var p = _parsed!;
        _result.Visibility = Visibility.Visible;
        _rTitle.Text = p.Title;
        var due = p.Due;
        _rWhen.Text = due.ToString("D", Loc.I.Culture) + ", " + due.ToString("t", Loc.I.Culture)
                      + (p.HasTime ? "" : "  (" + Loc.I["voice_default_time"] + ")");
        _rRepeat.Text = p.Repeat == RepeatKind.None ? "" : Loc.I["lbl_repeat"] + ": " + Recurrence.Describe(ToTask(p));
        _rRepeat.Visibility = p.Repeat == RepeatKind.None ? Visibility.Collapsed : Visibility.Visible;

        _ambig.Children.Clear();
        if (p.AmbiguousHour && p.Time is TimeSpan t)
        {
            _ambig.Visibility = Visibility.Visible;
            _ambig.Children.Add(new TextBlock { Text = Loc.I["voice_ambiguous"], TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            int h12 = t.Hours % 12;
            var am = new DateTime(2000, 1, 1, h12, t.Minutes, 0).ToString("t", Loc.I.Culture);
            var pm = new DateTime(2000, 1, 1, h12 + 12, t.Minutes, 0).ToString("t", Loc.I.Culture);
            var bAm = Dialogs.MakeButton(am, false, 80); bAm.Margin = new Thickness(0, 0, 8, 0);
            var bPm = Dialogs.MakeButton(pm, false, 80); bPm.Margin = new Thickness(0, 0, 8, 0);
            bAm.Click += (_, _) => { NaturalDateParser.ApplyHour(_parsed!, false); ShowResult(); if (Svc.Settings.TtsEnabled) SpeakConfirm(); };
            bPm.Click += (_, _) => { NaturalDateParser.ApplyHour(_parsed!, true); ShowResult(); if (Svc.Settings.TtsEnabled) SpeakConfirm(); };
            row.Children.Add(bAm);
            row.Children.Add(bPm);
            _ambig.Children.Add(row);
        }
        else _ambig.Visibility = Visibility.Collapsed;
        _save.IsEnabled = !p.AmbiguousHour;
    }

    private static TaskItem ToTask(ParsedTask p) => new()
    {
        Title = p.Title,
        Due = p.Due,
        Repeat = p.Repeat,
        RepeatInterval = p.RepeatInterval,
        RepeatDays = p.RepeatDays
    };

    // ---------- Озвучивание ----------
    public static string BuildConfirmText(ParsedTask p, string code)
    {
        var due = p.Due;
        string dateText = due.Date == DateTime.Today ? Loc.I.In(code, "rel_today")
            : due.Date == DateTime.Today.AddDays(1) ? Loc.I.In(code, "rel_tomorrow")
            : due.ToString("D", new CultureInfo(code));
        return Loc.I.In(code, "voice_confirm", p.Title, dateText, due.ToString("t", new CultureInfo(code)));
    }

    private void SpeakConfirm()
    {
        if (_parsed == null) return;
        var code = SpeechCode;
        if (!Svc.Speech.Speak(BuildConfirmText(_parsed, code), code))
            _status.Text = Loc.I["tts_no_voice"];
    }

    // ---------- Сохранение / исправление ----------
    private void SaveParsed()
    {
        if (_parsed == null) return;
        var t = ToTask(_parsed);
        TaskOps.Save(t);
        Svc.Main?.ViewModel.SelectDate(t.Due.Date, t);
        DialogResult = true;
    }

    private void FixInDialog()
    {
        if (_parsed == null) return;
        var t = ToTask(_parsed);
        var dlg = new TaskDialog(t) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            Svc.Main?.ViewModel.SelectDate(t.Due.Date, t);
            DialogResult = true;
        }
    }
}
