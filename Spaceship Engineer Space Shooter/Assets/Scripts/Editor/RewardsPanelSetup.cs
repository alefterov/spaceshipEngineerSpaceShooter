#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RewardsPanelSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string DataFolder = "Assets/Scripts/ScriptableObject/Rewards";
    private const string LevelsFolder = "Assets/Scripts/ScriptableObject/PlayMode";

    private static readonly Color Light = new(0.7294118f, 0.9490196f, 0.9843137f, 1f);
    private static TMP_FontAsset font;

    [MenuItem("Tools/Ship Builder/Create Rewards Panel")]
    public static void Create()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
        if (canvas == null) { Debug.LogError("RewardsPanelSetup: no 'Canvas' in SampleScene."); return; }

        var existing = canvas.transform.Find("RewardsPanel");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Rewards panel", "The scene already has a RewardsPanel. Replace it with a new one?", "Replace", "Cancel")) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        font = FindAsset<TMP_FontAsset>("DaysOne-Regular SDF");

        var icons = new RewardIcons
        {
            credits = LoadSprite("creditsIcon"),
            coins = LoadSprite("coinsIcon"),
            experience = LoadSprite("experianceGain"),
            researchPoints = LoadSprite("researchIcon"),
        };

        EnsureFolder(DataFolder);
        var levelRewards = CreateData<PlayerLevelRewards>($"{DataFolder}/PlayerLevelRewards.asset", r => r.FillWithDefaults());
        var taskDatabase = CreateTasks();

        var gameData = Object.FindFirstObjectByType<GameDataManager>(FindObjectsInactive.Include);
        if (gameData != null)
        {
            Undo.RecordObject(gameData, "Assign rewards data");
            if (gameData.playerLevelRewards == null) gameData.playerLevelRewards = levelRewards;
            if (gameData.taskDatabase == null) gameData.taskDatabase = taskDatabase;
            EditorUtility.SetDirty(gameData);
        }
        else
        {
            Debug.LogWarning("RewardsPanelSetup: no GameDataManager in the scene — assign Player Level Rewards and Task Database on it by hand.");
        }

        var levelPrefab = BuildLevelRewardPrefab();
        var taskPrefab = BuildTaskItemPrefab();

        var panelRect = NewUI("RewardsPanel", canvas.transform);
        Undo.RegisterCreatedObjectUndo(panelRect.gameObject, "Create rewards panel");

        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -100f);
        panelRect.sizeDelta = new Vector2(0f, -200f);
        AddImage(panelRect, LoadSprite("TechPanelBg"), Color.white);

        var tech = canvas.transform.Find("TechnologyPanel");
        if (tech != null) panelRect.SetSiblingIndex(tech.GetSiblingIndex() + 1);

        var title = NewUI("Title", panelRect);
        Place(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 80f));
        AddText(title, "Награды", 48, Light, TextAlignmentOptions.Center, FontStyles.Bold);

        var closeRect = NewUI("CloseRewardsButton", panelRect);
        Place(closeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-90f, -80f), new Vector2(80f, 80f));
        var closeImage = AddImage(closeRect, LoadSprite("button1Background"), Color.white);
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        var closeIcon = NewUI("Image", closeRect);
        Place(closeIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
        AddImage(closeIcon, LoadSprite("exitButtonIcon"), Color.white, raycast: false);

        var levelArea = NewUI("LevelProgress", panelRect);
        Place(levelArea, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(-80f, 330f));

        var levelLabelRect = NewUI("CurrentLevelText", levelArea);
        Place(levelLabelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 60f));
        var levelLabel = AddText(levelLabelRect, "Уровень 0", 36, Light, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var levelScrollRect = NewUI("LevelScrollView", levelArea);
        levelScrollRect.anchorMin = Vector2.zero;
        levelScrollRect.anchorMax = Vector2.one;
        levelScrollRect.offsetMin = Vector2.zero;
        levelScrollRect.offsetMax = new Vector2(0f, -70f);
        AddImage(levelScrollRect, null, new Color(0f, 0f, 0f, 0.001f));

        var levelViewport = NewUI("Viewport", levelScrollRect);
        Stretch(levelViewport);
        levelViewport.gameObject.AddComponent<RectMask2D>();

        var levelContent = NewUI("Content", levelViewport);
        levelContent.anchorMin = new Vector2(0f, 0f);
        levelContent.anchorMax = new Vector2(0f, 1f);
        levelContent.pivot = new Vector2(0f, 0.5f);
        levelContent.anchoredPosition = Vector2.zero;
        levelContent.sizeDelta = new Vector2(0f, 0f);
        var row = levelContent.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 20f;
        row.padding = new RectOffset(40, 40, 0, 0);
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        levelContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var barSprite = LoadSprite("ExpirienceBar");
        var trackRect = NewUI("BarTrack", levelContent);
        Place(trackRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 16f));
        var track = AddImage(trackRect, barSprite, new Color(Light.r, Light.g, Light.b, 0.25f), raycast: false);
        trackRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var fillRect = NewUI("BarFill", levelContent);
        Place(fillRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 16f));
        AddImage(fillRect, barSprite, Light, raycast: false);
        fillRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var sampleLevel = (GameObject)PrefabUtility.InstantiatePrefab(levelPrefab.gameObject, levelContent);

        var levelScroll = levelScrollRect.gameObject.AddComponent<ScrollRect>();
        levelScroll.viewport = levelViewport;
        levelScroll.content = levelContent;
        levelScroll.horizontal = true;
        levelScroll.vertical = false;
        levelScroll.movementType = ScrollRect.MovementType.Elastic;
        levelScroll.scrollSensitivity = 30f;

        var taskScrollRect = NewUI("TaskScrollView", panelRect);
        taskScrollRect.anchorMin = Vector2.zero;
        taskScrollRect.anchorMax = Vector2.one;
        taskScrollRect.offsetMin = new Vector2(40f, 40f);
        taskScrollRect.offsetMax = new Vector2(-40f, -480f);
        AddImage(taskScrollRect, null, new Color(0f, 0f, 0f, 0.001f));

        var taskViewport = NewUI("Viewport", taskScrollRect);
        Stretch(taskViewport);
        taskViewport.gameObject.AddComponent<RectMask2D>();

        var taskContent = NewUI("Content", taskViewport);
        taskContent.anchorMin = new Vector2(0f, 1f);
        taskContent.anchorMax = new Vector2(1f, 1f);
        taskContent.pivot = new Vector2(0.5f, 1f);
        taskContent.anchoredPosition = Vector2.zero;
        taskContent.sizeDelta = Vector2.zero;
        var column = taskContent.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 16f;
        column.padding = new RectOffset(0, 0, 10, 10);
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        taskContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sampleTask = (GameObject)PrefabUtility.InstantiatePrefab(taskPrefab.gameObject, taskContent);

        var taskScroll = taskScrollRect.gameObject.AddComponent<ScrollRect>();
        taskScroll.viewport = taskViewport;
        taskScroll.content = taskContent;
        taskScroll.horizontal = false;
        taskScroll.vertical = true;
        taskScroll.movementType = ScrollRect.MovementType.Elastic;
        taskScroll.scrollSensitivity = 30f;

        var panel = panelRect.gameObject.AddComponent<RewardsPanel>();
        panel.closeButton = closeButton;
        panel.levelScroll = levelScroll;
        panel.levelContent = levelContent;
        panel.levelRewardPrefab = levelPrefab;
        panel.barTrack = track;
        panel.barFill = fillRect;
        panel.currentLevelLabel = levelLabel;
        panel.taskScroll = taskScroll;
        panel.taskContent = taskContent;
        panel.taskItemPrefab = taskPrefab;
        panel.icons = icons;
        EditorUtility.SetDirty(panel);

        ShowSampleReward(sampleLevel, icons.credits);
        ShowSampleReward(sampleTask, icons.experience);

        panelRect.gameObject.SetActive(false);

        var rewardButtonObject = FindDeep(canvas.transform, "RewardButton");
        if (rewardButtonObject != null && rewardButtonObject.TryGetComponent<Button>(out var rewardButton))
        {
            Undo.RecordObject(rewardButton, "Wire reward button");
            UnityEventTools.AddVoidPersistentListener(rewardButton.onClick, panel.Open);
            EditorUtility.SetDirty(rewardButton);
        }
        else
        {
            Debug.LogWarning("RewardsPanelSetup: no 'RewardButton' found — wire its OnClick to RewardsPanel.Open yourself.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = panelRect.gameObject;
    }

    [MenuItem("Tools/Ship Builder/Create Crew Card Popup")]
    public static void CreateCrewCardPopup()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
        if (canvas == null) { Debug.LogError("RewardsPanelSetup: no 'Canvas' in SampleScene."); return; }

        var existing = canvas.transform.Find("CrewCardRewardPopup");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Crew card popup", "The scene already has a CrewCardRewardPopup. Replace it with a new one?", "Replace", "Cancel")) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        font = FindAsset<TMP_FontAsset>("DaysOne-Regular SDF");

        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CrewMemberCard.prefab")?.GetComponent<CrewCardView>();
        if (cardPrefab == null) Debug.LogWarning("RewardsPanelSetup: Assets/Prefabs/UI/CrewMemberCard.prefab has no CrewCardView - assign the Card Prefab by hand.");

        var holder = NewUI("CrewCardRewardPopup", canvas.transform);
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "Create crew card popup");
        Stretch(holder);
        holder.SetAsLastSibling();

        var panelRect = NewUI("Window", holder);
        Stretch(panelRect);
        var dim = AddImage(panelRect, null, new Color(0f, 0f, 0f, 0.7f));
        var close = panelRect.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.targetGraphic = dim;
        var animator = panelRect.gameObject.AddComponent<Animator>();

        var title = NewUI("Title", panelRect);
        Place(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(900f, 100f));
        AddText(title, "Новые члены экипажа", 54, Light, TextAlignmentOptions.Center, FontStyles.Bold);

        var cards = NewUI("Cards", panelRect);
        Place(cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 800f));
        var row = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 30f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        var popup = holder.gameObject.AddComponent<CrewCardRewardPopup>();
        popup.panel = panelRect.gameObject;
        popup.cardsContainer = cards;
        popup.cardPrefab = cardPrefab;
        popup.closeButton = close;
        popup.animator = animator;
        EditorUtility.SetDirty(popup);

        panelRect.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = holder.gameObject;
    }

    private static LevelRewardButtonView BuildLevelRewardPrefab()
    {
        var root = NewUI("LevelRewardButton", null);
        root.sizeDelta = new Vector2(LevelNodeWidth, 210f);
        var background = AddImage(root, LoadSprite("button1Background"), Color.white);
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        var level = NewUI("LevelText", root);
        Place(level, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(0f, 50f));
        var levelText = AddText(level, "1", 34, Light, TextAlignmentOptions.Center, FontStyles.Bold);

        var rewardRect = NewUI("Reward", root);
        Place(rewardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(LevelRowWidth, 130f));
        var row = BuildRewardRow(rewardRect, 76f, 6f, 52f, 26);

        var claimed = NewUI("ClaimedMark", root);
        Place(claimed, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 8f), new Vector2(54f, 54f));
        AddImage(claimed, LoadSprite("likeIcon"), Color.white, raycast: false);
        claimed.gameObject.SetActive(false);

        var view = root.gameObject.AddComponent<LevelRewardButtonView>();
        view.button = button;
        view.levelLabel = levelText;
        view.rewards = row;
        view.claimedMark = claimed.gameObject;

        return SavePrefab(root.gameObject, $"{PrefabFolder}/LevelRewardButton.prefab").GetComponent<LevelRewardButtonView>();
    }

    private static TaskItemView BuildTaskItemPrefab()
    {
        var root = NewUI("TaskItem", null);
        root.sizeDelta = new Vector2(1000f, 190f);
        AddImage(root, null, new Color(0f, 0f, 0f, 0.35f), raycast: false);
        var layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 190f;
        layout.minHeight = 190f;

        var title = NewUI("TitleText", root);
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = new Vector2(1f, 1f);
        title.pivot = new Vector2(0f, 1f);
        title.offsetMin = new Vector2(30f, -85f);
        title.offsetMax = new Vector2(-480f, -15f);
        var titleText = AddText(title, "Уничтожьте 50 блоков оружия", 32, Light, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 22f;
        titleText.fontSizeMax = 32f;

        var barRect = NewUI("ProgressBar", root);
        Place(barRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(30f, 78f), new Vector2(TaskBarWidth, 20f));
        var fill = AddImage(barRect, LoadSprite("ExpirienceBar"), Light, raycast: false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0.4f;
        var border = NewUI("Border", barRect);
        Place(border, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TaskBarWidth + 20f, 40f));
        AddImage(border, LoadSprite("expirianseborder"), Color.white, raycast: false);

        var progress = NewUI("ProgressText", root);
        Place(progress, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 12f), new Vector2(TaskBarWidth, 40f));
        var progressText = AddText(progress, "20 / 50", 26, Light, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);

        var rewardRect = NewUI("Reward", root);
        Place(rewardRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-210f, 0f), new Vector2(TaskRowWidth, 130f));
        var row = BuildRewardRow(rewardRect, 84f, 8f, 56f, 28);

        var buttonRect = NewUI("ClaimButton", root);
        Place(buttonRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(170f, 80f));
        var buttonImage = AddImage(buttonRect, LoadSprite("button1Background"), Color.white);
        var button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        var buttonLabelRect = NewUI("Text (TMP)", buttonRect);
        Stretch(buttonLabelRect);
        var buttonLabel = AddText(buttonLabelRect, "Забрать", 28, Light, TextAlignmentOptions.Center, FontStyles.Bold);
        buttonLabel.raycastTarget = false;

        var view = root.gameObject.AddComponent<TaskItemView>();
        view.titleLabel = titleText;
        view.fillImage = fill;
        view.progressLabel = progressText;
        view.rewards = row;
        view.claimButton = button;
        view.claimLabel = buttonLabel;

        return SavePrefab(root.gameObject, $"{PrefabFolder}/TaskItem.prefab").GetComponent<TaskItemView>();
    }

    private const float LevelNodeWidth = 250f;
    private const float LevelRowWidth = 240f;
    private const float TaskRowWidth = 268f;
    private const float TaskBarWidth = 480f;

    private static RewardRowView BuildRewardRow(RectTransform parent, float slotWidth, float spacing, float iconSize, int textSize)
    {
        var group = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = spacing;
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = false;
        group.childControlHeight = false;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;

        var row = parent.gameObject.AddComponent<RewardRowView>();
        row.slots = new RewardView[RewardRowView.MaxRewards];
        for (int i = 0; i < RewardRowView.MaxRewards; i++)
        {
            var slot = NewUI($"Reward{i + 1}", parent);
            slot.sizeDelta = new Vector2(slotWidth, parent.sizeDelta.y);
            row.slots[i] = BuildRewardView(slot, iconSize, textSize);
        }

        return row;
    }

    private static RewardView BuildRewardView(RectTransform parent, float iconSize, int textSize)
    {
        var icon = NewUI("Icon", parent);
        Place(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(iconSize, iconSize));
        var iconImage = AddImage(icon, null, Color.white, raycast: false);
        iconImage.preserveAspect = true;

        var amount = NewUI("Amount", parent);
        Place(amount, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(0f, 46f));
        var amountText = AddText(amount, "100", textSize, Light, TextAlignmentOptions.Center, FontStyles.Bold);

        var view = parent.gameObject.AddComponent<RewardView>();
        view.icon = iconImage;
        view.amountLabel = amountText;
        return view;
    }

    [MenuItem("Tools/Ship Builder/Upgrade Rewards Panel Prefabs (3 rewards)")]
    public static void UpgradePrefabs()
    {
        font = FindAsset<TMP_FontAsset>("DaysOne-Regular SDF");

        UpgradePrefab($"{PrefabFolder}/LevelRewardButton.prefab", (root) =>
        {
            var view = root.GetComponent<LevelRewardButtonView>();
            if (view == null || view.rewards != null) return false;

            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(LevelNodeWidth, rootRect.sizeDelta.y);
            var row = ReplaceRewardBlock(root.transform, new Vector2(LevelRowWidth, 130f), 76f, 6f, 52f, 26);
            if (row == null) return false;
            view.rewards = row;
            return true;
        });

        UpgradePrefab($"{PrefabFolder}/TaskItem.prefab", (root) =>
        {
            var view = root.GetComponent<TaskItemView>();
            if (view == null || view.rewards != null) return false;

            var row = ReplaceRewardBlock(root.transform, new Vector2(TaskRowWidth, 130f), 84f, 8f, 56f, 28);
            if (row == null) return false;
            view.rewards = row;

            if (root.transform.Find("TitleText") is RectTransform title) title.offsetMax = new Vector2(-480f, title.offsetMax.y);
            if (root.transform.Find("ProgressBar") is RectTransform bar)
            {
                bar.sizeDelta = new Vector2(TaskBarWidth, bar.sizeDelta.y);
                if (bar.Find("Border") is RectTransform border) border.sizeDelta = new Vector2(TaskBarWidth + 20f, border.sizeDelta.y);
            }

            if (root.transform.Find("ProgressText") is RectTransform progress) progress.sizeDelta = new Vector2(TaskBarWidth, progress.sizeDelta.y);
            if (root.transform.Find("ClaimButton") is RectTransform claim) claim.sizeDelta = new Vector2(170f, claim.sizeDelta.y);
            return true;
        });

        AssetDatabase.SaveAssets();
    }

    private static void UpgradePrefab(string path, System.Func<GameObject, bool> change)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"RewardsPanelSetup: {path} not found."); return; }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (change(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RewardRowView ReplaceRewardBlock(Transform root, Vector2 size, float slotWidth, float spacing, float iconSize, int textSize)
    {
        if (!(root.Find("Reward") is RectTransform reward)) return null;

        var oldView = reward.GetComponent<RewardView>();
        if (oldView != null) Object.DestroyImmediate(oldView);
        for (int i = reward.childCount - 1; i >= 0; i--) Object.DestroyImmediate(reward.GetChild(i).gameObject);

        reward.sizeDelta = size;
        return BuildRewardRow(reward, slotWidth, spacing, iconSize, textSize);
    }

    private static void ShowSampleReward(GameObject item, Sprite sprite)
    {
        var view = item.GetComponentInChildren<RewardView>(true);
        if (view != null && view.icon != null)
        {
            view.icon.sprite = sprite;
            view.icon.enabled = sprite != null;
        }
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static TaskDatabase CreateTasks()
    {
        const string databasePath = DataFolder + "/TaskDatabase.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TaskDatabase>(databasePath);
        if (existing != null) return existing;

        EnsureFolder(DataFolder + "/Tasks");

        var database = ScriptableObject.CreateInstance<TaskDatabase>();
        AssetDatabase.CreateAsset(database, databasePath);

        void Add(string name, System.Action<TaskDefinition> setup)
        {
            var task = ScriptableObject.CreateInstance<TaskDefinition>();
            task.name = name;
            task.id = name;
            setup(task);
            AssetDatabase.CreateAsset(task, $"{DataFolder}/Tasks/{name}.asset");
            database.tasks.Add(task);
        }

        LevelDefinition Level(int n) => AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{LevelsFolder}/Campaign1Level{n}.asset");

        RewardData R(RewardType type, int amount) => new RewardData(type, amount);
        System.Collections.Generic.List<RewardData> L(params RewardData[] items) => new System.Collections.Generic.List<RewardData>(items);

        Add("DestroyWeapons25", t => { t.kind = TaskKind.DestroyBlocks; t.anyBlockType = false; t.blockType = ModuleType.Weapon; t.targetCount = 25; t.rewards = L(R(RewardType.Credits, 1500)); });
        Add("DestroyArmor50", t => { t.kind = TaskKind.DestroyBlocks; t.anyBlockType = false; t.blockType = ModuleType.Armor; t.targetCount = 50; t.rewards = L(R(RewardType.Experience, 150), R(RewardType.Credits, 1000)); });
        Add("DestroyGenerators20", t => { t.kind = TaskKind.DestroyBlocks; t.anyBlockType = false; t.blockType = ModuleType.Generator; t.targetCount = 20; t.rewards = L(R(RewardType.Coins, 5), R(RewardType.RandomCrewCard, 1)); });
        Add("DestroyCockpits10", t => { t.kind = TaskKind.DestroyBlocks; t.anyBlockType = false; t.blockType = ModuleType.Cockpit; t.targetCount = 10; t.rewards = L(R(RewardType.ResearchPoints, 5)); });
        Add("DestroyAny200", t => { t.kind = TaskKind.DestroyBlocks; t.anyBlockType = true; t.targetCount = 200; t.rewards = L(R(RewardType.Credits, 5000), R(RewardType.RandomCrewCard, 1)); });
        Add("CompleteLevel1", t => { t.kind = TaskKind.CompleteCampaignLevel; t.level = Level(1); t.rewards = L(R(RewardType.Credits, 1000)); });
        Add("CompleteLevel5", t => { t.kind = TaskKind.CompleteCampaignLevel; t.level = Level(5); t.rewards = L(R(RewardType.Experience, 200), R(RewardType.RandomCrewCard, 1)); });
        Add("CompleteLevel10", t => { t.kind = TaskKind.CompleteCampaignLevel; t.level = Level(10); t.rewards = L(R(RewardType.Coins, 10), R(RewardType.Credits, 2500), R(RewardType.RandomCrewCard, 1)); });
        Add("SurvivalWave5", t => { t.kind = TaskKind.SurvivalWave; t.targetWave = 5; t.rewards = L(R(RewardType.Credits, 2500)); });
        Add("SurvivalWave10", t => { t.kind = TaskKind.SurvivalWave; t.targetWave = 10; t.rewards = L(R(RewardType.Coins, 10), R(RewardType.RandomCrewCard, 2)); });

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        return database;
    }

    private static T CreateData<T>(string path, System.Action<T> init) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<T>();
        init?.Invoke(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
        if (parent != null) go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool raycast = true)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static TextMeshProUGUI AddText(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Sprite LoadSprite(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != name) continue;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        Debug.LogWarning($"RewardsPanelSetup: sprite '{name}' not found — left empty.");
        return null;
    }

    private static T FindAsset<T>(string name) where T : Object
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:" + typeof(T).Name))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    private static GameObject FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }
}
#endif
