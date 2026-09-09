using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unity presentation component for detection diagnostics.
///
/// Responsibilities:
/// - Own the diagnostic Texture2D.
/// - Display it inside a Canvas RawImage.
/// - Preserve the source image aspect ratio.
/// - Display frame and detection information.
/// - Delegate all pixel composition to DetectionOverlay.
///
/// This class does not perform any detection or image processing.
/// </summary>
public sealed class DetectionView : MonoBehaviour
{
    // =========================================================
    // UI REFERENCES
    // =========================================================

    [Header("UI References")]
    [SerializeField] private RawImage previewImage;

    [Tooltip("Optional. Keeps the webcam/test image aspect ratio.")]
    [SerializeField] private AspectRatioFitter aspectRatioFitter;

    [Tooltip("Optional. Displays the current frame/image name.")]
    [SerializeField] private TMP_Text frameNameText;

    [Tooltip("Optional. Displays blob and detection information.")]
    [SerializeField] private TMP_Text diagnosticsText;

    [Tooltip("Optional. Root GameObject used to show/hide the whole preview.")]
    [SerializeField] private GameObject previewRoot;


    // =========================================================
    // DIAGNOSTIC VIEW
    // =========================================================

    [Header("Diagnostic View")]
    [SerializeField] private DiagnosticViewMode viewMode =
        DiagnosticViewMode.ProcessedMask;

    [SerializeField] private DetectionOverlaySettings overlaySettings =
        new DetectionOverlaySettings();

    [SerializeField] private bool showFrameName = true;
    [SerializeField] private bool showDiagnosticsText = true;


    // =========================================================
    // INTERNAL DATA
    // =========================================================

    private DetectionOverlay overlay;

    private Texture2D displayTexture;

    private int width;
    private int height;

    private bool initialized;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        overlay = new DetectionOverlay(
            overlaySettings
        );

        if (previewRoot == null &&
            previewImage != null)
        {
            previewRoot =
                previewImage.gameObject;
        }

        RefreshTextVisibility();
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
    /// Displays one processed frame and its diagnostic information.
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


        // DetectionOverlay performs all pixel composition.
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
                "DetectionView: Invalid overlay output."
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
    // VIEW CONTROL
    // =========================================================

    /// <summary>
    /// Shows or hides the complete diagnostic preview.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (previewRoot != null)
        {
            previewRoot.SetActive(
                visible
            );
        }
    }


    /// <summary>
    /// Returns whether the diagnostic preview is currently visible.
    /// </summary>
    public bool IsVisible()
    {
        return previewRoot != null &&
               previewRoot.activeSelf;
    }


    /// <summary>
    /// Changes the diagnostic visualization mode at runtime.
    /// </summary>
    public void SetViewMode(
        DiagnosticViewMode mode)
    {
        viewMode = mode;
    }


    // =========================================================
    // TEXTURE
    // =========================================================

    /// <summary>
    /// Creates or recreates the diagnostic texture when
    /// the source resolution changes.
    /// </summary>
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


        width = newWidth;
        height = newHeight;


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

    /// <summary>
    /// Keeps the diagnostic image from being stretched.
    /// </summary>
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
    // DIAGNOSTICS TEXT
    // =========================================================

    private void UpdateDiagnosticsText(
        DetectionDiagnostics diagnostics,
        DetectionResult result)
    {
        if (diagnosticsText == null)
            return;


        if (!showDiagnosticsText)
        {
            diagnosticsText.gameObject.SetActive(
                false
            );

            return;
        }


        diagnosticsText.gameObject.SetActive(
            true
        );


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
    // TEXT VISIBILITY
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


    // =========================================================
    // EDITOR VALIDATION
    // =========================================================

    private void OnValidate()
    {
        RefreshTextVisibility();
    }
}