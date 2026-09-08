using System;
using UnityEngine;

public class WebCam : MonoBehaviour
{
    // =========================================================
    // TYPES
    // =========================================================

    private enum ColorFilterMode
    {
        None,
        RGB,
        HSV
    }


    // =========================================================
    // EFFECTS
    // =========================================================

    [Header("Effects")]
    [SerializeField] private ColorFilterMode colorFilter = ColorFilterMode.None;
    [SerializeField] private bool greyScale = false;
    [SerializeField] private bool useThreshold = false;
    [SerializeField] private bool showBoundingBox = false;
    [SerializeField] private bool showCenterPoint = false;


    // =========================================================
    // THRESHOLD
    // =========================================================

    [Header("Threshold")]
    [Range(0, 255)]
    [SerializeField] private int threshold = 128;


    // =========================================================
    // RGB FILTER
    // =========================================================

    [Header("RGB Color Filter")]
    [SerializeField] private Vector3 minColor =
        new Vector3(100, 0, 0);

    [SerializeField] private Vector3 maxColor =
        new Vector3(255, 100, 100);


    // =========================================================
    // HSV FILTER
    // =========================================================

    [Header("HSV Color Filter")]

    [Range(0f, 1f)]
    [SerializeField] private float minHue = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float maxHue = 0.1f;

    [Range(0f, 1f)]
    [SerializeField] private float minSaturation = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float minValue = 0.2f;


    // =========================================================
    // BOUNDING BOX / CENTER
    // =========================================================

    [Header("Bounding Box")]
    [SerializeField] private Color32 boxColor =
        new Color32(255, 0, 0, 255);

    [Range(1, 10)]
    [SerializeField] private int boxSize = 3;


    [Header("Center Point")]
    [SerializeField] private Color32 pointColor =
        new Color32(0, 255, 0, 255);

    [Range(1, 20)]
    [SerializeField] private int centerPointSize = 5;


    // =========================================================
    // WEBCAM
    // =========================================================

    private WebCamTexture webcamTexture;

    private int width;
    private int height;

    private bool initialized;


    // =========================================================
    // IMAGE DATA
    // =========================================================

    private Color32[] webCamPixels;

    // Deux buffers réutilisés par tous les traitements
    private Color32[] bufferA;
    private Color32[] bufferB;


    // =========================================================
    // DISPLAY
    // =========================================================

    private Texture2D displayTexture;
    private Material displayMaterial;


    // =========================================================
    // UNITY
    // =========================================================

    private void Start()
    {
        displayMaterial = CreateDisplay();

        webcamTexture = new WebCamTexture();

        // Affichage direct en attendant l'initialisation
        displayMaterial.mainTexture = webcamTexture;

        webcamTexture.Play();
    }


