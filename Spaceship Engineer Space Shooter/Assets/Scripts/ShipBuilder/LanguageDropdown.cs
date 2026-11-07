using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if L10N_SDK
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#endif

[RequireComponent(typeof(TMP_Dropdown))]
public class LanguageDropdown : MonoBehaviour
{
    private static readonly Dictionary<string, string> NativeNames = new()
    {
        { "en", "English" },
        { "uk", "Українська" },
        { "ru", "Русский" },
        { "de", "Deutsch" },
        { "fr", "Français" },
        { "es", "Español" },
        { "pt", "Português" },
        { "it", "Italiano" },
        { "pl", "Polski" },
        { "zh-Hans", "简体中文" },
        { "hi", "हिन्दी" },
        { "ko", "한국어" },
        { "ar", "العربية" },
    };

    private static readonly string[] Order = { "en", "uk", "ru", "de", "fr", "es", "pt", "it", "pl", "zh-Hans", "hi", "ko", "ar" };

    private TMP_Dropdown dropdown;

#if L10N_SDK
    private readonly List<Locale> locales = new();

    private void Awake() => dropdown = GetComponent<TMP_Dropdown>();

    private IEnumerator Start()
    {
        dropdown.interactable = false;
        yield return LocalizationSettings.InitializationOperation;

        locales.Clear();
        var available = LocalizationSettings.AvailableLocales.Locales;
        foreach (var code in Order)
        {
            var locale = available.Find(l => l.Identifier.Code == code);
            if (locale != null) locales.Add(locale);
        }

        foreach (var locale in available)
            if (!locales.Contains(locale)) locales.Add(locale);

        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var locale in locales)
            options.Add(new TMP_Dropdown.OptionData(NativeNames.TryGetValue(locale.Identifier.Code, out var name) ? name : locale.LocaleName));

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        ShowSelected();

        dropdown.onValueChanged.AddListener(OnChanged);
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        dropdown.interactable = true;
    }

    private void OnDestroy()
    {
        if (dropdown != null) dropdown.onValueChanged.RemoveListener(OnChanged);
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnChanged(int index)
    {
        if (index < 0 || index >= locales.Count) return;
        LocalizationSettings.SelectedLocale = locales[index];
        PlayerPrefs.SetString("selected-locale", locales[index].Identifier.Code);
        PlayerPrefs.Save();
    }

    private void OnLocaleChanged(Locale locale) => ShowSelected();

    private void ShowSelected()
    {
        int index = locales.IndexOf(LocalizationSettings.SelectedLocale);
        if (index >= 0) dropdown.SetValueWithoutNotify(index);
        dropdown.RefreshShownValue();
    }
#else
    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string> { NativeNames["en"] });
        dropdown.interactable = false;
    }
#endif
}
