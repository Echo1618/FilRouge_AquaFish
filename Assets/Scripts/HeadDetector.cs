using System;
using UnityEngine;
using System.Collections.Generic;

public class HeadDetector : MonoBehaviour
{
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
    [SerializeField] private HSVRange redHSV, blueHSV;

    [Header("Blob Detection")]
    [SerializeField] private int minBlobArea = 50;

    public struct Blob
    {
        public int area;
        public RectInt bounds;
        public Vector2 centroid;
        public Vector2 center;
    }

    [Header("Blob Pairing")]
    [SerializeField] private float maxVerticalDifference = 50f;
    [SerializeField] private float minEyeDistance = 30f;
    [SerializeField] private float maxEyeDistance = 400f;
    [SerializeField] private float maxAreaRatio = 3f;

    private Blob bestRedBlob;
    private Blob bestBlueBlob;
    private bool pairDetected;

    public struct DetectionResult
    {
        public bool detected;

        public RectInt redBox;
        public RectInt blueBox;

        public Vector2 redEye;
        public Vector2 blueEye;

        public Vector2 headCenter;
        public float eyeDistance;
        public float headAngle;
    }
    private Color32[] gauss;

    private byte[] redMask;
    private byte[] blueMask;

    private byte[] tempMaskA;
    private byte[] tempMaskB;

    // =========================================================
    // INITIALIZATION
    // =========================================================
    public void Initialize(int imageWidth, int imageHeight)
    {
        if (imageWidth <= 16 || imageHeight <= 16)
        {
            Debug.LogError("HeadDetector : invalid image size.");
            return;
        }

        width = imageWidth;
        height = imageHeight;

        int length = width * height;

        gauss = new Color32[length];

        redMask = new byte[length];
        blueMask = new byte[length];

        tempMaskA = new byte[length];
        tempMaskB = new byte[length];

        initialized = true;
    }

    // =========================================================
    // Filtering Process
    // =========================================================

    public DetectionResult FilteringProcess(Color32[] input, Color32[] output)
    {
        DetectionResult result = new DetectionResult();

        if (!initialized || input == null || output == null)
            return result;

        // 1 - Blur
        GaussianBlur(input, gauss);

        // 2 - HSV rouge
        ColorFilterHSV(gauss, redMask, redHSV);

        // Opening
        Opening(redMask, tempMaskA, tempMaskB);

        // Closing
        Closing(tempMaskB, tempMaskA, redMask);


        // 3 - HSV bleu
        ColorFilterHSV(gauss, blueMask, blueHSV);

        // Opening
        Opening(blueMask, tempMaskA, tempMaskB);

        // Closing
        Closing(tempMaskB, tempMaskA, blueMask);


        // 4 - Image debug rouge / bleu
        computeRCFilters(redMask, blueMask, output);


        // 5 - Blobs
        List<Blob> redBlobs = FindBlobs(redMask);
        List<Blob> blueBlobs = FindBlobs(blueMask);


        // 6 - Recherche de la meilleure paire
        BlobCompare(redBlobs, blueBlobs);


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
            if (p_red[i] > 0) p_output[i] = new Color32(255, 0, 0, 255);
            else if (p_blue[i] > 0) p_output[i] = new Color32(0, 255, 255, 255);
            else p_output[i] = new Color32(0, 0, 0, 255);
        }
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
    // GAUSSIAN BLUR
    // =========================================================
    private void GaussianBlur(Color32[] input, Color32[] output)
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
    private void ColorFilterHSV(Color32[] input, byte[] output, HSVRange p_filter)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color.RGBToHSV(input[i], out float h, out float s, out float v);

            bool hueAccepted = p_filter.minH <= p_filter.maxH
                ? h >= p_filter.minH && h <= p_filter.maxH
                : h >= p_filter.minH || h <= p_filter.maxH;

            bool accepted = hueAccepted && s >= p_filter.minS && v >= p_filter.minV;

            output[i] = accepted ? (byte)255 : (byte)0;
        }
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
            if (area < minBlobArea) continue;

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
                // Ordre attendu sur l'image
                // Inverse cette condition si ta webcam est miroir
                if (red.center.x > blue.center.x) continue;

                float deltaY = Mathf.Abs(red.center.y - blue.center.y);
                float distance = Vector2.Distance(red.center, blue.center);

                float areaRatio =
                    (float)Mathf.Max(red.area, blue.area) /
                    Mathf.Min(red.area, blue.area);

                if (deltaY > maxVerticalDifference) continue;
                if (distance < minEyeDistance || distance > maxEyeDistance) continue;
                if (areaRatio > maxAreaRatio) continue;

                // Différence de taille normalisée : 0 = tailles identiques
                float sizeDifference =
                    Mathf.Abs(red.area - blue.area) /
                    (float)Mathf.Max(red.area, blue.area);

                // Différence verticale normalisée
                float verticalScore =
                    deltaY / maxVerticalDifference;

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
}