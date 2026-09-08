using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;



public class HeadDetectorV2 : MonoBehaviour
{
    private readonly int[][] GAUSSIAN_KERNEL = new int[][] {    new int[] { 1, 2, 1 },
                                                                new int[] { 2, 4, 2 },
                                                                new int[] { 1, 2, 1 } };
    private int width, height;
    private bool initialized;

    [Serializable]
    private struct HSVRange
    {
        [Range(0f, 1f)] public float minH;
        [Range(0f, 1f)] public float maxH;
        [Range(0f, 1f)] public float minS;
        [Range(0f, 1f)] public float minV;
    }
    [Header("HSV - frame paint, values between 0 and 1")]
    [SerializeField]
    private HSVRange redHSV = new HSVRange
    {
        minH = 0.95f,
        maxH = 0.05f,
        minS = 0.40f,
        minV = 0.15f
    };
    [SerializeField]
    private HSVRange blueHSV = new HSVRange
    {
        minH = 0.52f,
        maxH = 0.72f,
        minS = 0.35f,
        minV = 0.15f
    };

    [Header("Mask Processing")]
    [Tooltip("Averages RGB before HSV. Leave off initially: thin paint can lose saturation.")]
    [SerializeField] private bool useBlur = false;
    [Tooltip("Erosion then dilation. Can erase a thin glasses frame.")]
    [SerializeField] private bool useOpening = false;
    [Tooltip("Dilation then erosion. Can reconnect small gaps in the frame mask.")]
    [SerializeField] private bool useClosing = true;

    private enum DebugMaskStage { RawHSV, Processed }
    [Tooltip("RawHSV shows the masks before morphology; detection still uses the processed masks. White means both filters accepted the same pixel.")]
    [SerializeField] private DebugMaskStage debugMaskStage = DebugMaskStage.Processed;

    [Serializable]
    private struct DebugStatistics
    {
        public int rawRedPixels, rawBluePixels;
        public int processedRedPixels, processedBluePixels;
        public int redBlobCount, blueBlobCount;
        public bool pairDetected;
    }
    [Header("Last Frame Diagnostics (updated at runtime)")]
    [SerializeField] private DebugStatistics diagnostics;

    [Header("Blob Detection")]
    [Min(1)][SerializeField] private int minBlobArea = 50;

    public struct Blob
    {
        public int area;
        public RectInt bounds;
        public Vector2 centroid;
        public Vector2 center;
    }

    private enum ColorOrder { Any, RedOnImageLeft, RedOnImageRight }
    [Header("Blob Pairing")]
    [Tooltip("Order in the input pixels, independent of any mirrored display. Any is useful for initial calibration.")]
    [SerializeField] private ColorOrder expectedColorOrder = ColorOrder.Any;
    [Min(0f)][SerializeField] private float maxVerticalDifference = 50f;
    [Min(1f)][SerializeField] private float minEyeDistance = 30f;
    [Min(1f)][SerializeField] private float maxEyeDistance = 400f;
    [Min(1f)][SerializeField] private float maxAreaRatio = 3f;

    private Blob bestRedBlob;
    private Blob bestBlueBlob;
    private bool pairDetected;

    public struct DetectionResult
    {
        public bool detected;

        public RectInt redBox;
        public RectInt blueBox;

        // Estimates from the colored frame boxes, not measured pupil positions.
        public Vector2 redEye;
        public Vector2 blueEye;

        public Vector2 headCenter;
        public float eyeDistance;
        // Image-plane roll in degrees, measured from image left to right.
        public float headAngle;
    }
    private Color32[] blurBuffer;

    private byte[] redMask;
    private byte[] blueMask;

    private byte[] tempMaskA;
    private byte[] tempMaskB;

    private byte[] grayScale;

    // =========================================================
    // INITIALIZATION
    // =========================================================
    public void Initialize(int imageWidth, int imageHeight)
    {
        initialized = false;
        pairDetected = false;
        diagnostics = new DebugStatistics();

        if (imageWidth <= 16 || imageHeight <= 16)
        {
            Debug.LogError("HeadDetector : invalid image size.");
            return;
        }

        width = imageWidth;
        height = imageHeight;

        int length = width * height;

        blurBuffer = new Color32[length];

        redMask = new byte[length];
        blueMask = new byte[length];

        tempMaskA = new byte[length];
        tempMaskB = new byte[length];

        grayScale = new byte[length];

        initialized = true;
    }

    // =========================================================
    // Filtering Process
    // =========================================================

