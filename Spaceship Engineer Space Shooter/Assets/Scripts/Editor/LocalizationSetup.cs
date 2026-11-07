#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
#if L10N_SDK
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
#endif

public static class LocalizationSetup
{
    private const string Root = "Assets/Localization";
    private const string SourcePath = "Assets/Localization/Source/GameStrings.json";
    private const string MainFontPath = "Assets/Fonts/DaysOne-Regular SDF.asset";
    private static readonly string[] FallbackFonts =
    {
        "Assets/Fonts/Noto/NotoSans-Variable.ttf",
        "Assets/Fonts/Noto/NotoSansSC-Regular.otf",
        "Assets/Fonts/Noto/NotoSansKR-Regular.otf",
    };
    private static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/BattleScene.unity" };
    private const string PrefabFolder = "Assets/Prefabs";

    [Serializable]
    private class Entry
    {
        public string key;
        public string context;
        public string[] values;
    }

    [Serializable]
    private class Data
    {
        public string[] languages;
        public Entry[] entries;
    }

#if !L10N_SDK
    [MenuItem("Tools/Ship Builder/Localization/Set Up (needs the Localization package)")]
    private static void NotInstalled()
        => EditorUtility.DisplayDialog("Localization", "The Localization package is not loaded yet (no L10N_SDK symbol). Wait for Unity to finish importing and compiling, then try again.", "OK");
#else
    [MenuItem("Tools/Ship Builder/Localization/1. Languages and Strings")]
    public static void SetUpStrings()
    {
        if (!File.Exists(SourcePath))
        {
            Debug.LogError($"LocalizationSetup: {SourcePath} not found.");
            return;
        }

        var data = JsonUtility.FromJson<Data>(File.ReadAllText(SourcePath));
        EnsureFolder(Root);
        EnsureFolder(Root + "/Locales");
        EnsureFolder(Root + "/Tables");

        var settings = EnsureSettings();
        var locales = EnsureLocales(data.languages);
        EnsureSelectors(settings);

        var collection = LocalizationEditorSettings.GetStringTableCollection(L10n.Table)
                         ?? LocalizationEditorSettings.CreateStringTableCollection(L10n.Table, Root + "/Tables");

        foreach (var locale in locales)
            if (collection.GetTable(locale.Identifier) == null) collection.AddNewTable(locale.Identifier);

        foreach (var e in data.entries)
        {
            var shared = collection.SharedData.GetEntry(e.key) ?? collection.SharedData.AddKey(e.key);
            if (!string.IsNullOrEmpty(e.context))
            {
                var comment = shared.Metadata.GetMetadata<Comment>();
                if (comment == null) shared.Metadata.AddMetadata(new Comment { CommentText = e.context });
                else comment.CommentText = e.context;
            }

            for (int i = 0; i < data.languages.Length && i < e.values.Length; i++)
            {
                var table = collection.GetTable(new LocaleIdentifier(data.languages[i])) as StringTable;
                if (table == null) continue;

                var entry = table.GetEntry(shared.Id) ?? table.AddEntry(shared.Id, e.values[i]);
                entry.Value = e.values[i];
                EditorUtility.SetDirty(table);
            }
        }

        collection.SetPreloadTableFlag(true);
        EditorUtility.SetDirty(collection);
        EditorUtility.SetDirty(collection.SharedData);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        AssetDatabase.SaveAssets();

        Debug.LogWarning("LocalizationSetup: " + CheckTables());
    }

