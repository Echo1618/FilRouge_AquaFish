using System;
using UnityEngine;

public class AnaglyphDisplay : MonoBehaviour
{
    // =========================================================
    // DISPLAY SETTINGS
    // =========================================================

    [Header("Image Overlay")]
    [Range(0f, 1f)]
    [SerializeField] private float backgroundBrightness = 0.4f;

    [Range(0f, 1f)]
    [SerializeField] private float maskOpacity = 0.7f;


    [Header("Detection Overlay")]
    [Range(1, 10)]
    [SerializeField] private int boxThickness = 3;

    [Range(1, 15)]
    [SerializeField] private int eyePointRadius = 5;

    [Range(1, 15)]
    [SerializeField] private int centerPointRadius = 5;

    [Range(1, 5)]
    [SerializeField] private int lineThickness = 1;


    // =========================================================
    // INTERNAL DATA
    // =========================================================

    private int width;
    private int height;

    private Texture2D displayTexture;
    private Color32[] displayPixels;

    private GameObject displayQuad;
    private Renderer displayRenderer;
    private Material displayMaterial;

    private bool initialized;

    private string imageName = "";


    // =========================================================
    // INITIALIZATION
    // =========================================================

    public void Initialize(int imageWidth, int imageHeight)
    {
        width = imageWidth;
        height = imageHeight;


        displayPixels = new Color32[
            width * height
        ];


        // Destroy the previous texture when switching to an image
        // with another resolution.
        if (displayTexture != null)
            Destroy(displayTexture);


        displayTexture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false
        );


        if (displayQuad == null)
            CreateDisplay();
        else
            UpdateDisplay();