    public DetectionResult FilteringProcess(Color32[] input, Color32[] output)
    {
        DetectionResult result = new DetectionResult();
        pairDetected = false;
        diagnostics = new DebugStatistics();

        if (!initialized || input == null || output == null)
            return result;

        if (input.Length != width * height || output.Length != width * height)
            return result;

        // 1 - Optional RGB blur. Avoid mixing thin paint with the skin/background.
        Color32[] source = input;
        if (useBlur)
        {
            BoxBlur(input, blurBuffer);
            source = blurBuffer;
        }

        // 2 - Independent HSV masks. Hue wrapping already handles red near 0/1.
        diagnostics.rawRedPixels = ColorFilterHSV(source, redMask, redHSV);
        diagnostics.rawBluePixels = ColorFilterHSV(source, blueMask, blueHSV);

        // RawHSV is captured before any erosion/dilation.
        if (debugMaskStage == DebugMaskStage.RawHSV)
            computeRCFilters(redMask, blueMask, output);

        // 3 - Optional morphology. Every operation overwrites its destination.
        ProcessMask(redMask);
        ProcessMask(blueMask);
        diagnostics.processedRedPixels = CountPixels(redMask);
        diagnostics.processedBluePixels = CountPixels(blueMask);

        // 4 - Debug image (cyan is only the display color of the blue mask).
        if (debugMaskStage == DebugMaskStage.Processed)
            computeRCFilters(redMask, blueMask, output);


        // 5 - Blobs
        List<Blob> redBlobs = FindBlobs(redMask);
        List<Blob> blueBlobs = FindBlobs(blueMask);
        diagnostics.redBlobCount = redBlobs.Count;
        diagnostics.blueBlobCount = blueBlobs.Count;


        // 6 - Recherche de la meilleure paire
        BlobCompare(redBlobs, blueBlobs);
        diagnostics.pairDetected = pairDetected;


        // 7 - Aucune paire valide
        if (!pairDetected)
            return result;


        // 8 - Construction du résultat
        result.detected = true;

        result.redBox = bestRedBlob.bounds;
        result.blueBox = bestBlueBlob.bounds;

        result.redEye = bestRedBlob.center;
        result.blueEye = bestBlueBlob.center;

        result.headCenter =
            (result.redEye + result.blueEye) / 2f;

        result.eyeDistance =
            Vector2.Distance(
                result.redEye,
                result.blueEye
            );

        Vector2 direction =
            result.blueEye - result.redEye;

        // Keep a horizontal head near 0 degrees for either color order.
        if (direction.x < 0f)
            direction = -direction;

        result.headAngle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) * Mathf.Rad2Deg;

