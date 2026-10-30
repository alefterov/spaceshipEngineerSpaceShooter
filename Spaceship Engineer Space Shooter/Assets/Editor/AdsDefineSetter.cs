using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Switches the real advertising code of AdsManager on and off by itself: while the Google Mobile Ads plugin is in the project it adds the
/// scripting symbol ADMOB_SDK (for Android, iOS and desktop), and removes it again if the plugin is deleted. Without it the project still
/// compiles — AdsManager then only logs what it would show. Runs after every recompilation.
/// </summary>
[InitializeOnLoad]
public static class AdsDefineSetter
{
    private const string Symbol = "ADMOB_SDK";

    static AdsDefineSetter() => EditorApplication.delayCall += Update;

    private static bool PluginPresent()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (assembly.GetType("GoogleMobileAds.Api.MobileAds", false) != null) return true;
            }
            catch (Exception) { /* an assembly that cannot be inspected is not the plugin */ }
        }

        return false;
    }

    private static void Update()
    {
        bool present = PluginPresent();

        foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            bool has = symbols.Contains(Symbol);
            if (present == has) continue;

            if (present) symbols.Add(Symbol);
            else symbols.Remove(Symbol);

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            Debug.Log($"AdsDefineSetter: {(present ? "added" : "removed")} {Symbol} for {target.TargetName}.");
        }
    }
}
