using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FleetPanelController : MonoBehaviour
{
    [Header("Slides")]
    [Tooltip("The Scroll View's Content: the slides are laid out side by side in it.")]
    public RectTransform content;
    [Tooltip("The area one slide fills. Empty = the Scroll View's Viewport.")]
    public RectTransform viewport;
    [Tooltip("Optional: copied into every ship slide (a frame, a background...).")]
    public GameObject shipSlideTemplate;
    [Tooltip("The \"buy a new hangar\" slide's content (e.g. NewShipContainer). Shown as the last slide while the fleet is not full.")]
    public GameObject buySlide;
    [Tooltip("Seconds a slide change takes.")]
    [Min(0.01f)] public float slideSeconds = 0.3f;

    [Header("Opening / closing")]
    [Tooltip("Optional: closes the panel (e.g. CloseFleetButton). Or wire any button to Close().")]
    public Button closeButton;

    [Header("Turning")]
    public Button leftButton;
    public Button rightButton;

    [Header("Ship slide")]
    [Tooltip("The ship's name, editable.")]
    public TMP_InputField nameInput;
    [Min(1)] public int maxNameLength = 20;
    [Tooltip("Makes this ship the one that goes into battle — shown only for another ship.")]
    public Button selectButton;
    [Tooltip("Opens the builder on this ship.")]
    public Button buildButton;
    [Tooltip("Optional: shown on the ship that goes into battle.")]
    public GameObject selectedMark;
    [Tooltip("Optional: the ship's grid size, e.g. \"5x5\".")]
    public TMP_Text gridSizeLabel;
    [Tooltip("Optional: ONE fixed picture of the ship over the slides. Empty = every ship slide gets its own picture, which moves with it: " +
             "the RawImage of the Ship Slide Template if it has one, otherwise a RawImage made to fill the slide (minus Preview Margin).")]
    public RawImage shipPreview;
    [Tooltip("Room left around a picture made by the panel, in the slide's units.")]
    [Min(0f)] public float previewMargin = 20f;
    [Tooltip("A layer no other object uses (0-31). While the panel is open the ship's block sprites are put on it and the picture's camera " +
             "sees only it: just the blocks — no background, no particles, no shield circle.")]
    [Range(0, 31)] public int previewLayer = 31;
    [Tooltip("Empty space around the ship in the preview, in grid cells.")]
    [Min(0f)] public float previewPaddingCells = 1f;
    [Tooltip("Objects shown on ship slides only (the hangar upgrades, the grid size...).")]
    public GameObject[] shipOnly;

    [Header("Buy slide")]
    public Button buyButton;
    public TMP_Text buyPriceLabel;
    [Tooltip("Objects shown on the buy slide only.")]
    public GameObject[] buyOnly;

    [Header("Scene (found by themselves if empty)")]
    public MainMenuFlowController flow;
    [Tooltip("The ship stats window: shown on ship slides.")]
    public SlideOutPanel statsPanel;
    public MessageToast messageToast;
    public string notEnoughMessage = "Not enough resources";

    private readonly List<RectTransform> slides = new();
    private readonly List<RawImage> slidePictures = new();
    private int index;
    private bool enteringBuilder;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private ShipGrid previewShip;
    private bool previewDirty = true;
    private readonly Dictionary<GameObject, int> originalLayers = new();

    public void Open() => gameObject.SetActive(true);

    public void Close() => gameObject.SetActive(false);

    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    private static GameDataManager Data => GameDataManager.Instance;
    private int ShipCount => Data != null ? Data.Fleet.Count : 0;
    private bool OnBuySlide => index >= ShipCount;

    private void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<MainMenuFlowController>(FindObjectsInactive.Include);
        if (statsPanel == null && flow != null) statsPanel = flow.statsPanel;
        if (messageToast == null && flow != null) messageToast = flow.messageToast;

        if (viewport == null && content != null)
        {
            var scroll = content.GetComponentInParent<ScrollRect>(true);
            if (scroll != null)
            {
                viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
                scroll.enabled = false;
            }
        }

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (leftButton != null) leftButton.onClick.AddListener(() => Turn(-1));
        if (rightButton != null) rightButton.onClick.AddListener(() => Turn(1));
        if (selectButton != null) selectButton.onClick.AddListener(SelectShown);
        if (buildButton != null) buildButton.onClick.AddListener(BuildShown);
        if (buyButton != null) buyButton.onClick.AddListener(Buy);
        if (nameInput != null)
        {
            nameInput.characterLimit = maxNameLength;
            nameInput.onEndEdit.AddListener(Rename);
        }
    }

    private void OnEnable()
    {
        if (Data == null) return;
        Data.OnFleetChanged += Refresh;
        Data.OnHangarLevelChanged += Refresh;

        enteringBuilder = false;
        BuildSlides();
        index = Data.SelectedShipIndex;
        if (flow != null && Data.FocusedShipIndex != index) flow.ShowFleetShip(index);
        StillLook();
        SnapContent();
        Refresh();
    }

    private void OnDisable()
    {
        if (Data == null) return;
        Data.OnFleetChanged -= Refresh;
        Data.OnHangarLevelChanged -= Refresh;

        if (previewCamera != null) previewCamera.enabled = false;
        RestoreLayers();
        if (previewShip != null) previewShip.OnShipChanged -= MarkPreviewDirty;
        previewShip = null;
        if (enteringBuilder) return;

        if (statsPanel != null) statsPanel.Hide();
        if (flow != null && Data.FocusedShipIndex != Data.SelectedShipIndex) flow.ShowFleetShip(Data.SelectedShipIndex);
        else if (flow != null && flow.playerShip != null) flow.playerShip.SetViewMode(ShipViewMode.Preview);
        Data.ResetFocus();
    }

    private void OnDestroy()
    {
        if (previewCamera != null) Destroy(previewCamera.gameObject);
        if (previewTexture != null) previewTexture.Release();
    }

    private void Turn(int direction)
    {
        int last = Data.CanAddFleetShip ? ShipCount : ShipCount - 1;
        int next = Mathf.Clamp(index + direction, 0, last);
        if (next == index) return;

        index = next;
        if (!OnBuySlide && flow != null) flow.ShowFleetShip(index);
        StillLook();
        Refresh();
    }

    private void SelectShown()
    {
        if (OnBuySlide) return;
        Data.SelectShip(index);
    }

    private void BuildShown()
    {
        if (OnBuySlide || flow == null) return;
        if (Data.FocusedShipIndex != index) flow.ShowFleetShip(index);

        enteringBuilder = true;
        gameObject.SetActive(false);
        flow.OnBuildShipPressed();
    }

    private void Buy()
    {
        if (!Data.TryBuyFleetShip(out int newIndex))
        {
            if (messageToast != null && Data.CanAddFleetShip) messageToast.Show(notEnoughMessage);
            return;
        }

        BuildSlides();
        index = newIndex;
        if (flow != null) flow.ShowFleetShip(index);
        StillLook();
        Refresh();
    }

    private void Rename(string newName)
    {
        if (OnBuySlide) return;
        Data.RenameShip(index, newName);
    }

    private void Refresh()
    {
        if (Data == null || !isActiveAndEnabled) return;
        bool buy = OnBuySlide;
        bool selected = !buy && index == Data.SelectedShipIndex;

        if (leftButton != null) leftButton.interactable = index > 0;
        if (rightButton != null) rightButton.interactable = index < (Data.CanAddFleetShip ? ShipCount : ShipCount - 1);

        if (nameInput != null)
        {
            nameInput.gameObject.SetActive(!buy);
            if (!buy && !nameInput.isFocused) nameInput.SetTextWithoutNotify(Data.Fleet[index].name);
        }

        if (selectButton != null) selectButton.gameObject.SetActive(!buy && !selected);
        if (buildButton != null) buildButton.gameObject.SetActive(!buy);
        if (selectedMark != null) selectedMark.SetActive(selected);

        if (gridSizeLabel != null && flow != null && flow.playerShip != null)
            gridSizeLabel.text = $"{flow.playerShip.GridWidth}x{flow.playerShip.GridHeight}";

        if (buyButton != null) buyButton.gameObject.SetActive(buy);
        if (buyPriceLabel != null) buyPriceLabel.text = Data.NextFleetShipCost.ToString();

        foreach (var go in shipOnly) if (go != null) go.SetActive(!buy);
        foreach (var go in buyOnly) if (go != null) go.SetActive(buy);

        if (statsPanel != null)
        {
            if (buy) statsPanel.Hide();
            else statsPanel.Show();
        }
    }

    private void BuildSlides()
    {
        if (content == null) return;

        if (buySlide != null) buySlide.transform.SetParent(content, false);
        foreach (var slide in slides)
            if (slide != null) Destroy(slide.gameObject);
        slides.Clear();
        slidePictures.Clear();

        for (int i = 0; i < ShipCount; i++)
        {
            var slide = NewSlide($"ShipSlide{i + 1}");
            RawImage picture = null;
            if (shipSlideTemplate != null)
            {
                var copy = Instantiate(shipSlideTemplate, slide);
                copy.SetActive(true);
                var copyRect = (RectTransform)copy.transform;
                copyRect.anchorMin = Vector2.zero;
                copyRect.anchorMax = Vector2.one;
                copyRect.offsetMin = copyRect.offsetMax = Vector2.zero;
                picture = copy.GetComponentInChildren<RawImage>(true);
            }

            if (shipPreview == null && picture == null) picture = NewPicture(slide);
            slidePictures.Add(picture);
        }

        if (Data.CanAddFleetShip)
        {
            var slide = NewSlide("BuySlide");
            if (buySlide != null)
            {
                buySlide.transform.SetParent(slide, false);
                buySlide.SetActive(true);
            }
        }
        else if (buySlide != null) buySlide.SetActive(false);

        if (shipSlideTemplate != null && shipSlideTemplate.scene.IsValid()) shipSlideTemplate.SetActive(false);
        index = Mathf.Clamp(index, 0, slides.Count - 1);
    }

    private RawImage NewPicture(RectTransform slide)
    {
        var go = new GameObject("ShipPicture", typeof(RectTransform), typeof(RawImage)) { layer = slide.gameObject.layer };
        var rect = (RectTransform)go.transform;
        rect.SetParent(slide, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(previewMargin, previewMargin);
        rect.offsetMax = new Vector2(-previewMargin, -previewMargin);

        var image = go.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private RectTransform NewSlide(string name)
    {
        var slide = new GameObject(name, typeof(RectTransform)) { layer = content.gameObject.layer };
        var rect = (RectTransform)slide.transform;
        rect.SetParent(content, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        slides.Add(rect);
        return rect;
    }

    private float SlideWidth => viewport != null ? viewport.rect.width : content.rect.width;

    private void LayoutSlides()
    {
        float width = SlideWidth;
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.sizeDelta = new Vector2(width * slides.Count, 0f);

        for (int i = 0; i < slides.Count; i++)
        {
            if (slides[i] == null) continue;
            slides[i].sizeDelta = new Vector2(width, 0f);
            slides[i].anchoredPosition = new Vector2(i * width, 0f);
        }
    }

    private void SnapContent()
    {
        if (content == null) return;
        LayoutSlides();
        content.anchoredPosition = new Vector2(-index * SlideWidth, 0f);
    }

    private void LateUpdate()
    {
        if (content == null || Data == null) return;

        LayoutSlides();
        float width = SlideWidth;
        float target = -index * width;
        float x = content.anchoredPosition.x;
        float step = width / slideSeconds * Time.unscaledDeltaTime;
        x = Mathf.MoveTowards(x, target, Mathf.Max(step, Mathf.Abs(target - x) * 10f * Time.unscaledDeltaTime));
        content.anchoredPosition = new Vector2(x, 0f);

        float settled = width > 0f ? 1f - Mathf.Clamp01(Mathf.Abs(target - x) / width) : 1f;
        UpdatePreview(!OnBuySlide, settled);
    }

    private void StillLook()
    {
        if (flow == null || flow.playerShip == null) return;

        if (previewShip != flow.playerShip)
        {
            if (previewShip != null) previewShip.OnShipChanged -= MarkPreviewDirty;
            previewShip = flow.playerShip;
            previewShip.OnShipChanged += MarkPreviewDirty;
        }

        previewShip.SetViewMode(ShipViewMode.Building);
        previewDirty = true;
    }

    private void MarkPreviewDirty() => previewDirty = true;

    private void PutBlocksOnPreviewLayer()
    {
        previewDirty = false;
        if (previewShip == null) return;

        foreach (var sprite in previewShip.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var go = sprite.gameObject;
            if (go.layer == previewLayer) continue;

            var shield = sprite.GetComponentInParent<ShieldModule>();
            if (shield != null && shield.zoneRenderer == sprite) continue;

            if (!originalLayers.ContainsKey(go)) originalLayers[go] = go.layer;
            go.layer = previewLayer;
        }

        foreach (var light in FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>())
        {
            var go = light.gameObject;
            if (go.layer == previewLayer || go.GetComponent<Renderer>() != null) continue;
            if (!originalLayers.ContainsKey(go)) originalLayers[go] = go.layer;
            go.layer = previewLayer;
        }
    }

    private void RestoreLayers()
    {
        foreach (var pair in originalLayers)
            if (pair.Key != null) pair.Key.layer = pair.Value;
        originalLayers.Clear();
    }

    private void UpdatePreview(bool visible, float alpha)
    {
        var picture = shipPreview;
        for (int i = 0; i < slidePictures.Count; i++)
        {
            var slidePicture = slidePictures[i];
            if (slidePicture == null) continue;
            bool current = visible && i == index;
            slidePicture.enabled = current;
            if (current) picture = slidePicture;
        }

        if (picture == null) return;
        if (shipPreview != null) shipPreview.gameObject.SetActive(visible);
        if (!visible || flow == null || flow.playerShip == null)
        {
            if (previewCamera != null) previewCamera.enabled = false;
            return;
        }

        if (!EnsurePreviewCamera(picture)) return;
        if (previewDirty) PutBlocksOnPreviewLayer();
        previewCamera.enabled = true;
        if (picture.texture != previewTexture) picture.texture = previewTexture;

        if (picture != shipPreview) alpha = 1f;

        var grid = flow.playerShip;
        Vector3 center = grid.GetShipWorldCenter();
        Vector2 size = grid.GetShipWorldSize();
        float padding = previewPaddingCells * grid.cellSize;
        float aspect = Mathf.Max(0.01f, (float)previewTexture.width / previewTexture.height);

        previewCamera.transform.position = new Vector3(center.x, center.y, previewCamera.transform.position.z);
        previewCamera.orthographicSize = Mathf.Max(size.y * 0.5f + padding, (size.x * 0.5f + padding) / aspect, grid.cellSize);

        var color = picture.color;
        color.a = alpha;
        picture.color = color;
    }

    private bool EnsurePreviewCamera(RawImage picture)
    {
        if (previewCamera != null) return true;

        var main = Camera.main;
        if (main == null)
        {
            Debug.LogWarning("FleetPanelController: no camera tagged MainCamera - the ship picture cannot be drawn.", this);
            return false;
        }

        var rect = picture.rectTransform.rect;
        if (rect.width < 1f || rect.height < 1f) return false;
        var canvas = picture.canvas;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        int width = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 128, 2048);
        int height = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 128, 2048);
        previewTexture = new RenderTexture(width, height, 24) { name = "FleetShipPreview" };

        var cameraObject = new GameObject("FleetPreviewCamera");
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.CopyFrom(main);
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << previewLayer;
        previewCamera.targetTexture = previewTexture;
        cameraObject.transform.position = main.transform.position;
        return true;
    }
}
