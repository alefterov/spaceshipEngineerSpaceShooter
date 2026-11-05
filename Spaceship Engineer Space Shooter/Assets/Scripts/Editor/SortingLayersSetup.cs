#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SortingLayersSetup
{
    private static readonly string[] Wanted = { EffectSorting.EffectsLayer, EffectSorting.UiLayer };

    static SortingLayersSetup() => EditorApplication.delayCall += Ensure;

    [MenuItem("Tools/Ship Builder/Ensure Sorting Layers (Effects, UI)")]
    public static void Ensure()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        var tagManager = new SerializedObject(assets[0]);
        var layers = tagManager.FindProperty("m_SortingLayers");
        if (layers == null) return;

        bool changed = false;
        foreach (var name in Wanted)
        {
            if (IndexOf(layers, name) >= 0) continue;

            layers.InsertArrayElementAtIndex(layers.arraySize);
            var entry = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            entry.FindPropertyRelative("name").stringValue = name;
            entry.FindPropertyRelative("uniqueID").intValue = NewId(layers);
            var locked = entry.FindPropertyRelative("locked");
            if (locked != null) locked.intValue = 0;
            changed = true;
        }

        int effects = IndexOf(layers, EffectSorting.EffectsLayer);
        int ui = IndexOf(layers, EffectSorting.UiLayer);
        if (effects > ui)
        {
            layers.MoveArrayElement(ui, layers.arraySize - 1);
            changed = true;
        }

        if (!changed) return;
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    private static int IndexOf(SerializedProperty layers, string name)
    {
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
        return -1;
    }

    private static int NewId(SerializedProperty layers)
    {
        for (;;)
        {
            int id = Random.Range(1, int.MaxValue);
            bool used = false;
            for (int i = 0; i < layers.arraySize && !used; i++)
                used = layers.GetArrayElementAtIndex(i).FindPropertyRelative("uniqueID").intValue == id;
            if (!used) return id;
        }
    }
}
#endif
