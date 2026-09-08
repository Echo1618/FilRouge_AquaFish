using System;
using UnityEngine;

public class WebCam : MonoBehaviour
{
    private enum ColorFilterMode { None, RGB, HSV }

    [Header("Effects")]
    [SerializeField] private ColorFilterMode colorFilter = ColorFilterMode.None;
    [SerializeField] private bool greyScale;
    [SerializeField] private bool useThreshold;
    [SerializeField] private bool showBoundingBox;
    [SerializeField] private bool showCenterPoint;

    [Header("Threshold")]
    [Range(0, 255)]
    [SerializeField] private int threshold = 128;

    [Header("RGB Filter")]
    [SerializeField] private Vector3 minColor = new(100, 0, 0);
    [SerializeField] private Vector3 maxColor = new(255, 100, 100);

    [Header("HSV Filter")]
    [Range(0, 1)] [SerializeField] private float minHue = 0f;
    [Range(0, 1)] [SerializeField] private float maxHue = 0.1f;
    [Range(0, 1)] [SerializeField] private float minSaturation = 0.5f;
    [Range(0, 1)] [SerializeField] private float minValue = 0.2f;

    [Header("Bounding Box")]
    [SerializeField] private Color32 boxColor = new(255, 0, 0, 255);
    [Range(1, 10)] [SerializeField] private int boxSize = 3;

    [Header("Center Point")]
    [SerializeField] private Color32 pointColor = new(0, 255, 0, 255);
    [Range(1, 20)] [SerializeField] private int centerPointSize = 5;

    private WebCamTexture webcamTexture;
    private Texture2D displayTexture;
    private Material displayMaterial;

    private Color32[] webCamPixels;
    private Color32[] bufferA;
    private Color32[] bufferB;

    private int width, height;
    private bool initialized;


    private void Start()
    {
        displayMaterial = CreateDisplay();
        webcamTexture = new WebCamTexture();
        displayMaterial.mainTexture = webcamTexture;
        webcamTexture.Play();
    }