    private void Update()
    {
        if (webcamTexture == null ||
            !webcamTexture.isPlaying)
            return;

        if (webcamTexture.width <= 16 ||
            webcamTexture.height <= 16)
            return;


        if (!initialized)
            InitializeProcessing();


        // =====================================================
        // ACQUISITION
        // =====================================================

        webcamTexture.GetPixels32(webCamPixels);


        Color32[] current = webCamPixels;
        Color32[] output = bufferA;

        RectInt boundingBox = new RectInt();
        bool objectDetected = false;


        // =====================================================
        // COLOR FILTER
        // =====================================================

        switch (colorFilter)
        {
            case ColorFilterMode.RGB:

                ColorFilterRGBProcess(
                    current,
                    output
                );

                Advance(
                    ref current,
                    ref output
                );

                boundingBox =
                    FindBoundingBox(current);

                objectDetected =
                    boundingBox.width > 0;

                break;


            case ColorFilterMode.HSV:

                ColorFilterHSVProcess(
                    current,
                    output
                );

                Advance(
                    ref current,
                    ref output
                );

                boundingBox =
                    FindBoundingBox(current);

                objectDetected =
                    boundingBox.width > 0;

                break;
        }


        // =====================================================
        // GREYSCALE
        // =====================================================

        if (greyScale)
        {
            GreyScaleProcess(
                current,
                output
            );

            Advance(
                ref current,
                ref output
            );
        }


        // =====================================================
        // THRESHOLD
        // =====================================================

        if (useThreshold)
        {
            ThresholdProcess(
                current,
                output
            );

            Advance(
                ref current,
                ref output
            );


            // Si aucun filtre couleur n'est actif,
            // le threshold peut servir de masque de détection
            if (colorFilter == ColorFilterMode.None)
            {
                boundingBox =
                    FindBoundingBox(current);

                objectDetected =
                    boundingBox.width > 0;
            }
        }


        // =====================================================
        // BOUNDING BOX OVERLAY
        // =====================================================

        if (showBoundingBox && objectDetected)
        {
            BoundingBoxProcess(
                current,
                output,
                boundingBox
            );

            Advance(
                ref current,
                ref output
            );
        }


        // =====================================================
        // CENTER POINT OVERLAY
        // =====================================================

        if (showCenterPoint && objectDetected)
        {
            CenterPointProcess(
                current,
                output,
                boundingBox
            );

            Advance(
                ref current,
                ref output
            );
        }


        // =====================================================
        // DISPLAY
        // =====================================================

        displayTexture.SetPixels32(current);
        displayTexture.Apply();
    }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void InitializeProcessing()
    {
        width = webcamTexture.width;
        height = webcamTexture.height;

        int pixelCount =
            width * height;


        webCamPixels =
            new Color32[pixelCount];

        bufferA =
            new Color32[pixelCount];

        bufferB =
            new Color32[pixelCount];


        displayTexture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false
            );


        displayMaterial.mainTexture =
            displayTexture;


