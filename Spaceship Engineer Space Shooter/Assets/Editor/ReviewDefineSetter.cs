using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

[InitializeOnLoad]
public static class ReviewDefineSetter
{
    private const string Symbol = "REVIEW_SDK";

    static ReviewDefineSetter() => EditorApplication.delayCall += Update;

    private static bool PackagePresent()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (assembly.GetType("Google.Play.Review.ReviewManager", false) != null) return true;
            }
            catch (Exception) { }
        }

        return false;
    }

    private static void Update()
    {
        bool present = PackagePresent();

        var target = NamedBuildTarget.Android;
        var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        bool has = symbols.Contains(Symbol);
        if (present == has) return;

        if (present) symbols.Add(Symbol);
        else symbols.Remove(Symbol);

        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
    }
}
