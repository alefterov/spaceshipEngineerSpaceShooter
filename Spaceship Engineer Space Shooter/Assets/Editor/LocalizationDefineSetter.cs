using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

[InitializeOnLoad]
public static class LocalizationDefineSetter
{
    private const string Symbol = "L10N_SDK";

    static LocalizationDefineSetter() => EditorApplication.delayCall += Update;

    private static bool PackagePresent()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (assembly.GetType("UnityEngine.Localization.Settings.LocalizationSettings", false) != null) return true;
            }
            catch (Exception) { }
        }

        return false;
    }

    private static void Update()
    {
        bool present = PackagePresent();

        foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            bool has = symbols.Contains(Symbol);
            if (present == has) continue;

            if (present) symbols.Add(Symbol);
            else symbols.Remove(Symbol);

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
        }
    }
}
