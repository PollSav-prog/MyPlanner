using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace MyPlanner.Services;

public record SpeechResult(bool Ok, string Text, string ErrorKey, bool Online);

/// <summary>
/// Распознавание речи: сначала локальное (установленные в Windows распознаватели речи),
/// если для языка его нет — онлайн-распознавание Windows (нужен интернет и разрешение
/// «Распознавание голоса» в параметрах конфиденциальности Windows).
/// Синтез речи — установленные в Windows голоса.
/// </summary>
public sealed class SpeechService
{
    private SpeechSynthesizer? _tts;

    // ---------------- Распознавание ----------------
    public bool HasLocalRecognizer(string culture)
    {
        try { return FindRecognizer(culture) != null; } catch { return false; }
    }

    private static RecognizerInfo? FindRecognizer(string culture)
    {
        var all = SpeechRecognitionEngine.InstalledRecognizers();
        return all.FirstOrDefault(r => r.Culture.Name.Equals(culture, StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName.Equals(
                   new CultureInfo(culture).TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SpeechResult> ListenAsync(string culture, CancellationToken ct = default)
    {
        // 1) локальное распознавание
        try
        {
            var info = FindRecognizer(culture);
            if (info != null)
            {
                var text = await Task.Run(() =>
                {
                    using var engine = new SpeechRecognitionEngine(info);
                    engine.LoadGrammar(new DictationGrammar());
                    engine.InitialSilenceTimeout = TimeSpan.FromSeconds(7);
                    engine.BabbleTimeout = TimeSpan.FromSeconds(4);
                    engine.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
                    engine.EndSilenceTimeoutAmbiguous = TimeSpan.FromSeconds(1.6);
                    engine.SetInputToDefaultAudioDevice();
                    var res = engine.Recognize(TimeSpan.FromSeconds(15));
                    return res?.Text ?? "";
                }, ct);
                return string.IsNullOrWhiteSpace(text)
                    ? new SpeechResult(false, "", "speech_nothing", false)
                    : new SpeechResult(true, text, "", false);
            }
        }
        catch (InvalidOperationException)
        {
            return new SpeechResult(false, "", "speech_no_mic", false); // нет микрофона / устройства ввода
        }
        catch (Exception ex)
        {
            Logger.Write("Local speech failed", ex);
        }

        // 2) онлайн-распознавание Windows
        return await ListenOnlineAsync(culture);
    }

    private static async Task<SpeechResult> ListenOnlineAsync(string culture)
    {
        try
        {
            using var rec = new Windows.Media.SpeechRecognition.SpeechRecognizer(new Windows.Globalization.Language(culture));
            rec.Constraints.Add(new Windows.Media.SpeechRecognition.SpeechRecognitionTopicConstraint(
                Windows.Media.SpeechRecognition.SpeechRecognitionScenario.Dictation, "dictation"));
            var compile = await rec.CompileConstraintsAsync();
            if (compile.Status != Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success)
                return new SpeechResult(false, "", "speech_unsupported", true);
            rec.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(7);
            rec.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.5);
            var res = await rec.RecognizeAsync();
            if (res.Status == Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success
                && !string.IsNullOrWhiteSpace(res.Text))
                return new SpeechResult(true, res.Text, "", true);
            return res.Status switch
            {
                Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.NetworkFailure => new SpeechResult(false, "", "speech_network", true),
                Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.MicrophoneUnavailable => new SpeechResult(false, "", "speech_no_mic", true),
                _ => new SpeechResult(false, "", "speech_nothing", true)
            };
        }
        catch (Exception ex) when (unchecked((uint)ex.HResult) == 0x80045509)
        {
            return new SpeechResult(false, "", "speech_policy", true); // выключено «Распознавание голоса» в Windows
        }
        catch (Exception ex) when (unchecked((uint)ex.HResult) == 0x8004503A || ex is ArgumentException)
        {
            return new SpeechResult(false, "", "speech_unsupported", true); // язык не поддерживается
        }
        catch (UnauthorizedAccessException)
        {
            return new SpeechResult(false, "", "speech_no_mic", true);
        }
        catch (Exception ex)
        {
            Logger.Write("Online speech failed", ex);
            return new SpeechResult(false, "", "speech_unsupported", true);
        }
    }

    // ---------------- Синтез ----------------
    public List<string> Voices()
    {
        try
        {
            using var s = new SpeechSynthesizer();
            return s.GetInstalledVoices().Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name).ToList();
        }
        catch { return new(); }
    }

    /// <summary>Озвучивает текст. Возвращает false, если подходящего голоса нет.</summary>
    public bool Speak(string text, string culture)
    {
        try
        {
            var st = Svc.Settings;
            Stop();
            _tts = new SpeechSynthesizer();
            _tts.Rate = st.TtsRate;
            _tts.Volume = st.TtsVolume;
            bool voiceSet = false;
            if (!string.IsNullOrEmpty(st.TtsVoice))
            {
                try { _tts.SelectVoice(st.TtsVoice); voiceSet = true; } catch { }
            }
            if (!voiceSet)
            {
                try
                {
                    _tts.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, new CultureInfo(culture));
                    voiceSet = _tts.Voice.Culture.TwoLetterISOLanguageName == new CultureInfo(culture).TwoLetterISOLanguageName;
                }
                catch { voiceSet = false; }
            }
            _tts.SpeakAsync(text);
            return voiceSet;
        }
        catch (Exception ex)
        {
            Logger.Write("TTS failed", ex);
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            _tts?.SpeakAsyncCancelAll();
            _tts?.Dispose();
        }
        catch { }
        _tts = null;
    }
}