        return result;
    }

    private void computeRCFilters(byte[] p_red, byte[] p_blue, Color32[] p_output)
    {
        for (int i = 0; i < width * height; i++)
        {
            if (p_red[i] > 0 && p_blue[i] > 0) p_output[i] = new Color32(255, 255, 255, 255);
            else if (p_red[i] > 0) p_output[i] = new Color32(255, 0, 0, 255);
            else if (p_blue[i] > 0) p_output[i] = new Color32(0, 255, 255, 255);
            else p_output[i] = new Color32(0, 0, 0, 255);
        }
    }

    [ContextMenu("Apply starting HSV ranges (red and blue paint)")]
    private void ApplyStartingHSVRanges()
    {
        redHSV = new HSVRange
        {
            minH = 0.95f,
            maxH = 0.05f,
            minS = 0.40f,
            minV = 0.15f
        };
        blueHSV = new HSVRange
        {
            minH = 0.52f,
            maxH = 0.72f,
            minS = 0.35f,
            minV = 0.15f
        };
    }

    private void ProcessMask(byte[] mask)
    {
        if (useOpening)
        {
            Opening(mask, tempMaskA, tempMaskB);
            Array.Copy(tempMaskB, mask, mask.Length);
        }

        if (useClosing)
        {
            Closing(mask, tempMaskA, tempMaskB);
            Array.Copy(tempMaskB, mask, mask.Length);
        }
    }

    private int CountPixels(byte[] mask)
    {
        int count = 0;
        for (int i = 0; i < mask.Length; i++)
            if (mask[i] != 0) count++;
        return count;
    }

    private void Opening(byte[] input, byte[] temp, byte[] output)
    {
        Erosion(input, temp);
        Dilating(temp, output);
    }

    private void Closing(byte[] input, byte[] temp, byte[] output)
    {
        Dilating(input, temp);
        Erosion(temp, output);
    }

    // =========================================================
    // BOX BLUR
    // =========================================================
    private void BoxBlur(Color32[] input, Color32[] output)
    {
        if (!initialized || input == null || output == null) return;
        if (input.Length != output.Length) return;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int r = 0, g = 0, b = 0, count = 0;

                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int neighborX = x + offsetX;
                        int neighborY = y + offsetY;

                        if (neighborX < 0 || neighborX >= width ||
                            neighborY < 0 || neighborY >= height) continue;

                        Color32 pixel = input[neighborY * width + neighborX];

                        r += pixel.r;
                        g += pixel.g;
                        b += pixel.b;

                        count++;
                    }
                }

                output[y * width + x] = new Color32(
                    (byte)(r / count),
                    (byte)(g / count),
                    (byte)(b / count),
                    255
                );
            }
        }
    }

    // =========================================================
    // HSV COLOR FILTER
    // =========================================================
    private int ColorFilterHSV(Color32[] input, byte[] output, HSVRange p_filter)
    {
        int count = 0;
        for (int i = 0; i < input.Length; i++)
        {
            Color.RGBToHSV(input[i], out float h, out float s, out float v);

            bool hueAccepted = p_filter.minH <= p_filter.maxH
                ? h >= p_filter.minH && h <= p_filter.maxH
                : h >= p_filter.minH || h <= p_filter.maxH;

            bool accepted = hueAccepted && s >= p_filter.minS && v >= p_filter.minV;

            output[i] = accepted ? (byte)255 : (byte)0;
            if (accepted) count++;
        }
        return count;
    }

    // =========================================================
    // ERODING
    // =========================================================    

    private void Erosion(byte[] p_input, byte[] p_output)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int count = 0;

                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int neighborX = x + offsetX;
                        int neighborY = y + offsetY;

                        if (neighborX < 0 || neighborX >= width ||
                            neighborY < 0 || neighborY >= height) continue;

                        byte pixel = p_input[neighborY * width + neighborX];
                        if (pixel == 255) count++;
                    }
                }

                p_output[y * width + x] = (count == 9) ? (byte)255 : (byte)0;
            }
        }
    }

    // =========================================================
    // DILATING
    // ========================================================= 
    private void Dilating(byte[] p_input, byte[] p_output)
    {
        // Input and output must be separate arrays (as in Opening/Closing).
        // Otherwise old red/blue pixels and previous frames remain in the mask.
        Array.Clear(p_output, 0, p_output.Length);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (p_input[y * width + x] == 255)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            int neighborX = x + offsetX;
                            int neighborY = y + offsetY;

                            if (neighborX < 0 || neighborX >= width ||
                                neighborY < 0 || neighborY >= height) continue;

                            p_output[neighborY * width + neighborX] = 255;
                        }
                    }
                }

            }
        }
    }

    // =========================================================
    // GRADIEN
    // =========================================================

    private void GrayScale(Color32[] p_input, byte[] p_output)
    {
        for (int i = 0; i < p_input.Length; i++)
        {
            p_output[i] = (byte)(
                            0.299f * p_input[i].r +
                            0.587f * p_input[i].g +
                            0.114f * p_input[i].b
                        );
        }
    }

    private void GradientSobel(byte[] p_input, byte[] p_output)
    {
        Array.Clear(p_output, 0, p_output.Length);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // Read the 3x3 neighborhood.
                int topLeft = p_input[(y - 1) * width + (x - 1)];
                int top = p_input[(y - 1) * width + x];
                int topRight = p_input[(y - 1) * width + (x + 1)];

                int left = p_input[y * width + (x - 1)];
                int right = p_input[y * width + (x + 1)];

                int bottomLeft = p_input[(y + 1) * width + (x - 1)];
                int bottom = p_input[(y + 1) * width + x];
                int bottomRight = p_input[(y + 1) * width + (x + 1)];


                // Sobel horizontal gradient.
                //
                // -1  0  1
                // -2  0  2
                // -1  0  1
                int gx =
                    -topLeft + topRight
                    - 2 * left + 2 * right
                    - bottomLeft + bottomRight;


                // Sobel vertical gradient.
                //
                // -1 -2 -1
                //  0  0  0
                //  1  2  1
                int gy =
                    -topLeft - 2 * top - topRight
                    + bottomLeft + 2 * bottom + bottomRight;


                // Gradient magnitude.
                float magnitude = Mathf.Sqrt(
                    gx * gx + gy * gy
                );


                // Convert the result back to a grayscale byte.
                p_output[y * width + x] = (byte)Mathf.Clamp(
                    magnitude,
                    0f,
                    255f
                );
            }
        }
    }

    //Debug
    public void GradientFilter(Color32[] p_input, Color32[] p_output)
    {
        byte[] gray = new byte[p_input.Length];
        GrayScale(p_input, gray);

        byte[] grad = new byte[p_input.Length];
        GradientSobel(gray, grad);

        ByteToColor32(grad, p_output);
    }

    // =========================================================
    // BLOB
    // =========================================================

    private List<Blob> FindBlobs(byte[] mask)
    {
        List<Blob> blobs = new List<Blob>();
        bool[] visited = new bool[mask.Length];
        Queue<int> queue = new Queue<int>();

        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] == 0 || visited[i]) continue;

            int area = 0;
            int minX = width, minY = height;
            int maxX = -1, maxY = -1;

            long sumX = 0;
            long sumY = 0;

            visited[i] = true;
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                int x = current % width;
                int y = current / width;

                area++;

                sumX += x;
                sumY += y;

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);

                // Parcours des 8 voisins
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0) continue;

                        int neighborX = x + offsetX;
                        int neighborY = y + offsetY;

                        if (neighborX < 0 || neighborX >= width ||
                            neighborY < 0 || neighborY >= height) continue;

                        int neighborIndex = neighborY * width + neighborX;

                        if (mask[neighborIndex] == 0 || visited[neighborIndex])
                            continue;

                        visited[neighborIndex] = true;
                        queue.Enqueue(neighborIndex);
                    }
                }
            }

            // Élimination des petits parasites
            if (area < Mathf.Max(1, minBlobArea)) continue;

            RectInt bounds = new RectInt(
                minX,
                minY,
                maxX - minX + 1,
                maxY - minY + 1
            );

            Blob blob = new Blob
            {
                area = area,

                bounds = bounds,

                // Centre de masse réel des pixels détectés
                centroid = new Vector2(
                    (float)sumX / area,
                    (float)sumY / area
                ),

                // Centre géométrique de la BoundingBox
                center = new Vector2(
                    bounds.x + bounds.width / 2f,
                    bounds.y + bounds.height / 2f
                )
            };

            blobs.Add(blob);
        }

        return blobs;
    }

    private void BlobCompare(List<Blob> redBlobs, List<Blob> blueBlobs)
    {
        pairDetected = false;
        float bestScore = float.MaxValue;

        foreach (Blob red in redBlobs)
        {
            foreach (Blob blue in blueBlobs)
            {
                // Order applies to input pixel coordinates, not to the display.
                if (expectedColorOrder == ColorOrder.RedOnImageLeft &&
                    red.center.x >= blue.center.x) continue;
                if (expectedColorOrder == ColorOrder.RedOnImageRight &&
                    red.center.x <= blue.center.x) continue;

                float deltaY = Mathf.Abs(red.center.y - blue.center.y);
                float distance = Vector2.Distance(red.center, blue.center);

                float areaRatio =
                    (float)Mathf.Max(red.area, blue.area) /
                    Mathf.Min(red.area, blue.area);

                if (deltaY > maxVerticalDifference) continue;
                if (distance < Mathf.Max(1f, minEyeDistance) || distance > maxEyeDistance) continue;
                if (areaRatio > maxAreaRatio) continue;

                // Différence de taille normalisée : 0 = tailles identiques
                float sizeDifference =
                    Mathf.Abs(red.area - blue.area) /
                    (float)Mathf.Max(red.area, blue.area);

                // Différence verticale normalisée
                float verticalScore =
                    deltaY / Mathf.Max(0.001f, maxVerticalDifference);

                // Score taille normalisé
                float sizeScore = sizeDifference;

                // Score final : plus petit = meilleure paire
                float score =
                    verticalScore * 0.7f +
                    sizeScore * 0.3f;

                if (score >= bestScore) continue;

                bestScore = score;
                bestRedBlob = red;
                bestBlueBlob = blue;
                pairDetected = true;
            }
        }
    }

    private void ByteToColor32(byte[] p_input, Color32[] p_output)
    {
        for (int i = 0; i < p_input.Length; i++)
        {
            p_output[i] = new Color32 ( p_input[i],
                                        p_input[i],
                                        p_input[i],
                                        255);
        }
    }
}
