#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// One-shot generator for the UI hierarchy the ShipBuilder scripts expect
/// (Canvas, EventSystem, MainMenu screen, Builder screen, palette scroll view,
/// rotate/delete buttons) and for wiring every public field these scripts need.
/// Run via Tools/Ship Builder/Generate Scene UI. Safe to re-run — skips anything
/// that already exists instead of duplicating it.
/// </summary>
public static class ShipBuilderUISetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string BlockDatabasePath = "Assets/Scripts/ScriptableObject/BlockDatabase.asset";
    private const string BlockButtonPrefabPath = "Assets/Prefabs/UI/BlockButton.prefab";

    [MenuItem("Tools/Ship Builder/Generate Scene UI")]
    public static void Generate()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (GameObject.Find("Canvas") != null)
        {
            Debug.LogWarning("ShipBuilderUISetup: a 'Canvas' already exists in the scene — aborting to avoid duplicating UI. Delete it first if you want to regenerate.");
            return;
        }

        var shipGridObj = GameObject.Find("ShipGrid");
        if (shipGridObj == null)
        {
            Debug.LogError("ShipBuilderUISetup: no 'ShipGrid' GameObject found in the scene. Aborting.");
            return;
        }
        var shipGrid = shipGridObj.GetComponent<ShipGrid>();

        var mainCameraObj = GameObject.Find("Main Camera");
        var mainCamera = mainCameraObj != null ? mainCameraObj.GetComponent<Camera>() : null;

        var database = AssetDatabase.LoadAssetAtPath<BlockDatabase>(BlockDatabasePath);
        if (database == null)
            Debug.LogWarning($"ShipBuilderUISetup: BlockDatabase not found at {BlockDatabasePath}.");

        var blockButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockButtonPrefabPath);
        if (blockButtonPrefab == null)
            Debug.LogWarning($"ShipBuilderUISetup: BlockButton prefab not found at {BlockButtonPrefabPath}.");

        // ---------- EventSystem ----------
        if (GameObject.Find("EventSystem") == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // ---------- Canvas ----------
        var canvasObj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); // portrait mobile — adjust to taste
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ---------- Bootstrap (GameDataManager) ----------
        var bootstrap = new GameObject("Bootstrap", typeof(GameDataManager));
        Undo.RegisterCreatedObjectUndo(bootstrap, "Create Bootstrap");

        // ---------- Main Menu screen ----------
        var mainMenuScreen = CreateUIObject("MainMenuScreen", canvasObj.transform);
        Stretch(mainMenuScreen);

        var playButton = CreateButton(mainMenuScreen, "PlayButton", "Играть", out _);
        Anchor(playButton, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(320, 110));

        var buildShipButton = CreateButton(mainMenuScreen, "BuildShipButton", "Ангар", out _);
        Anchor(buildShipButton, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), new Vector2(320, 110));

        // ---------- Builder screen ----------
        var builderScreen = CreateUIObject("BuilderScreen", canvasObj.transform);
        Stretch(builderScreen);
        builderScreen.gameObject.SetActive(false);

        // Full-screen input catcher FIRST so it renders/hits behind every other panel below.
        var inputCatcher = CreateUIObject("InputCatcher", builderScreen);
        Stretch(inputCatcher);
        AddImage(inputCatcher, new Color(0, 0, 0, 0), raycastTarget: true);
        var ghost = inputCatcher.gameObject.AddComponent<GhostBlockController>();
        ghost.grid = shipGrid;
        ghost.worldCamera = mainCamera;

        // Top bar: Hull / Modules mode tabs + Exit
        var topBar = CreateUIObject("TopBar", builderScreen);
        Anchor(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, 140));
        topBar.pivot = new Vector2(0.5f, 1f);
        var topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        topLayout.childAlignment = TextAnchor.MiddleLeft;
        topLayout.spacing = 20;
        topLayout.padding = new RectOffset(20, 20, 20, 20);
        // Control flags default OFF in Unity — without them, LayoutElement.preferredWidth/Height
        // below (and the spacer's flexibleWidth) are silently ignored and children keep their
        // raw 100x100 RectTransform default instead.
        topLayout.childControlWidth = true;
        topLayout.childControlHeight = true;
        topLayout.childForceExpandWidth = false;
        topLayout.childForceExpandHeight = true;

        var hullModeButton = CreateButton(topBar, "HullModeButton", "Корпус", out var hullLE);
        SetLayoutSize(hullLE, 220, 100);
        var modulesModeButton = CreateButton(topBar, "ModulesModeButton", "Модули", out var modLE);
        SetLayoutSize(modLE, 220, 100);

        var spacer = CreateUIObject("Spacer", topBar);
        var spacerLE = spacer.gameObject.AddComponent<LayoutElement>();
        spacerLE.flexibleWidth = 1;

        var exitButton = CreateButton(topBar, "ExitButton", "Выход", out var exitLE);
        SetLayoutSize(exitLE, 220, 100);

        // Bottom panel: palette scroll view + rotate + delete
        var bottomPanel = CreateUIObject("BottomPanel", builderScreen);
        Anchor(bottomPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0, 220));
        bottomPanel.pivot = new Vector2(0.5f, 0f);
        AddImage(bottomPanel, new Color(0f, 0f, 0f, 0.35f), raycastTarget: true);

        var scrollViewRT = CreateUIObject("PaletteScrollView", bottomPanel);
        scrollViewRT.anchorMin = new Vector2(0f, 0f);
        scrollViewRT.anchorMax = new Vector2(0.72f, 1f);
        scrollViewRT.offsetMin = new Vector2(20, 10);
        scrollViewRT.offsetMax = new Vector2(-10, -10);
        var scrollRect = scrollViewRT.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;

        var viewport = CreateUIObject("Viewport", scrollViewRT);
        Stretch(viewport);
        AddImage(viewport, new Color(1, 1, 1, 0.02f), raycastTarget: true);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateUIObject("Content", viewport);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.sizeDelta = new Vector2(0, 0);
        var contentLayout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        contentLayout.spacing = 12;
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = true;
        contentLayout.childAlignment = TextAnchor.MiddleLeft;
        var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        var paletteUI = scrollViewRT.gameObject.AddComponent<BuildPaletteUI>();
        paletteUI.database = database;
        paletteUI.buttonContainer = content;
        paletteUI.buttonPrefab = blockButtonPrefab;

        var rotateButtonRT = CreateUIObject("RotateButton", bottomPanel);
        rotateButtonRT.anchorMin = new Vector2(0.74f, 0.15f);
        rotateButtonRT.anchorMax = new Vector2(0.74f, 0.15f);
        rotateButtonRT.sizeDelta = new Vector2(150, 150);
        AddImage(rotateButtonRT, new Color(1, 1, 1, 0.85f));
        var rotateButtonComp = rotateButtonRT.gameObject.AddComponent<Button>();
        rotateButtonComp.targetGraphic = rotateButtonRT.GetComponent<Image>();
        AddLegacyText(rotateButtonRT, "⟳", 48, TextAnchor.MiddleCenter, Color.black);

        var rotateIconRT = CreateUIObject("RotateIcon", rotateButtonRT);
        rotateIconRT.anchorMin = rotateIconRT.anchorMax = new Vector2(0.5f, 0.5f);
        rotateIconRT.sizeDelta = new Vector2(60, 10);
        AddImage(rotateIconRT, new Color(0.2f, 0.2f, 0.2f, 1f), raycastTarget: false);

        var deleteButtonRT = CreateUIObject("DeleteButton", bottomPanel);
        deleteButtonRT.anchorMin = new Vector2(0.87f, 0.15f);
        deleteButtonRT.anchorMax = new Vector2(0.87f, 0.15f);
        deleteButtonRT.sizeDelta = new Vector2(150, 150);
        var deleteImage = AddImage(deleteButtonRT, new Color(1, 1, 1, 0.85f));
        var deleteButtonComp = deleteButtonRT.gameObject.AddComponent<Button>();
        deleteButtonComp.targetGraphic = deleteImage;
        // BuildModeController.Start() reads deleteButton.image.color (Selectable's Sprite-Swap
        // slot, separate from targetGraphic) — must be set explicitly or it NREs on play.
        deleteButtonComp.image = deleteImage;
        AddLegacyText(deleteButtonRT, "Удалить", 28, TextAnchor.MiddleCenter, Color.black);

        // BuildModeController lives on the BuilderScreen root — the "top-level state" for the screen.
        var buildModeController = builderScreen.gameObject.AddComponent<BuildModeController>();
        buildModeController.grid = shipGrid;
        buildModeController.ghost = ghost;
        buildModeController.palette = paletteUI;
        buildModeController.rotateButton = rotateButtonComp;
        buildModeController.rotateButtonIcon = rotateIconRT;
        buildModeController.deleteButton = deleteButtonComp;

        // BuildPaletteUI.controller is read on every palette click (Select -> controller.SelectBlock);
        // paletteUI was created before BuildModeController existed, so wire it back here.
        paletteUI.controller = buildModeController;

        // ---------- Game flow (screen switching) ----------
        var gameFlowObj = new GameObject("GameFlow", typeof(MainMenuFlowController));
        Undo.RegisterCreatedObjectUndo(gameFlowObj, "Create GameFlow");
        var flow = gameFlowObj.GetComponent<MainMenuFlowController>();
        flow.playerShip = shipGrid;
        flow.database = database;
        flow.mainMenuScreen = mainMenuScreen.gameObject;
        flow.builderScreen = builderScreen.gameObject;
        flow.buildModeController = buildModeController;

        // ---------- Button wiring (methods NOT self-registered by the scripts) ----------
        AddClickListener(playButton, flow, nameof(MainMenuFlowController.OnPlayPressed));
        AddClickListener(buildShipButton, flow, nameof(MainMenuFlowController.OnBuildShipPressed));
        AddClickListener(exitButton, flow, nameof(MainMenuFlowController.OnExitBuilderPressed));
        AddClickListener(hullModeButton, buildModeController, nameof(BuildModeController.SetHullBuildMode));
        AddClickListener(modulesModeButton, buildModeController, nameof(BuildModeController.SetModuleBuildMode));

        FixBlockButtonHighlight();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("ShipBuilderUISetup: scene UI generated and saved (Canvas, EventSystem, MainMenuScreen, BuilderScreen, palette, rotate/delete buttons, GameFlow).");
    }

    /// <summary>
    /// BlockButtonView.selectionHighlight in the existing BlockButton prefab was wired to the
    /// "Text (Legacy)" label instead of a dedicated highlight frame — toggling selection was
    /// hiding the block's name instead of showing a glow border. Adds the missing frame and
    /// rewires the field. No-ops if a highlight child already exists.
    /// </summary>
    private static void FixBlockButtonHighlight()
    {
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BlockButtonPrefabPath);
        if (prefabAsset == null) return;

        var root = PrefabUtility.LoadPrefabContents(BlockButtonPrefabPath);
        var view = root.GetComponent<BlockButtonView>();
        if (view == null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        if (root.transform.Find("SelectionHighlight") != null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var rt = CreateUIObject("SelectionHighlight", root.transform);
        Stretch(rt);
        rt.offsetMin = new Vector2(-4, -4);
        rt.offsetMax = new Vector2(4, 4);
        var img = AddImage(rt, new Color(1f, 1f, 1f, 0.9f), raycastTarget: false);
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        rt.SetAsFirstSibling(); // behind icon + label
        rt.gameObject.SetActive(false);

        view.selectionHighlight = rt.gameObject;
        EditorUtility.SetDirty(view);
        PrefabUtility.SaveAsPrefabAsset(root, BlockButtonPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("ShipBuilderUISetup: fixed BlockButton.prefab — added a dedicated SelectionHighlight frame (selectionHighlight previously pointed at the Text label).");
    }

    // ---------- Helpers ----------

    private static RectTransform CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void SetLayoutSize(LayoutElement le, float width, float height)
    {
        le.preferredWidth = width;
        le.preferredHeight = height;
    }

    private static Image AddImage(RectTransform rt, Color color, bool raycastTarget = true)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    private static Font GetDefaultFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private static Text AddLegacyText(RectTransform rt, string text, int fontSize, TextAnchor align, Color color)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.font = GetDefaultFont();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        var textRT = t.GetComponent<RectTransform>();
        Stretch(textRT);
        return t;
    }

    private static RectTransform CreateButton(RectTransform parent, string name, string label, out LayoutElement layoutElement)
    {
        var rt = CreateUIObject(name, parent);
        var img = AddImage(rt, new Color(1f, 1f, 1f, 0.9f));
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        AddLegacyText(rt, label, 32, TextAnchor.MiddleCenter, Color.black);
        layoutElement = rt.gameObject.AddComponent<LayoutElement>();
        return rt;
    }

    private static void AddClickListener(RectTransform buttonRT, Object target, string methodName)
    {
        var button = buttonRT.GetComponent<Button>();
        var method = target.GetType().GetMethod(methodName);
        var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, method);
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }
}
#endif
