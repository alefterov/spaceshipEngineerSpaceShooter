using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Switches the real store code of IAPManager on and off by itself: while the In App Purchasing package (com.unity.purchasing 5) is in the
/// project it adds the scripting symbol IAP_SDK (for Android, iOS and desktop), and removes it again if the package is removed. Without it
/// the project still compiles — IAPManager then only simulates purchases in the Editor. Runs after every recompilation.
/// </summary>
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
            catch (Exception) { /* an assembly that cannot be inspected is not the package */ }
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
            Debug.Log($"IapDefineSetter: {(present ? "added" : "removed")} {Symbol} for {target.TargetName}.");
        }
    }
}