        initialized = true;
    }


    // =========================================================
    // IMAGE NAME
    // =========================================================

    public void SetImageName(string value)
    {
        imageName = value;
    }


    // =========================================================
    // MAIN DISPLAY
    // =========================================================

    public void DisplayDetection(
        Color32[] original,
        Color32[] mask,
        HeadDetectorV2.DetectionResult detection)
    {
        if (!initialized)
            return;

        if (original == null || mask == null)
            return;

        if (original.Length != displayPixels.Length ||
            mask.Length != displayPixels.Length)
            return;


        // ---------------------------------------------------------
        // Build original image + semi-transparent detection mask.
        // ---------------------------------------------------------

        for (int i = 0; i < displayPixels.Length; i++)
        {
            Color32 source = original[i];
            Color32 overlay = mask[i];


            bool hasMask =
                overlay.r > 0 ||
                overlay.g > 0 ||
                overlay.b > 0;


            if (hasMask)
            {
                // Keep the real image visible underneath the mask.
                displayPixels[i] = Blend(
                    source,
                    overlay,
                    maskOpacity
                );
            }
            else
            {
                // Darken pixels outside the detected areas.
                displayPixels[i] = Darken(
                    source,
                    backgroundBrightness
                );
            }
        }


        // ---------------------------------------------------------
        // Draw detection geometry.
        // ---------------------------------------------------------

        if (detection.detected)
        {
            Color32 red =
                new Color32(255, 0, 0, 255);

            Color32 blue =
                new Color32(0, 100, 255, 255);

            Color32 green =
                new Color32(0, 255, 0, 255);

            Color32 yellow =
                new Color32(255, 255, 0, 255);


            // Red eye bounding box.
            DrawBoundingBox(
                detection.redBox,
                red,
                boxThickness
            );


            // Blue eye bounding box.
            DrawBoundingBox(
                detection.blueBox,
                blue,
                boxThickness
            );


            // Eye centers.
            DrawPoint(
                detection.redEye,
                red,
                eyePointRadius
            );

            DrawPoint(
                detection.blueEye,
                blue,
                eyePointRadius
            );


            // Eye-to-eye line.
            DrawLine(
                detection.redEye,
                detection.blueEye,
                green,
                lineThickness
            );


            // Head center.
            DrawPoint(
                detection.headCenter,
                yellow,
                centerPointRadius
            );
        }


        displayTexture.SetPixels32(
            displayPixels
        );

        displayTexture.Apply();
    }


    // =========================================================
    // COLOR COMPOSITION
    // =========================================================

    private Color32 Blend(
        Color32 background,
        Color32 overlay,
        float opacity)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(
                Mathf.Lerp(
                    background.r,
                    overlay.r,
                    opacity
                )
            ),

            (byte)Mathf.RoundToInt(
                Mathf.Lerp(
                    background.g,
                    overlay.g,
                    opacity
                )
            ),

            (byte)Mathf.RoundToInt(
                Mathf.Lerp(
                    background.b,
                    overlay.b,
                    opacity
                )
            ),

            255
        );
    }


    private Color32 Darken(
        Color32 color,
        float brightness)
    {
        return new Color32(
            (byte)(color.r * brightness),
            (byte)(color.g * brightness),
            (byte)(color.b * brightness),
            255
        );
    }


    // =========================================================
    // BOUNDING BOX
    // =========================================================

    private void DrawBoundingBox(
        RectInt box,
        Color32 color,
        int thickness)
    {
        for (int t = 0; t < thickness; t++)
        {
            int minX = box.xMin + t;
            int maxX = box.xMax - 1 - t;

            int minY = box.yMin + t;
            int maxY = box.yMax - 1 - t;


            if (minX > maxX || minY > maxY)
                break;


            for (int x = minX; x <= maxX; x++)
            {
                SetPixel(
                    x,
                    minY,
                    color
                );

                SetPixel(
                    x,
                    maxY,
                    color
                );
            }


            for (int y = minY; y <= maxY; y++)
            {
                SetPixel(
                    minX,
                    y,
                    color
                );

                SetPixel(
                    maxX,
                    y,
                    color
                );
            }
        }
    }


    // =========================================================
    // POINT
    // =========================================================

    private void DrawPoint(
        Vector2 position,
        Color32 color,
        int radius)
    {
        int centerX =
            Mathf.RoundToInt(position.x);

        int centerY =
            Mathf.RoundToInt(position.y);


        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y >
                    radius * radius)
                    continue;


                SetPixel(
                    centerX + x,
                    centerY + y,
                    color
                );
            }
        }
    }


    // =========================================================
    // LINE
    // =========================================================

    private void DrawLine(
        Vector2 start,
        Vector2 end,
        Color32 color,
        int thickness)
    {
        int steps = Mathf.CeilToInt(
            Vector2.Distance(
                start,
                end
            )
        );


        if (steps <= 0)
            return;


        for (int i = 0; i <= steps; i++)
        {
            float t =
                i / (float)steps;


            Vector2 point =
                Vector2.Lerp(
                    start,
                    end,
                    t
                );


            DrawPoint(
                point,
                color,
                thickness
            );
        }
    }


    // =========================================================
    // PIXEL
    // =========================================================

    private void SetPixel(
        int x,
        int y,
        Color32 color)
    {
        if (x < 0 || x >= width ||
            y < 0 || y >= height)
            return;


        displayPixels[
            y * width + x
        ] = color;
    }


    // =========================================================
    // DISPLAY OBJECT
    // =========================================================

    private void CreateDisplay()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError(
                "AnaglyphDisplay: Main Camera not found."
            );

            return;
        }


        Shader shader =
            Shader.Find("Unlit/Texture");


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit"
                );
        }


        if (shader == null)
        {
            Debug.LogError(
                "AnaglyphDisplay: Unlit shader not found."
            );

            return;
        }


        displayQuad =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        displayQuad.name =
            "Detection Display";


        displayRenderer =
            displayQuad.GetComponent<Renderer>();


        displayMaterial =
            new Material(shader);


        displayRenderer.material =
            displayMaterial;


        displayMaterial.mainTexture =
            displayTexture;


        Destroy(
            displayQuad.GetComponent<Collider>()
        );


        UpdateDisplay();
    }


    private void UpdateDisplay()
    {
        if (displayQuad == null)
            return;


        Camera cam = Camera.main;

        if (cam == null)
            return;


        const float distance = 5f;


        float screenHeight =
            cam.orthographic
                ? cam.orthographicSize * 2f
                : 2f *
                  distance *
                  Mathf.Tan(
                      cam.fieldOfView *
                      0.5f *
                      Mathf.Deg2Rad
                  );


        float screenWidth =
            screenHeight * cam.aspect;


        float imageAspect =
            width / (float)height;


        float displayWidth;
        float displayHeight;


        // Fit the image inside the camera without distortion.
        if (imageAspect > cam.aspect)
        {
            displayWidth =
                screenWidth;

            displayHeight =
                screenWidth /
                imageAspect;
        }
        else
        {
            displayHeight =
                screenHeight;

            displayWidth =
                screenHeight *
                imageAspect;
        }


        displayQuad.transform.position =
            cam.transform.position +
            cam.transform.forward *
            distance;


        displayQuad.transform.rotation =
            cam.transform.rotation;


        displayQuad.transform.localScale =
            new Vector3(
                displayWidth,
                displayHeight,
                1f
            );


        if (displayMaterial != null)
        {
            displayMaterial.mainTexture =
                displayTexture;
        }
    }


    // =========================================================
    // IMAGE NAME GUI
    // =========================================================

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(imageName))
            return;

        if (width <= 0 || height <= 0)
            return;


        // Determine the exact screen rectangle occupied by the image.
        float screenAspect =
            Screen.width / (float)Screen.height;

        float imageAspect =
            width / (float)height;


        float displayedWidth;
        float displayedHeight;


        if (imageAspect > screenAspect)
        {
            displayedWidth =
                Screen.width;

            displayedHeight =
                displayedWidth /
                imageAspect;
        }
        else
        {
            displayedHeight =
                Screen.height;

            displayedWidth =
                displayedHeight *
                imageAspect;
        }


        float left =
            (Screen.width - displayedWidth) *
            0.5f;

        float top =
            (Screen.height - displayedHeight) *
            0.5f;


        Rect textRect = new Rect(
            left + 12f,
            top + displayedHeight - 36f,
            displayedWidth - 24f,
            30f
        );


        GUIStyle shadow =
            new GUIStyle(
                GUI.skin.label
            );

        shadow.fontSize = 20;
        shadow.alignment =
            TextAnchor.LowerLeft;

        shadow.normal.textColor =
            Color.black;


        GUIStyle foreground =
            new GUIStyle(shadow);

        foreground.normal.textColor =
            Color.white;


        Rect shadowRect =
            new Rect(
                textRect.x + 2f,
                textRect.y + 2f,
                textRect.width,
                textRect.height
            );


        GUI.Label(
            shadowRect,
            imageName,
            shadow
        );


        GUI.Label(
            textRect,
            imageName,
            foreground
        );
    }


    // =========================================================
    // CLEANUP
    // =========================================================

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