    private static LocalizationSettings EnsureSettings()
    {
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            var guid = AssetDatabase.FindAssets("t:LocalizationSettings", new[] { "Assets" }).FirstOrDefault();
            if (guid != null) settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(AssetDatabase.GUIDToAssetPath(guid));
        }

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "LocalizationSettings";
            AssetDatabase.CreateAsset(settings, Root + "/LocalizationSettings.asset");
        }

        LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        return settings;
    }

    private static List<Locale> EnsureLocales(IEnumerable<string> codes)
    {
        var result = new List<Locale>();
        foreach (var code in codes)
        {
            var locale = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == code);
            if (locale == null)
            {
                locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, $"{Root}/Locales/{code}.asset");
                LocalizationEditorSettings.AddLocale(locale);
            }

            result.Add(locale);
        }

        return result;
    }

    private static void EnsureSelectors(LocalizationSettings settings)
    {
        var selectors = settings.GetStartupLocaleSelectors();
        if (!selectors.OfType<PlayerPrefLocaleSelector>().Any())
            selectors.Insert(0, new PlayerPrefLocaleSelector { PlayerPreferenceKey = "selected-locale" });
        if (!selectors.OfType<SystemLocaleSelector>().Any())
            selectors.Add(new SystemLocaleSelector());

        var specific = selectors.OfType<SpecificLocaleSelector>().FirstOrDefault();
        if (specific == null) selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("en") });
        else specific.LocaleId = new LocaleIdentifier("en");

        EditorUtility.SetDirty(settings);
    }

    [MenuItem("Tools/Ship Builder/Localization/2. Fonts (Polish, Chinese, Korean)")]
    public static void SetUpFonts()
    {
        var main = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);
        if (main == null)
        {
            Debug.LogError($"LocalizationSetup: {MainFontPath} not found.");
            return;
        }

        var made = new List<string>();
        foreach (var fontPath in FallbackFonts)
        {
            var asset = EnsureFontAsset(fontPath);
            if (asset == null) continue;

            if (main.fallbackFontAssetTable == null) main.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!main.fallbackFontAssetTable.Contains(asset)) main.fallbackFontAssetTable.Add(asset);

            var global = TMP_Settings.fallbackFontAssets;
            if (global != null && !global.Contains(asset)) global.Add(asset);

            made.Add(asset.name);
        }

        EditorUtility.SetDirty(main);
        if (TMP_Settings.instance != null) EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();

        Debug.LogWarning($"LocalizationSetup: fallback fonts of {main.name}: {string.Join(", ", made)}");
    }

    private static TMP_FontAsset EnsureFontAsset(string fontPath)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            Debug.LogError($"LocalizationSetup: {fontPath} not found.");
            return null;
        }

        string assetPath = Path.ChangeExtension(fontPath, null) + " SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(asset, assetPath);

        foreach (var atlas in asset.atlasTextures)
        {
            atlas.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
        }

        asset.material.name = asset.name + " Material";
        asset.material.mainTexture = asset.atlasTexture;
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        var so = new SerializedObject(asset);
        var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
        if (clear != null)
        {
            clear.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        if (!AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Material>().Any())
            Debug.LogError($"LocalizationSetup: the material of {assetPath} was not saved.");

        return asset;
    }

    [MenuItem("Tools/Ship Builder/Localization/3. Bind Texts in Scenes and Prefabs")]
    public static void BindTexts()
    {
        if (Application.isBatchMode) throw new InvalidOperationException("Run this from the Editor, not in batch mode.");

        var collection = LocalizationEditorSettings.GetStringTableCollection(L10n.Table);
        if (collection == null)
        {
            EditorUtility.DisplayDialog("Localization", "Run \"1. Languages and Strings\" first.", "OK");
            return;
        }

        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }).Select(AssetDatabase.GUIDToAssetPath).ToList();
        if (!EditorUtility.DisplayDialog("Bind texts",
                $"This opens and SAVES {Scenes.Length} scenes ({string.Join(", ", Scenes.Select(Path.GetFileNameWithoutExtension))}) and the prefabs in {PrefabFolder} " +
                $"that have a matching text ({prefabs.Count} prefabs are checked). Texts whose words are in the \"{L10n.Table}\" table get a Localize String Event. " +
                "Commit or back up first. Continue?", "Bind", "Cancel"))
            throw new OperationCanceledException("Binding cancelled - nothing was written.");

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            throw new OperationCanceledException("Binding cancelled - nothing was written.");

        var keys = new HashSet<string>(collection.SharedData.Entries.Select(e => e.Key));
        var report = new Report();
        string startScene = SceneManager.GetActiveScene().path;

        foreach (var path in prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = Bind(root.GetComponentsInChildren<TMP_Text>(true), root.GetComponentsInChildren<MonoBehaviour>(true), keys, path, report);
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (var scenePath in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            int changed = Bind(roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToArray(),
                roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray(), keys, scenePath, report);
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
        LocalizationSettings.Instance.ForceRefresh();

        Debug.LogWarning(report.ToString());
    }

    private class Report
    {
        public readonly List<string> bound = new();
        public readonly List<string> setByScript = new();
        public readonly List<string> noKey = new();
        public readonly List<string> broken = new();

        public override string ToString() =>
            $"LocalizationSetup: bound {bound.Count} texts.\n" +
            $"Bound:\n  {string.Join("\n  ", bound)}\n" +
            $"Left alone, set by a script (translated in code where it matters) ({setByScript.Count}):\n  {string.Join("\n  ", setByScript)}\n" +
            $"Left alone, not in the table ({noKey.Count}):\n  {string.Join("\n  ", noKey)}\n" +
            (broken.Count > 0 ? $"BINDING NOT LIVE ({broken.Count}):\n  {string.Join("\n  ", broken)}" : "All bindings checked: live in Editor and Runtime.");
    }

    private static int Bind(TMP_Text[] texts, MonoBehaviour[] behaviours, HashSet<string> keys, string where, Report report)
    {
        var byScript = ReferencedTexts(behaviours);
        int changed = 0;

        foreach (var text in texts)
        {
            string key = L10n.Key(text.text);
            var existing = text.GetComponent<LocalizeStringEvent>();
            if (existing != null && !IsOurTable(existing)) continue;
            string label = $"{Path.GetFileNameWithoutExtension(where)} :: {PathOf(text.transform)} :: \"{key}\"";

            if (string.IsNullOrEmpty(key) || !key.Any(char.IsLetter)) continue;

            if (byScript.Contains(text) || !keys.Contains(key))
            {
                if (byScript.Contains(text)) report.setByScript.Add(label);
                else report.noKey.Add(label);

                if (existing != null && existing.enabled)
                {
                    existing.enabled = false;
                    RecordInstance(existing);
                    changed++;
                }

                continue;
            }

            var lse = existing != null ? existing : text.gameObject.AddComponent<LocalizeStringEvent>();
            lse.enabled = true;
            lse.StringReference.SetReference(L10n.Table, key);

            while (lse.OnUpdateString.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(lse.OnUpdateString, 0);
            var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, "set_text");
            UnityEventTools.AddPersistentListener(lse.OnUpdateString, setter);
            int index = lse.OnUpdateString.GetPersistentEventCount() - 1;
            lse.OnUpdateString.SetPersistentListenerState(index, UnityEventCallState.EditorAndRuntime);

            RecordInstance(lse);
            EditorUtility.SetDirty(lse);
            changed++;

            bool live = lse.OnUpdateString.GetPersistentEventCount() == 1
                        && lse.OnUpdateString.GetPersistentTarget(0) == text
                        && lse.OnUpdateString.GetPersistentMethodName(0) == "set_text"
                        && lse.OnUpdateString.GetPersistentListenerState(0) == UnityEventCallState.EditorAndRuntime;
            (live ? report.bound : report.broken).Add(label);
        }

        return changed;
    }

    private static bool IsOurTable(LocalizeStringEvent lse)
    {
        var table = lse.StringReference.TableReference;
        return table.ReferenceType == TableReference.Type.Empty || table.TableCollectionName == L10n.Table;
    }

    private static void RecordInstance(Component component)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }

    private static HashSet<TMP_Text> ReferencedTexts(IEnumerable<MonoBehaviour> behaviours)
    {
        var result = new HashSet<TMP_Text>();
        foreach (var behaviour in behaviours)
        {
            if (behaviour == null || behaviour is TMP_Text || behaviour is LocalizeStringEvent) continue;

            bool ours = behaviour.GetType().Assembly.GetName().Name == "Assembly-CSharp";
            if (!ours && !(behaviour is TMP_Dropdown) && !(behaviour is TMP_InputField)) continue;

            var so = new SerializedObject(behaviour);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (behaviour is TMP_InputField && it.name == "m_Placeholder") continue;
                if (it.objectReferenceValue is TMP_Text referenced) result.Add(referenced);
            }
        }

        return result;
    }

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Insert(0, t.name);
        return string.Join("/", parts);
    }

    [MenuItem("Tools/Ship Builder/Localization/4. Check Tables")]
    public static void CheckTablesMenu() => Debug.LogWarning("LocalizationSetup: " + CheckTables());

    private static string CheckTables()
    {
        var gaps = new List<string>();
        int checkedCount = 0;

        foreach (var col in LocalizationEditorSettings.GetStringTableCollections())
        {
            foreach (var key in col.SharedData.Entries)
            {
                foreach (var table in col.StringTables)
                {
                    checkedCount++;
                    var entry = table.GetEntry(key.Id);
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                        gaps.Add($"{col.TableCollectionName} / {table.LocaleIdentifier.Code} / {key.Key}");
                }
            }
        }

        if (checkedCount == 0) return "INCONCLUSIVE: no table entries found. Run \"1. Languages and Strings\" first.";
        return gaps.Count == 0
            ? $"COMPLETE: {checkedCount} entries checked, no gaps"
            : $"GAPS ({gaps.Count} of {checkedCount} checked):\n  " + string.Join("\n  ", gaps);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
#endif
}
#endif
