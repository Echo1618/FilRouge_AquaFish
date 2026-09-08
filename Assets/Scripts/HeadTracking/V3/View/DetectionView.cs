using UnityEngine;

/// <summary>
/// Unity presentation component. It owns the Texture2D, display quad and GUI labels.
/// Pixel composition itself is delegated to DetectionOverlay.
/// </summary>
public sealed class DetectionView : MonoBehaviour
{
    [Header("Diagnostic view")]
    [SerializeField] private DiagnosticViewMode viewMode = DiagnosticViewMode.ProcessedMask;
    [SerializeField] private DetectionOverlaySettings overlaySettings = new DetectionOverlaySettings();
    [SerializeField] private bool showDiagnosticsText = true;

    private DetectionOverlay overlay;

    private int width;
    private int height;

    private Texture2D displayTexture;
    private GameObject displayQuad;
    private Material displayMaterial;

    private string frameName = string.Empty;
    private string diagnosticsText = string.Empty;

    private GUIStyle labelStyle;
    private GUIStyle shadowStyle;

    private void Awake()
    {
        overlay = new DetectionOverlay(overlaySettings);
    }

    public void Show(
        ImageFrame frame,
        DetectionDiagnostics diagnostics,
        DetectionResult result)
    {
        if (!frame.IsValid)
            return;

        EnsureDisplay(frame.Width, frame.Height);

        Color32[] pixels = overlay.Compose(frame, diagnostics, result, viewMode);

        displayTexture.SetPixels32(pixels);
        displayTexture.Apply();

        frameName = frame.Name;
        diagnosticsText =
            $"Red: {diagnostics.RedCandidates.Count} blobs | " +
            $"Blue: {diagnostics.BlueCandidates.Count} blobs | " +
            $"Pair: {(result.Detected ? "YES" : "NO")}";
    }

    private void EnsureDisplay(int newWidth, int newHeight)
    {
        if (displayTexture != null && width == newWidth && height == newHeight)
            return;

        width = newWidth;
        height = newHeight;

        if (displayTexture != null)
            Destroy(displayTexture);

        displayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        if (displayQuad == null)
            CreateDisplay();

        if (displayMaterial != null)
            displayMaterial.mainTexture = displayTexture;

        UpdateDisplayTransform();
    }

    private void CreateDisplay()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("DetectionView: Main Camera not found.");
            return;
        }

        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            Debug.LogError("DetectionView: Unlit shader not found.");
            return;
        }

        displayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        displayQuad.name = "Detection Display";

        Renderer renderer = displayQuad.GetComponent<Renderer>();
        displayMaterial = new Material(shader);
        renderer.material = displayMaterial;

        Collider collider = displayQuad.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    private void UpdateDisplayTransform()
    {
        if (displayQuad == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        const float distance = 5f;

        float screenHeight = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float screenWidth = screenHeight * cam.aspect;
        float imageAspect = width / (float)height;

        float displayWidth;
        float displayHeight;

        if (imageAspect > cam.aspect)
        {
            displayWidth = screenWidth;
            displayHeight = screenWidth / imageAspect;
        }
        else
        {
            displayHeight = screenHeight;
            displayWidth = screenHeight * imageAspect;
        }

        displayQuad.transform.position = cam.transform.position + cam.transform.forward * distance;
        displayQuad.transform.rotation = cam.transform.rotation;
        displayQuad.transform.localScale = new Vector3(displayWidth, displayHeight, 1f);
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(frameName) || width <= 0 || height <= 0)
            return;

        EnsureGuiStyles();
        GetDisplayedRect(out float left, out float top, out float displayedWidth, out float displayedHeight);

        Rect nameRect = new Rect(
            left + 12f,
            top + displayedHeight - 36f,
            displayedWidth - 24f,
            30f
        );

        DrawLabel(nameRect, frameName);

        if (showDiagnosticsText && !string.IsNullOrEmpty(diagnosticsText))
        {
            Rect statsRect = new Rect(
                left + 12f,
                top + 8f,
                displayedWidth - 24f,
                30f
            );

            DrawLabel(statsRect, diagnosticsText);
        }
    }

    private void DrawLabel(Rect rect, string text)
    {
        Rect shadowRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
        GUI.Label(shadowRect, text, shadowStyle);
        GUI.Label(rect, text, labelStyle);
    }

    private void EnsureGuiStyles()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.LowerLeft
        };
        labelStyle.normal.textColor = Color.white;

        shadowStyle = new GUIStyle(labelStyle);
        shadowStyle.normal.textColor = Color.black;
    }

    private void GetDisplayedRect(
        out float left,
        out float top,
        out float displayedWidth,
        out float displayedHeight)
    {
        float screenAspect = Screen.width / (float)Screen.height;
        float imageAspect = width / (float)height;

        if (imageAspect > screenAspect)
        {
            displayedWidth = Screen.width;
            displayedHeight = displayedWidth / imageAspect;
        }
        else
        {
            displayedHeight = Screen.height;
            displayedWidth = displayedHeight * imageAspect;
        }

        left = (Screen.width - displayedWidth) * 0.5f;
        top = (Screen.height - displayedHeight) * 0.5f;
    }

    private void OnDestroy()
    {
        if (displayTexture != null)
            Destroy(displayTexture);

        if (displayMaterial != null)
            Destroy(displayMaterial);

        if (displayQuad != null)
            Destroy(displayQuad);
    }
}
