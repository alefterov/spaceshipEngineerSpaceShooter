using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
public static class IapDefineSetter
{
    private const string Symbol = "IAP_SDK";

    static IapDefineSetter() => EditorApplication.delayCall += Update;

    private static bool PackagePresent()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (assembly.GetType("UnityEngine.Purchasing.UnityIAPServices", false) != null) return true;
            }
            catch (Exception) {  }
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
