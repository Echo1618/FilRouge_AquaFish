using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays detection diagnostics inside a Canvas RawImage.
///
/// The component automatically creates:
/// - A frame name label in the bottom-left corner.
/// - A diagnostics label in the top-left corner.
///
/// Pixel composition is delegated to DetectionOverlay.
/// </summary>
public sealed class DetectionView : MonoBehaviour
{
    // =========================================================
    // UI REFERENCES
    // =========================================================

    [Header("UI References")]

    [SerializeField]
    private RawImage previewImage;

    [Tooltip("Optional. Keeps the webcam/test image aspect ratio.")]
    [SerializeField]
    private AspectRatioFitter aspectRatioFitter;

    [Tooltip("Optional root used to show/hide the whole preview.")]
    [SerializeField]
    private GameObject previewRoot;


    // =========================================================
    // DIAGNOSTIC VIEW
    // =========================================================

    [Header("Diagnostic View")]

    [SerializeField]
    private DiagnosticViewMode viewMode =
        DiagnosticViewMode.ProcessedMask;

    [SerializeField]
    private DetectionOverlaySettings overlaySettings =
        new DetectionOverlaySettings();

    [SerializeField]
    private bool showFrameName = true;

    [SerializeField]
    private bool showDiagnosticsText = true;


    // =========================================================
    // TEXT SETTINGS
    // =========================================================

    [Header("Text")]

    [SerializeField]
    private float frameNameFontSize = 18f;

    [SerializeField]
    private float diagnosticsFontSize = 16f;


    // =========================================================
    // INTERNAL DATA
    // =========================================================

    private DetectionOverlay overlay;

    private Texture2D displayTexture;

    private TMP_Text frameNameText;
    private TMP_Text diagnosticsText;

    private int width;
    private int height;

    private bool initialized;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        overlay =
            new DetectionOverlay(
                overlaySettings
            );


        if (previewRoot == null &&
            previewImage != null)
        {
            previewRoot =
                previewImage.gameObject;
        }


