using UnityEngine;

public class AnaglyphDisplay : MonoBehaviour
{
    private int width, height;

    private Texture2D displayTexture;
    private Color32[] displayPixels;

    private bool initialized;


    public void Initialize(int imageWidth, int imageHeight)
    {
        width = imageWidth;
        height = imageHeight;

        displayPixels = new Color32[width * height];
        displayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        CreateDisplay();

        initialized = true;
    }

    private void DrawBoundingBox(
    Color32[] pixels,
    RectInt box,
    Color32 color,
    int thickness = 3)
    {
        for (int t = 0; t < thickness; t++)
        {
            int minX = box.xMin + t;
            int maxX = box.xMax - 1 - t;
            int minY = box.yMin + t;
            int maxY = box.yMax - 1 - t;

            if (minX > maxX || minY > maxY) break;

            for (int x = minX; x <= maxX; x++)
            {
                SetPixel(pixels, x, minY, color);
                SetPixel(pixels, x, maxY, color);
            }

            for (int y = minY; y <= maxY; y++)
            {
                SetPixel(pixels, minX, y, color);
                SetPixel(pixels, maxX, y, color);
            }
        }
    }

    private void SetPixel(
        Color32[] pixels,
        int x,
        int y,
        Color32 color)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        pixels[y * width + x] = color;
    }

    private void DrawPoint(
        Color32[] pixels,
        Vector2 position,
        Color32 color,
        int radius = 5)
    {
        int centerX = Mathf.RoundToInt(position.x);
        int centerY = Mathf.RoundToInt(position.y);

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > radius * radius) continue;

                SetPixel(
                    pixels,
                    centerX + x,
                    centerY + y,
                    color
                );
            }
        }
    }

    private void DrawLine(
        Color32[] pixels,
        Vector2 start,
        Vector2 end,
        Color32 color,
        int thickness = 1)
    {
        int steps = Mathf.CeilToInt(
            Vector2.Distance(start, end)
        );

        if (steps <= 0) return;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;

            int x = Mathf.RoundToInt(
                Mathf.Lerp(start.x, end.x, t)
            );

            int y = Mathf.RoundToInt(
                Mathf.Lerp(start.y, end.y, t)
            );

            DrawPoint(
                pixels,
                new Vector2(x, y),
                color,
                thickness
            );
        }
    }

    public void DisplayDetection(
        Color32[] input,
        HeadDetector.DetectionResult detection)
    {
        if (!initialized || input == null) return;

        System.Array.Copy(
            input,
            displayPixels,
            input.Length
        );

        if (detection.detected)
        {
            Color32 red = new Color32(255, 0, 0, 255);
            Color32 blue = new Color32(0, 100, 255, 255);
            Color32 line = new Color32(0, 255, 0, 255);

            DrawBoundingBox(
                displayPixels,
                detection.redBox,
                red
            );

            DrawBoundingBox(
                displayPixels,
                detection.blueBox,
                blue
            );

            DrawPoint(
                displayPixels,
                detection.redEye,
                red,
                5
            );

            DrawPoint(
                displayPixels,
                detection.blueEye,
                blue,
                5
            );

            DrawLine(
                displayPixels,
                detection.redEye,
                detection.blueEye,
                line,
                1
            );

            DrawPoint(
                displayPixels,
                detection.headCenter,
                line,
                5
            );
        }

        Display(displayPixels);
    }

    public void Display(Color32[] pixels)
    {
        if (!initialized || pixels == null) return;
        if (pixels.Length != width * height) return;

        displayTexture.SetPixels32(pixels);
        displayTexture.Apply();
    }


    public void DisplayMask(byte[] mask)
    {
        if (!initialized || mask == null || mask.Length != displayPixels.Length) return;

        for (int i = 0; i < mask.Length; i++)
        {
            byte value = mask[i];
            displayPixels[i] = new Color32(value, value, value, 255);
        }

        Display(displayPixels);
    }


    public void DisplayMasks(byte[] redMask, byte[] cyanMask)
    {
        if (!initialized || redMask == null || cyanMask == null) return;
        if (redMask.Length != displayPixels.Length || cyanMask.Length != displayPixels.Length) return;

        for (int i = 0; i < displayPixels.Length; i++)
        {
            bool red = redMask[i] > 0;
            bool cyan = cyanMask[i] > 0;

            if (red && cyan) displayPixels[i] = new Color32(255, 255, 255, 255);
            else if (red) displayPixels[i] = new Color32(255, 0, 0, 255);
            else if (cyan) displayPixels[i] = new Color32(0, 255, 255, 255);
            else displayPixels[i] = new Color32(0, 0, 0, 255);
        }

        Display(displayPixels);
    }


    private void CreateDisplay()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("Main Camera introuvable.");
            return;
        }

        Shader shader = Shader.Find("Unlit/Texture");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            Debug.LogError("Shader Unlit introuvable.");
            return;
        }

        const float distance = 5f;

        float screenHeight = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float screenWidth = screenHeight * cam.aspect;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Detection Display";

        quad.transform.position = cam.transform.position + cam.transform.forward * distance;
        quad.transform.rotation = cam.transform.rotation;
        quad.transform.localScale = new Vector3(screenWidth, screenHeight, 1f);

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.material = new Material(shader);
        renderer.material.mainTexture = displayTexture;

        Destroy(quad.GetComponent<Collider>());
    }
}