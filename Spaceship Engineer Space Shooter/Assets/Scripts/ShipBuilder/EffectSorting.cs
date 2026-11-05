using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class EffectSorting
{
    public const string EffectsLayer = "Effects";
    public const string UiLayer = "UI";

    public static bool HasLayers => IsLayer(EffectsLayer) && IsLayer(UiLayer);

    public static int EffectsLayerId => SortingLayer.NameToID(EffectsLayer);

    private static bool IsLayer(string layerName)
    {
        int id = SortingLayer.NameToID(layerName);
        return id != 0 && SortingLayer.IsValid(id);
    }

    public static GameObject Apply(GameObject fx)
    {
        if (fx == null || !IsLayer(EffectsLayer)) return fx;

        int id = EffectsLayerId;
        foreach (var group in fx.GetComponentsInChildren<SortingGroup>(true)) group.sortingLayerID = id;
        foreach (var renderer in fx.GetComponentsInChildren<Renderer>(true)) renderer.sortingLayerID = id;
        foreach (var canvas in fx.GetComponentsInChildren<Canvas>(true))
        {
            canvas.overrideSorting = true;
            canvas.sortingLayerID = id;
        }

        return fx;
    }

    public static void ApplyToCanvas(Canvas canvas)
    {
        if (canvas != null && IsLayer(EffectsLayer)) canvas.sortingLayerID = EffectsLayerId;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsLayer(UiLayer)) return;

        int ui = SortingLayer.NameToID(UiLayer);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.sortingLayerID != 0) continue;

                canvas.sortingLayerID = ui;
                foreach (var nested in canvas.GetComponentsInChildren<Canvas>(true))
                    if (nested != canvas && nested.overrideSorting && nested.sortingLayerID == 0) nested.sortingLayerID = ui;
            }
        }
    }
}
