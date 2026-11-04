using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class TutorialSetup
{
    private const string GunId = "kinetic_static_gun1_1x1";
    private const string ShieldTechId = "shield_1x1";
    private static readonly Color TextColor = new(0.7294f, 0.949f, 0.984f);

    [MenuItem("Tools/Ship Builder/Create Tutorial")]
    public static void Create()
    {
        bool battle = Object.FindAnyObjectByType<BattleSequenceController>(FindObjectsInactive.Include) != null;
        bool main = Object.FindAnyObjectByType<MainMenuFlowController>(FindObjectsInactive.Include) != null;
        if (!battle && !main)
        {
            Debug.LogWarning("TutorialSetup: open the main scene (MainMenuFlowController) or the battle scene (BattleSequenceController) first.");
            return;
        }

        var controller = Object.FindAnyObjectByType<TutorialController>(FindObjectsInactive.Include);
        if (controller == null) controller = BuildUi();
        else Undo.RecordObject(controller, "Tutorial steps");

        controller.steps = battle ? BattleSteps() : MainSteps();
        EditorUtility.SetDirty(controller);

        if (main) RemoveStarterGun();

        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Selection.activeObject = controller.gameObject;
        var missing = controller.steps.Where(s => s.targetKind == TutorialTargetKind.Object && s.target == null).Select(s => s.id).ToList();
    }

    private static List<TutorialStep> MainSteps()
    {
        var flow = Object.FindAnyObjectByType<MainMenuFlowController>(FindObjectsInactive.Include);
        var builder = flow.builderScreen;
        var modes = flow.gameModePopup != null ? flow.gameModePopup.gameObject : null;
        var levels = flow.levelSelectPopup != null ? flow.levelSelectPopup.gameObject : null;

        var selector = Object.FindAnyObjectByType<CampaignSelector>(FindObjectsInactive.Include);
        var firstCampaign = selector != null && selector.campaigns.Count > 0 ? selector.campaigns[0] : null;
        var info = Object.FindAnyObjectByType<LevelInfoPanel>(FindObjectsInactive.Include);
        var rewards = Object.FindAnyObjectByType<RewardsPanel>(FindObjectsInactive.Include);
        var techPopup = Object.FindAnyObjectByType<TechTreePopup>(FindObjectsInactive.Include);
        var techPanel = techPopup != null ? techPopup.gameObject : Named("TechnologyPanel")?.gameObject;
        var shieldTech = AssetDatabase.FindAssets("t:TechDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<TechDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(t => t != null && t.id == ShieldTechId);
        var level1 = AssetDatabase.FindAssets("Campaign1Level1 t:LevelDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(l => l != null && l.name == "Campaign1Level1");

        return new List<TutorialStep>
        {
            new() { id = "open_builder", target = ButtonWithMethod("OnBuildShipPressed"), targetKind = TutorialTargetKind.Object,
                    completeWhen = TutorialComplete.ObjectActive, watchObject = builder,
                    text = "Welcome aboard, Commander! Your ship is almost ready for its first battle, but one gun is still missing. Tap Build to open the ship builder." },
            new() { id = "select_weapons", target = Named("WeaponCategoryButton"), targetKind = TutorialTargetKind.Object, requireActive = builder,
                    completeWhen = TutorialComplete.TargetTapped,
                    text = "Blocks are sorted into categories. Tap Weapons." },
            new() { id = "place_gun", targetKind = TutorialTargetKind.PaletteBlock, blockId = GunId, requireActive = builder,
                    completeWhen = TutorialComplete.BlockAdded, finger = TutorialFingerMotion.Drag, blockOthers = false,
                    text = "Drag the gun onto the free spot of your ship, then tap the check mark to confirm." },
            new() { id = "save_ship", target = ButtonWithMethod("OnSaveShipPressed"), targetKind = TutorialTargetKind.Object, requireActive = builder,
                    completeWhen = TutorialComplete.TargetTapped,
                    text = "Well done! Tap Save to keep your new ship." },
            new() { id = "leave_builder", target = ButtonWithMethod("OnExitBuilderPressed"), targetKind = TutorialTargetKind.Object,
                    completeWhen = TutorialComplete.ObjectInactive, watchObject = builder,
                    text = "Your ship is saved. Now leave the builder." },
            new() { id = "press_play", target = ButtonWithMethod("OnPlayPressed"), targetKind = TutorialTargetKind.Object,
                    completeWhen = TutorialComplete.ObjectActive, watchObject = modes,
                    text = "Your ship is ready for battle. Tap Play!" },
            new() { id = "choose_campaign", target = ButtonWithMethod("OnCampaignPressed"), targetKind = TutorialTargetKind.Object, requireActive = modes,
                    completeWhen = TutorialComplete.ObjectActive, watchObject = levels,
                    text = "Choose Campaign." },
            new() { id = "choose_system", target = firstCampaign?.selectButton != null ? (RectTransform)firstCampaign.selectButton.transform : null,
                    targetKind = TutorialTargetKind.Object, requireActive = selector != null && selector.selectionRoot != null ? selector.selectionRoot : levels,
                    completeWhen = TutorialComplete.ObjectActive, watchObject = firstCampaign?.panel,
                    text = "Select the first star system." },
            new() { id = "choose_level", targetKind = TutorialTargetKind.LevelButton, level = level1, requireActive = firstCampaign?.panel,
                    completeWhen = TutorialComplete.TargetTapped,
                    text = "Tap level 1." },
            new() { id = "start_battle", target = info != null && info.playButton != null ? (RectTransform)info.playButton.transform : null,
                    targetKind = TutorialTargetKind.Object, requireActive = info != null ? info.gameObject : null,
                    completeWhen = TutorialComplete.TargetTapped,
                    text = "Tap Play to launch the battle!" },
            new() { id = "open_rewards", target = Named("RewardButton"), targetKind = TutorialTargetKind.Object, startWhen = TutorialStart.HasClaimableRewards,
                    completeWhen = TutorialComplete.ObjectActive, watchObject = rewards != null ? rewards.gameObject : null,
                    text = "You've completed a task! Open Rewards to collect your prize." },
            new() { id = "claim_reward", targetKind = TutorialTargetKind.ClaimableReward, requireActive = rewards != null ? rewards.gameObject : null,
                    completeWhen = TutorialComplete.TargetTapped,
                    text = "Tap Claim to get your reward." },
            new() { id = "open_research", target = Named("TechnologyButton"), targetKind = TutorialTargetKind.Object,
                    startWhen = TutorialStart.CanResearchTech, tech = shieldTech,
                    completeWhen = TutorialComplete.ObjectActive, watchObject = techPanel,
                    text = "You have enough research points to unlock shields! Open Research." },
            new() { id = "research_shield", targetKind = TutorialTargetKind.TechNode, tech = shieldTech, requireActive = techPanel,
                    completeWhen = TutorialComplete.TechResearched,
                    text = "Tap the shield to research it. Shields stop enemy shots before they hit your ship." },
        };
    }

    private static List<TutorialStep> BattleSteps()
    {
        var joystick = Object.FindAnyObjectByType<ShipJoystickController>(FindObjectsInactive.Include);
        RectTransform target = joystick == null ? null : joystick.joystickBase != null ? joystick.joystickBase : (RectTransform)joystick.transform;

        return new List<TutorialStep>
        {
            new() { id = "battle_steer", target = target, targetKind = TutorialTargetKind.Object, startWhen = TutorialStart.BattleStarted,
                    completeWhen = TutorialComplete.TargetHeld, holdSeconds = 1.5f, finger = TutorialFingerMotion.Joystick, blockOthers = false,
                    text = "Move your finger on the joystick to fly your ship. Your guns fire on their own: dodge enemy shots and destroy every enemy!" },
        };
    }

    private static RectTransform ButtonWithMethod(string method)
    {
        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            UnityEventBase onClick = button.onClick;
            for (int i = 0; i < onClick.GetPersistentEventCount(); i++)
                if (onClick.GetPersistentMethodName(i) == method) return (RectTransform)button.transform;
        }

        return null;
    }

    private static RectTransform Named(string name)
        => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name.Trim() == name);

    private static void RemoveStarterGun()
    {
        var data = Object.FindAnyObjectByType<GameDataManager>(FindObjectsInactive.Include);
        if (data == null) return;

        var so = new SerializedObject(data);
        var blocks = so.FindProperty("starterShip.blocks");
        if (blocks == null) return;

        int guns = 0;
        for (int i = 0; i < blocks.arraySize; i++)
            if (blocks.GetArrayElementAtIndex(i).FindPropertyRelative("blockId").stringValue == GunId) guns++;
        if (guns < 2) return;

        for (int i = blocks.arraySize - 1; i >= 0; i--)
        {
            var entry = blocks.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("blockId").stringValue != GunId) continue;
            blocks.DeleteArrayElementAtIndex(i);

            break;
        }

        so.ApplyModifiedProperties();
    }

    private static TutorialController BuildUi()
    {
        var font = AssetDatabase.FindAssets("DaysOne-Regular SDF t:TMP_FontAsset")
            .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();

        var canvasObject = new GameObject("TutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Tutorial");
        canvasObject.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        var gameScaler = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include).FirstOrDefault(s => s != scaler);
        if (gameScaler != null)
        {
            scaler.uiScaleMode = gameScaler.uiScaleMode;
            scaler.referenceResolution = gameScaler.referenceResolution;
            scaler.screenMatchMode = gameScaler.screenMatchMode;
            scaler.matchWidthOrHeight = gameScaler.matchWidthOrHeight;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }

        var root = Rect("Root", canvasObject.transform);
        Stretch(root);

        var blockers = new RectTransform[4];
        string[] names = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
        for (int i = 0; i < 4; i++)
        {
            blockers[i] = Rect(names[i], root);
            var image = blockers[i].gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            blockers[i].gameObject.SetActive(false);
        }

        var fingerRect = Rect("Finger", root);
        fingerRect.sizeDelta = new Vector2(120, 120);
        fingerRect.pivot = new Vector2(0.3f, 0.9f);
        var fingerImage = fingerRect.gameObject.AddComponent<Image>();
        fingerImage.raycastTarget = false;
        fingerImage.color = new Color(1f, 1f, 1f, 0.9f);
        var finger = fingerRect.gameObject.AddComponent<TutorialFinger>();
        fingerRect.gameObject.SetActive(false);

        var window = Rect("HintWindow", root);
        window.sizeDelta = new Vector2(900, 340);
        var windowImage = window.gameObject.AddComponent<Image>();
        windowImage.color = new Color(0.04f, 0.09f, 0.14f, 0.94f);

        var text = Label("Text", window, font, 38, TextAlignmentOptions.Center);
        var textRect = (RectTransform)text.transform;
        Stretch(textRect);
        textRect.offsetMin = new Vector2(40, 80);
        textRect.offsetMax = new Vector2(-90, -30);
        text.enableAutoSizing = true;
        text.fontSizeMin = 24;
        text.fontSizeMax = 40;

        var close = Rect("CloseButton", window);
        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1, 1);
        close.anchoredPosition = new Vector2(-14, -14);
        close.sizeDelta = new Vector2(64, 64);
        var closeImage = close.gameObject.AddComponent<Image>();
        closeImage.color = new Color(1f, 1f, 1f, 0.12f);
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        var closeLabel = Label("X", close, font, 34, TextAlignmentOptions.Center);
        Stretch((RectTransform)closeLabel.transform);
        closeLabel.text = "X";

        var toggle = BuildToggle(window, font);

        var controller = canvasObject.AddComponent<TutorialController>();
        controller.root = root;
        controller.window = window;
        controller.textLabel = text;
        controller.closeButton = closeButton;
        controller.dontShowToggle = toggle;
        controller.finger = finger;
        controller.blockers = blockers;
        window.gameObject.SetActive(false);
        return controller;
    }

    private static Toggle BuildToggle(RectTransform window, TMP_FontAsset font)
    {
        var row = Rect("DontShowToggle", window);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 0);
        row.anchoredPosition = new Vector2(30, 20);
        row.sizeDelta = new Vector2(600, 50);

        var box = Rect("Box", row);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0, 0.5f);
        box.anchoredPosition = Vector2.zero;
        box.sizeDelta = new Vector2(44, 44);
        var boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = new Color(1f, 1f, 1f, 0.2f);

        var check = Rect("Checkmark", box);
        Stretch(check);
        check.offsetMin = new Vector2(8, 8);
        check.offsetMax = new Vector2(-8, -8);
        var checkImage = check.gameObject.AddComponent<Image>();
        checkImage.color = TextColor;

        var label = Label("Label", row, font, 26, TextAlignmentOptions.MidlineLeft);
        var labelRect = (RectTransform)label.transform;
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(60, 0);
        label.text = "Don't show the tutorial again";

        var toggle = row.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = boxImage;
        toggle.graphic = checkImage;
        toggle.isOn = false;
        return toggle;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
    {
        var rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = TextColor;
        text.raycastTarget = false;
        return text;
    }
}
