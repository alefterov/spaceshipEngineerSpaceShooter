using System;
using UnityEngine;
#if L10N_SDK
using UnityEngine.Localization.Settings;
#endif

public static class L10n
{
    public const string Table = "Game";

    public static event Action Changed;

    public static string Tr(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

#if L10N_SDK
        try
        {
            var table = LocalizationSettings.StringDatabase.GetTable(Table);
            var entry = table != null ? table.GetEntry(Key(text)) : null;
            if (entry != null && !string.IsNullOrEmpty(entry.Value)) return entry.Value;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"L10n: no translation of \"{text}\": {e.Message}");
        }
#endif

        return text;
    }

    public static string Key(string text) => text == null ? null : text.Replace("​", "").Trim();

#if L10N_SDK
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private static void OnLocaleChanged(UnityEngine.Localization.Locale locale) => Changed?.Invoke();
#endif
}