        CreateRuntimeUI();
    }


    private void OnDestroy()
    {
        if (displayTexture != null)
        {
            Destroy(displayTexture);
            displayTexture = null;
        }
    }


    // =========================================================
    // PUBLIC DISPLAY
    // =========================================================

    /// <summary>
    /// Displays one processed frame and its diagnostics.
    /// </summary>
    public void Show(
        ImageFrame frame,
        DetectionDiagnostics diagnostics,
        DetectionResult result)
    {
        if (!frame.IsValid)
            return;


        if (previewImage == null)
        {
            Debug.LogError(
                "DetectionView: Preview Image is not assigned."
            );

            return;
        }


        EnsureTexture(
            frame.Width,
            frame.Height
        );


        Color32[] pixels =
            overlay.Compose(
                frame,
                diagnostics,
                result,
                viewMode
            );


        if (pixels == null ||
            pixels.Length != width * height)
        {
            Debug.LogError(
                "DetectionView: invalid overlay output."
            );

            return;
        }


        displayTexture.SetPixels32(
            pixels
        );

        displayTexture.Apply(false);


        UpdateFrameName(
            frame
        );

        UpdateDiagnosticsText(
            diagnostics,
            result
        );
    }


    // =========================================================
    // RUNTIME UI CREATION
    // =========================================================

    /// <summary>
    /// Creates the text labels automatically on the preview.
    /// </summary>
    private void CreateRuntimeUI()
    {
        if (previewImage == null)
            return;


        Transform parent =
            previewImage.transform;


        frameNameText =
            CreateText(
                "FrameName",
                parent,
                TextAlignmentOptions.BottomLeft,
                frameNameFontSize
            );


        RectTransform frameRect =
            frameNameText.rectTransform;

        frameRect.anchorMin =
            new Vector2(0f, 0f);

        frameRect.anchorMax =
            new Vector2(1f, 0f);

        frameRect.pivot =
            new Vector2(0f, 0f);

        frameRect.anchoredPosition =
            new Vector2(8f, 6f);

        frameRect.sizeDelta =
            new Vector2(-16f, 28f);


        diagnosticsText =
            CreateText(
                "Diagnostics",
                parent,
                TextAlignmentOptions.TopLeft,
                diagnosticsFontSize
            );


        RectTransform diagnosticsRect =
            diagnosticsText.rectTransform;

        diagnosticsRect.anchorMin =
            new Vector2(0f, 1f);

        diagnosticsRect.anchorMax =
            new Vector2(1f, 1f);

        diagnosticsRect.pivot =
            new Vector2(0f, 1f);

        diagnosticsRect.anchoredPosition =
            new Vector2(8f, -6f);

        diagnosticsRect.sizeDelta =
            new Vector2(-16f, 28f);


        RefreshTextVisibility();
    }


    /// <summary>
    /// Creates one TextMeshProUGUI element.
    /// </summary>
    private TMP_Text CreateText(
    string objectName,
    Transform parent,
    TextAlignmentOptions alignment,
    float fontSize)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();


        // Load the standard TMP font directly from Resources.
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(
            "Fonts & Materials/LiberationSans SDF"
        );

        if (font == null)
        {
            Debug.LogError(
                "DetectionView: LiberationSans SDF was not found. " +
                "Import TextMeshPro Essential Resources."
            );

            return text;
        }


        text.font = font;

        text.text = "";
        text.fontSize = fontSize;
        text.alignment = alignment;

        text.color = Color.white;

        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;


        // The font is assigned before accessing its material.
        text.outlineColor =
            new Color32(0, 0, 0, 220);

        text.outlineWidth = 0.2f;


        return text;
    }


    // =========================================================
    // VIEW CONTROL
    // =========================================================

    public void SetVisible(bool visible)
    {
        if (previewRoot != null)
            previewRoot.SetActive(visible);
    }


    public bool IsVisible()
    {
        return previewRoot != null &&
               previewRoot.activeSelf;
    }


    public void SetViewMode(
        DiagnosticViewMode mode)
    {
        viewMode = mode;
    }


    // =========================================================
    // TEXTURE
    // =========================================================

    private void EnsureTexture(
        int newWidth,
        int newHeight)
    {
        if (initialized &&
            displayTexture != null &&
            width == newWidth &&
            height == newHeight)
        {
            return;
        }


        width =
            newWidth;

        height =
            newHeight;


        if (displayTexture != null)
        {
            Destroy(
                displayTexture
            );
        }


        displayTexture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false
            );


        displayTexture.name =
            "Detection Preview Texture";

        displayTexture.filterMode =
            FilterMode.Bilinear;

        displayTexture.wrapMode =
            TextureWrapMode.Clamp;


        previewImage.texture =
            displayTexture;


        UpdateAspectRatio();


        initialized = true;
    }


    // =========================================================
    // ASPECT RATIO
    // =========================================================

    private void UpdateAspectRatio()
    {
        if (aspectRatioFitter == null ||
            height <= 0)
        {
            return;
        }


        aspectRatioFitter.aspectRatio =
            width / (float)height;
    }


    // =========================================================
    // FRAME NAME
    // =========================================================

    private void UpdateFrameName(
        ImageFrame frame)
    {
        if (frameNameText == null)
            return;


        frameNameText.text =
            frame.Name;


        frameNameText.gameObject.SetActive(
            showFrameName &&
            !string.IsNullOrEmpty(frame.Name)
        );
    }


    // =========================================================
    // DIAGNOSTICS
    // =========================================================

    private void UpdateDiagnosticsText(
        DetectionDiagnostics diagnostics,
        DetectionResult result)
    {
        if (diagnosticsText == null)
            return;


        if (!showDiagnosticsText)
        {
            diagnosticsText.gameObject.SetActive(false);
            return;
        }


        diagnosticsText.gameObject.SetActive(true);


        int redCount =
            diagnostics != null
                ? diagnostics.RedCandidates.Count
                : 0;


        int blueCount =
            diagnostics != null
                ? diagnostics.BlueCandidates.Count
                : 0;


        int regionCount =
            diagnostics != null
                ? diagnostics.RegionCount
                : 0;


        diagnosticsText.text =
            $"Red: {redCount} | " +
            $"Blue: {blueCount} | " +
            $"Regions: {regionCount} | " +
            $"Pair: {(result.detected ? "YES" : "NO")}";
    }


    // =========================================================
    // VISIBILITY
    // =========================================================

    private void RefreshTextVisibility()
    {
        if (frameNameText != null)
        {
            frameNameText.gameObject.SetActive(
                showFrameName
            );
        }


        if (diagnosticsText != null)
        {
            diagnosticsText.gameObject.SetActive(
                showDiagnosticsText
            );
        }
    }
}