    private void Update()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying) return;
        if (webcamTexture.width <= 16 || webcamTexture.height <= 16) return;
        if (!initialized) InitializeProcessing();

        webcamTexture.GetPixels32(webCamPixels);

        Color32[] current = webCamPixels;
        Color32[] output = bufferA;

        RectInt boundingBox = new();
        bool objectDetected = false;


        // COLOR FILTER
        if (colorFilter == ColorFilterMode.RGB)
        {
            ColorFilterRGBProcess(current, output);
            Advance(ref current, ref output);

            boundingBox = FindBoundingBox(current);
            objectDetected = boundingBox.width > 0;
        }
        else if (colorFilter == ColorFilterMode.HSV)
        {
            ColorFilterHSVProcess(current, output);
            Advance(ref current, ref output);

            boundingBox = FindBoundingBox(current);
            objectDetected = boundingBox.width > 0;
        }


        // GREYSCALE
        if (greyScale)
        {
            GreyScaleProcess(current, output);
            Advance(ref current, ref output);
        }


        // THRESHOLD
        if (useThreshold)
        {
            ThresholdProcess(current, output);
            Advance(ref current, ref output);

            if (colorFilter == ColorFilterMode.None)
            {
                boundingBox = FindBoundingBox(current);
                objectDetected = boundingBox.width > 0;
            }
        }


        // OVERLAYS
        if (showBoundingBox && objectDetected)
        {
            BoundingBoxProcess(current, output, boundingBox);
            Advance(ref current, ref output);
        }

        if (showCenterPoint && objectDetected)
        {
            CenterPointProcess(current, output, boundingBox);
            Advance(ref current, ref output);
        }


        displayTexture.SetPixels32(current);
        displayTexture.Apply();
    }


    private void InitializeProcessing()
    {
        width = webcamTexture.width;
        height = webcamTexture.height;

        int pixelCount = width * height;

        webCamPixels = new Color32[pixelCount];
        bufferA = new Color32[pixelCount];
        bufferB = new Color32[pixelCount];

        displayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        displayMaterial.mainTexture = displayTexture;

        initialized = true;
    }


    private void Advance(ref Color32[] current, ref Color32[] output)
    {
        current = output;
        output = ReferenceEquals(current, bufferA) ? bufferB : bufferA;
    }


    // =========================================================
    // GREYSCALE
    // =========================================================

    private void GreyScaleProcess(Color32[] input, Color32[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color32 p = input[i];
            byte grey = (byte)(p.r * 0.299f + p.g * 0.587f + p.b * 0.114f);
            output[i] = new Color32(grey, grey, grey, 255);
        }
    }


    // =========================================================
    // THRESHOLD
    // =========================================================

    private void ThresholdProcess(Color32[] input, Color32[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color32 p = input[i];
            byte grey = (byte)(p.r * 0.299f + p.g * 0.587f + p.b * 0.114f);
            byte value = grey > threshold ? (byte)255 : (byte)0;

            output[i] = new Color32(value, value, value, 255);
        }
    }


    // =========================================================
    // RGB FILTER
    // =========================================================

    private void ColorFilterRGBProcess(Color32[] input, Color32[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color32 p = input[i];

            bool accepted =
                p.r >= minColor.x && p.r <= maxColor.x &&
                p.g >= minColor.y && p.g <= maxColor.y &&
                p.b >= minColor.z && p.b <= maxColor.z;

            output[i] = accepted ? p : new Color32(0, 0, 0, 255);
        }
    }


    // =========================================================
    // HSV FILTER
    // =========================================================

    private void ColorFilterHSVProcess(Color32[] input, Color32[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color.RGBToHSV(input[i], out float h, out float s, out float v);

            bool hueAccepted = minHue <= maxHue
                ? h >= minHue && h <= maxHue
                : h >= minHue || h <= maxHue;

            bool accepted = hueAccepted && s >= minSaturation && v >= minValue;

            output[i] = accepted ? input[i] : new Color32(0, 0, 0, 255);
        }
    }


    // =========================================================
    // BOUNDING BOX
    // =========================================================

    private RectInt FindBoundingBox(Color32[] input)
    {
        int minX = width, minY = height;
        int maxX = -1, maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color32 p = input[y * width + x];

                if (p.r == 0 && p.g == 0 && p.b == 0) continue;

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < 0) return new RectInt();

        return new RectInt(
            minX,
            minY,
            maxX - minX + 1,
            maxY - minY + 1
        );
    }


    private void BoundingBoxProcess(Color32[] input, Color32[] output, RectInt box)
    {
        Array.Copy(input, output, input.Length);

        for (int thickness = 0; thickness < boxSize; thickness++)
        {
            int minX = box.xMin + thickness;
            int maxX = box.xMax - 1 - thickness;
            int minY = box.yMin + thickness;
            int maxY = box.yMax - 1 - thickness;

            if (minX > maxX || minY > maxY) break;

            for (int x = minX; x <= maxX; x++)
            {
                output[minY * width + x] = boxColor;
                output[maxY * width + x] = boxColor;
            }

            for (int y = minY; y <= maxY; y++)
            {
                output[y * width + minX] = boxColor;
                output[y * width + maxX] = boxColor;
            }
        }
    }


    // =========================================================
    // CENTER POINT
    // =========================================================

    private void CenterPointProcess(Color32[] input, Color32[] output, RectInt box)
    {
        Array.Copy(input, output, input.Length);

        int centerX = box.x + box.width / 2;
        int centerY = box.y + box.height / 2;

        for (int y = -centerPointSize; y <= centerPointSize; y++)
        {
            for (int x = -centerPointSize; x <= centerPointSize; x++)
            {
                if (x * x + y * y > centerPointSize * centerPointSize) continue;

                int pixelX = centerX + x;
                int pixelY = centerY + y;

                if (pixelX < 0 || pixelX >= width || pixelY < 0 || pixelY >= height) continue;

                output[pixelY * width + pixelX] = pointColor;
            }
        }
    }


    // =========================================================
    // DISPLAY
    // =========================================================

    private Material CreateDisplay()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("Aucune Main Camera trouvée.");
            return null;
        }

        const float distance = 5f;

        float screenHeight = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float screenWidth = screenHeight * cam.aspect;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "WebCam Display";

        quad.transform.position = cam.transform.position + cam.transform.forward * distance;
        quad.transform.rotation = cam.transform.rotation;
        quad.transform.localScale = new Vector3(screenWidth, screenHeight, 1f);

        Material material = CreateDisplayMaterial();
        quad.GetComponent<Renderer>().material = material;

        Destroy(quad.GetComponent<Collider>());

        return material;
    }


    private Material CreateDisplayMaterial()
    {
        Shader shader = Shader.Find("Unlit/Texture");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            Debug.LogError("Aucun shader Unlit trouvé.");
            return null;
        }

        return new Material(shader);
    }


    private void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
            webcamTexture.Stop();
    }
}