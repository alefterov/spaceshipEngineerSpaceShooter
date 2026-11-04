using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

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
            catch (Exception) {  }
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
        }
    }
}