        initialized = true;
    }


    // =========================================================
    // BUFFER MANAGEMENT
    // =========================================================

    private void Advance(
        ref Color32[] current,
        ref Color32[] output)
    {
        current = output;

        output =
            ReferenceEquals(current, bufferA)
            ? bufferB
            : bufferA;
    }


    // =========================================================
    // GREYSCALE
    // =========================================================

    private void GreyScaleProcess(
        Color32[] input,
        Color32[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color32 pixel = input[i];

            byte grey = (byte)(
                pixel.r * 0.299f +
                pixel.g * 0.587f +
                pixel.b * 0.114f
            );

            output[i] =
                new Color32(
                    grey,
                    grey,
                    grey,
                    255
                );
        }
    }


    // =========================================================
    // THRESHOLD
    // =========================================================

    private void ThresholdProcess(
        Color32[] input,
        Color32[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color32 pixel = input[i];

            // Le threshold fonctionne même
            // si GreyScale est désactivé
            byte grey = (byte)(
                pixel.r * 0.299f +
                pixel.g * 0.587f +
                pixel.b * 0.114f
            );

            byte value =
                grey > threshold
                ? (byte)255
                : (byte)0;

            output[i] =
                new Color32(
                    value,
                    value,
                    value,
                    255
                );
        }
    }


    // =========================================================
    // RGB FILTER
    // =========================================================

    private void ColorFilterRGBProcess(
        Color32[] input,
        Color32[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color32 pixel = input[i];

            bool accepted =
                pixel.r >= minColor.x &&
                pixel.r <= maxColor.x &&

                pixel.g >= minColor.y &&
                pixel.g <= maxColor.y &&

                pixel.b >= minColor.z &&
                pixel.b <= maxColor.z;


            output[i] =
                accepted
                ? pixel
                : new Color32(
                    0,
                    0,
                    0,
                    255
                );
        }
    }


    // =========================================================
    // HSV FILTER
    // =========================================================

    private void ColorFilterHSVProcess(
        Color32[] input,
        Color32[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color color = input[i];

            Color.RGBToHSV(
                color,
                out float h,
                out float s,
                out float v
            );


            bool hueAccepted;


            // Plage normale
            if (minHue <= maxHue)
            {
                hueAccepted =
                    h >= minHue &&
                    h <= maxHue;
            }

            // Plage traversant 0
            // Exemple : rouge 0.95 → 0.05
            else
            {
                hueAccepted =
                    h >= minHue ||
                    h <= maxHue;
            }


            bool accepted =
                hueAccepted &&
                s >= minSaturation &&
                v >= minValue;


            output[i] =
                accepted
                ? input[i]
                : new Color32(
                    0,
                    0,
                    0,
                    255
                );
        }
    }


    // =========================================================
    // FIND BOUNDING BOX
    // =========================================================

    private RectInt FindBoundingBox(
        Color32[] input)
    {
        int minX = width;
        int minY = height;

        int maxX = -1;
        int maxY = -1;


        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index =
                    y * width + x;

                Color32 pixel =
                    input[index];


                // Noir = non détecté
                if (pixel.r == 0 &&
                    pixel.g == 0 &&
                    pixel.b == 0)
                    continue;


                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);

                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }


        if (maxX < 0)
            return new RectInt();


        return new RectInt(
            minX,
            minY,
            maxX - minX + 1,
            maxY - minY + 1
        );
    }


    // =========================================================
    // DRAW BOUNDING BOX
    // =========================================================

    private void BoundingBoxProcess(
        Color32[] input,
        Color32[] output,
        RectInt box)
    {
        Array.Copy(
            input,
            output,
            input.Length
        );


        for (int thickness = 0;
             thickness < boxSize;
             thickness++)
        {
            int minX =
                box.xMin + thickness;

            int maxX =
                box.xMax - 1 - thickness;

            int minY =
                box.yMin + thickness;

            int maxY =
                box.yMax - 1 - thickness;


            if (minX > maxX ||
                minY > maxY)
                break;


            // Haut / bas
            for (int x = minX;
                 x <= maxX;
                 x++)
            {
                output[
                    minY * width + x
                ] = boxColor;

                output[
                    maxY * width + x
                ] = boxColor;
            }


            // Gauche / droite
            for (int y = minY;
                 y <= maxY;
                 y++)
            {
                output[
                    y * width + minX
                ] = boxColor;

                output[
                    y * width + maxX
                ] = boxColor;
            }
        }
    }


    // =========================================================
    // CENTER POINT
    // =========================================================

    private void CenterPointProcess(
        Color32[] input,
        Color32[] output,
        RectInt boundingBox)
    {
        Array.Copy(
            input,
            output,
            input.Length
        );


        int centerX =
            boundingBox.x +
            boundingBox.width / 2;

        int centerY =
            boundingBox.y +
            boundingBox.height / 2;


        int radius =
            centerPointSize;


        for (int y = -radius;
             y <= radius;
             y++)
        {
            for (int x = -radius;
                 x <= radius;
                 x++)
            {
                // Cercle
                if (x * x + y * y >
                    radius * radius)
                    continue;


                int pixelX =
                    centerX + x;

                int pixelY =
                    centerY + y;


                if (pixelX < 0 ||
                    pixelX >= width ||
                    pixelY < 0 ||
                    pixelY >= height)
                    continue;


                output[
                    pixelY * width +
                    pixelX
                ] = pointColor;
            }
        }
    }


    // =========================================================
    // DISPLAY
    // =========================================================

    private Material CreateDisplay()
    {
        Camera cam =
            Camera.main;

        if (cam == null)
        {
            Debug.LogError(
                "Aucune Main Camera trouvée."
            );

            return null;
        }


        const float distance = 5f;


        float screenHeight;

        if (cam.orthographic)
        {
            screenHeight =
                cam.orthographicSize * 2f;
        }
        else
        {
            screenHeight =
                2f *
                distance *
                Mathf.Tan(
                    cam.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad
                );
        }


        float screenWidth =
            screenHeight * cam.aspect;


        GameObject quad =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );

        quad.name =
            "WebCam Display";


        quad.transform.position =
            cam.transform.position +
            cam.transform.forward *
            distance;

        quad.transform.rotation =
            cam.transform.rotation;

        quad.transform.localScale =
            new Vector3(
                screenWidth,
                screenHeight,
                1f
            );


        Material material =
            CreateDisplayMaterial();

        quad.GetComponent<Renderer>()
            .material = material;


        Destroy(
            quad.GetComponent<Collider>()
        );


        return material;
    }


    private Material CreateDisplayMaterial()
    {
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
                "Aucun shader Unlit trouvé."
            );

            return null;
        }


        return new Material(shader);
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (webcamTexture != null &&
            webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}