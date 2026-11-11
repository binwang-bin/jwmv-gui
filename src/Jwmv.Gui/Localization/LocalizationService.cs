using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jwmv.Gui.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _preferencesPath;
    private IReadOnlyDictionary<string, string> _strings = UiStringCatalog.English;

    public LocalizationService(string? preferencesDirectory = null)
    {
        var root = preferencesDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jwmv");
        Directory.CreateDirectory(root);
        _preferencesPath = Path.Combine(root, "gui.json");
        Current = this;
        ApplyLanguage(LoadPreferredLanguage(), persist: false);
    }

    public static LocalizationService? Current { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public UiLanguage Language { get; private set; } = UiLanguage.English;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (_strings.TryGetValue(key, out var value))
        {
            return value;
        }

        if (UiStringCatalog.English.TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    public void SetLanguage(UiLanguage language) => ApplyLanguage(language, persist: true);

    public void SetLanguageFromCode(string? code)
    {
        if (string.Equals(code, "zh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "zh-CN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "zh-Hans", StringComparison.OrdinalIgnoreCase))
        {
            SetLanguage(UiLanguage.Chinese);
            return;
        }

        SetLanguage(UiLanguage.English);
    }

    private void ApplyLanguage(UiLanguage language, bool persist)
    {
        Language = language;
        _strings = language == UiLanguage.Chinese ? UiStringCatalog.Chinese : UiStringCatalog.English;
        var culture = language == UiLanguage.Chinese
            ? CultureInfo.GetCultureInfo("zh-CN")
            : CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        if (persist)
        {
            SavePreferredLanguage(language);
        }

        OnPropertyChanged(string.Empty);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private UiLanguage LoadPreferredLanguage()
    {
        try
        {
            if (File.Exists(_preferencesPath))
            {
                var json = File.ReadAllText(_preferencesPath);
                var prefs = JsonSerializer.Deserialize<GuiPreferences>(json, JsonOptions);
                if (prefs is not null)
                {
                    return prefs.Language;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            // Fall back to system language when preferences cannot be read.
        }

        var system = CultureInfo.CurrentUICulture;
        return system.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.Chinese
            : UiLanguage.English;
    }

    private void SavePreferredLanguage(UiLanguage language)
    {
        try
        {
            var prefs = new GuiPreferences { Language = language };
            File.WriteAllText(_preferencesPath, JsonSerializer.Serialize(prefs, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Preference persistence is best-effort.
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class GuiPreferences
    {
        public UiLanguage Language { get; set; } = UiLanguage.English;
    }
}
