using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simulates the result normally produced by the glasses/head detector.
///
/// This component is intended for development on a computer
/// without a webcam.
///
/// Controls:
/// - Arrow keys : move the simulated head
/// - W / S      : move closer / farther
/// - Q / E      : rotate the glasses
/// - Space      : reset
/// </summary>
public sealed class HeadTrackingSimulator : MonoBehaviour
{
    // =========================================================
    // SIMULATED CAMERA
    // =========================================================

    [Header("Simulated Camera")]
    [SerializeField] private int frameWidth = 640;
    [SerializeField] private int frameHeight = 480;


    // =========================================================
    // SIMULATED HEAD
    // =========================================================

    [Header("Head Position")]

    [SerializeField] private Vector2 headCenter =
        new Vector2(320f, 240f);

    [Tooltip("Distance between both detected glasses centers, in pixels.")]
    [SerializeField] private float eyeDistance = 120f;

    [Tooltip("Rotation of the glasses in degrees.")]
    [SerializeField] private float headAngle = 0f;

    [SerializeField] private bool redEyeOnLeft = true;


    // =========================================================
    // SIMULATED GLASSES
    // =========================================================

    [Header("Glasses")]

    [SerializeField] private Vector2Int eyeBoxSize =
        new Vector2Int(70, 45);


    // =========================================================
    // CONTROLS
    // =========================================================

    [Header("Controls")]

    [SerializeField] private float movementSpeed = 150f;

    [Tooltip("Controls how fast simulated depth changes.")]
    [SerializeField] private float eyeDistanceSpeed = 60f;

    [SerializeField] private float rotationSpeed = 50f;


    // =========================================================
    // DEPTH LIMITS
    // =========================================================

    [Header("Limits")]

    [SerializeField] private float minEyeDistance = 40f;
    [SerializeField] private float maxEyeDistance = 250f;


    // =========================================================
    // PUBLIC DATA
    // =========================================================

    public int FrameWidth => frameWidth;
    public int FrameHeight => frameHeight;


    // =========================================================
    // UNITY
    // =========================================================

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;


        float deltaTime = Time.deltaTime;


        // ---------------------------------------------------------
        // X / Y movement
        // ---------------------------------------------------------

        Vector2 movement = Vector2.zero;

        if (keyboard.leftArrowKey.isPressed)
            movement.x -= 1f;

        if (keyboard.rightArrowKey.isPressed)
            movement.x += 1f;

        if (keyboard.downArrowKey.isPressed)
            movement.y -= 1f;

        if (keyboard.upArrowKey.isPressed)
            movement.y += 1f;


        headCenter +=
            movement *
            movementSpeed *
            deltaTime;


        // ---------------------------------------------------------
        // Simulated depth
        //
        // Closer viewer = larger eye distance in the image.
        // ---------------------------------------------------------

        if (keyboard.wKey.isPressed)
        {
            eyeDistance +=
                eyeDistanceSpeed *
                deltaTime;
        }

        if (keyboard.sKey.isPressed)
        {
            eyeDistance -=
                eyeDistanceSpeed *
                deltaTime;
        }


        // ---------------------------------------------------------
        // Head roll
        // ---------------------------------------------------------

        if (keyboard.qKey.isPressed)
        {
            headAngle +=
                rotationSpeed *
                deltaTime;
        }

        if (keyboard.eKey.isPressed)
        {
            headAngle -=
                rotationSpeed *
                deltaTime;
        }


        // ---------------------------------------------------------
        // Reset
        // ---------------------------------------------------------

        if (keyboard.spaceKey.wasPressedThisFrame)
            ResetSimulation();


        ClampValues();
    }


    // =========================================================
    // DETECTION RESULT
    // =========================================================

    /// <summary>
    /// Builds a DetectionResult exactly as if the detector
    /// had found the red and blue parts of the glasses.
    /// </summary>
    public DetectionResult GetDetectionResult()
    {
        float angle =
            headAngle *
            Mathf.Deg2Rad;


        // Direction between both eyes.
        Vector2 eyeAxis =
            new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            );


        Vector2 halfEyeVector =
            eyeAxis *
            eyeDistance *
            0.5f;


        Vector2 leftEye =
            headCenter -
            halfEyeVector;

        Vector2 rightEye =
            headCenter +
            halfEyeVector;


        Vector2 redEye =
            redEyeOnLeft
                ? leftEye
                : rightEye;


        Vector2 blueEye =
            redEyeOnLeft
                ? rightEye
                : leftEye;


        DetectionResult result =
            new DetectionResult();


        result.detected = true;

        result.redEye = redEye;
        result.blueEye = blueEye;

        result.headCenter =
            headCenter;

        result.eyeDistance =
            eyeDistance;

        result.headAngle =
            headAngle;


        result.redBox =
            CreateEyeBox(
                redEye
            );

        result.blueBox =
            CreateEyeBox(
                blueEye
            );


        return result;
    }


    // =========================================================
    // BOUNDING BOX
    // =========================================================

    private RectInt CreateEyeBox(
        Vector2 center)
    {
        int width =
            eyeBoxSize.x;

        int height =
            eyeBoxSize.y;


        int x =
            Mathf.RoundToInt(
                center.x -
                width * 0.5f
            );

        int y =
            Mathf.RoundToInt(
                center.y -
                height * 0.5f
            );


        return new RectInt(
            x,
            y,
            width,
            height
        );
    }


    // =========================================================
    // LIMITS
    // =========================================================

    private void ClampValues()
    {
        headCenter.x =
            Mathf.Clamp(
                headCenter.x,
                0f,
                frameWidth
            );

        headCenter.y =
            Mathf.Clamp(
                headCenter.y,
                0f,
                frameHeight
            );


        eyeDistance =
            Mathf.Clamp(
                eyeDistance,
                minEyeDistance,
                maxEyeDistance
            );
    }


    // =========================================================
    // RESET
    // =========================================================

    private void ResetSimulation()
    {
        headCenter =
            new Vector2(
                frameWidth * 0.5f,
                frameHeight * 0.5f
            );

        eyeDistance = 120f;
        headAngle = 0f;
    }
}