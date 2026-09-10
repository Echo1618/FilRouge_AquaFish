using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Optional preview. Horizontal mirroring affects only RawImage UVs.</summary>
public sealed class DetectionView : MonoBehaviour
{
    [Header("UI references")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private AspectRatioFitter aspectRatioFitter;
    [SerializeField] private GameObject previewRoot;
    [Header("Display")]
    [SerializeField] private DiagnosticViewMode viewMode = DiagnosticViewMode.ProcessedMask;
    [SerializeField] private DetectionOverlaySettings overlaySettings = new DetectionOverlaySettings();
    [SerializeField] private bool mirrorPreviewX = true;
    [Min(1f)] [SerializeField] private float previewRefreshRate = 15f;
    [Min(1f)] [SerializeField] private float textRefreshRate = 5f;
    [SerializeField] private bool showFrameName = true;
    [SerializeField] private bool showDiagnosticsText = true;
    [SerializeField] private float frameNameFontSize = 18f;
    [SerializeField] private float diagnosticsFontSize = 16f;

    private DetectionOverlay overlay;
    private Texture2D displayTexture;
    private TMP_Text frameNameText, diagnosticsText;
    private CanvasGroup[] canvasGroups;
    private float nextPreview, nextText;

    private void Awake()
    {
        if (previewImage == null) { Debug.LogWarning("DetectionView: no preview RawImage assigned.", this); return; }
        if (previewRoot == null) previewRoot = previewImage.gameObject;
        if (aspectRatioFitter == null) aspectRatioFitter = previewImage.GetComponent<AspectRatioFitter>();
        canvasGroups = previewImage.GetComponentsInParent<CanvasGroup>(true);
        overlay = new DetectionOverlay(overlaySettings);
        frameNameText = CreateLabel("Frame name", TextAlignmentOptions.BottomLeft, frameNameFontSize, false);
        diagnosticsText = CreateLabel("Detection diagnostics", TextAlignmentOptions.TopLeft, diagnosticsFontSize, true);
        previewImage.raycastTarget = false;
    }

    public bool IsVisible()
    {
        if (!isActiveAndEnabled || previewImage == null || !previewImage.isActiveAndEnabled ||
            (previewRoot != null && !previewRoot.activeInHierarchy) || previewImage.color.a <= 0f) return false;
        if (previewImage.canvas != null && !previewImage.canvas.isActiveAndEnabled) return false;
        if (canvasGroups != null)
            for (int i = 0; i < canvasGroups.Length; i++)
            {
                CanvasGroup group = canvasGroups[i];
                if (group == null || !group.enabled) continue;
                if (group.alpha <= 0f) return false;
                if (group.ignoreParentGroups) break;
            }
        return true;
    }
    public bool ShouldRefresh(float now) => IsVisible() && now >= nextPreview;
    public void SetVisible(bool visible) { if (previewRoot != null) previewRoot.SetActive(visible); nextPreview = 0f; }
    public void SetViewMode(DiagnosticViewMode mode) { viewMode = mode; nextPreview = 0f; }

    public void Show(ImageFrame frame, DetectionDiagnostics diagnostics, DetectionResult result)
    {
        if (!frame.IsValid || !IsVisible() || overlay == null) return;
        nextPreview = Time.unscaledTime + 1f / Mathf.Max(1f, previewRefreshRate);
        if (displayTexture == null || displayTexture.width != frame.Width || displayTexture.height != frame.Height)
        {
            if (displayTexture != null) Destroy(displayTexture);
            displayTexture = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false)
            { name = "Detection preview", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        }
        displayTexture.SetPixels32(overlay.Compose(frame, diagnostics, result, viewMode));
        displayTexture.Apply(false);
        previewImage.texture = displayTexture;
        previewImage.uvRect = mirrorPreviewX ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
        if (aspectRatioFitter != null)
        {
            aspectRatioFitter.aspectRatio = (float)frame.Width / frame.Height;
        }
        if (Time.unscaledTime < nextText) return;
        nextText = Time.unscaledTime + 1f / Mathf.Max(1f, textRefreshRate);
        frameNameText.gameObject.SetActive(showFrameName);
        diagnosticsText.gameObject.SetActive(showDiagnosticsText);
        if (showFrameName) frameNameText.text = frame.Name;
        if (showDiagnosticsText) diagnosticsText.text = $"Red {diagnostics.RedCandidates.Count} | Blue {diagnostics.BlueCandidates.Count} | Regions {diagnostics.RegionCount}\nPair {(result.detected ? "YES" : "NO")} | score {(result.hasCandidate ? result.pairScore.ToString("F3") : "n/a")}";
    }

    private TMP_Text CreateLabel(string name, TextAlignmentOptions alignment, float size, bool top)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(previewImage.transform, false);
        TMP_Text label = go.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        if (label.font == null) label.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        label.text = string.Empty;
        label.fontSize = size;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.color = Color.white;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(0f, top ? 1f : 0f);
        rect.anchoredPosition = new Vector2(6f, top ? -6f : 6f);
        rect.sizeDelta = new Vector2(-12f, top ? 52f : 26f);
        return label;
    }

    public void Clear()
    {
        nextPreview = nextText = 0f;
        if (previewImage != null) previewImage.texture = null;
        if (frameNameText != null) frameNameText.text = string.Empty;
        if (diagnosticsText != null) diagnosticsText.text = string.Empty;
    }
    private void OnDestroy()
    {
        if (previewImage != null && previewImage.texture == displayTexture) previewImage.texture = null;
        if (displayTexture != null) Destroy(displayTexture);
        if (frameNameText != null) Destroy(frameNameText.gameObject);
        if (diagnosticsText != null) Destroy(diagnosticsText.gameObject);
    }
}
