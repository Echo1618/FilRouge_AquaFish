using UnityEngine;
using UnityEngine.UI;

/// <summary>Binds the stereo textures to a private composite material without modifying the asset.</summary>
[RequireComponent(typeof(RawImage))]
public sealed class AnaglyphOutput : MonoBehaviour
{
    [SerializeField] private StereoCameraRig stereoRig;
    [SerializeField] private Material compositeMaterial;
    [SerializeField] private bool fullscreen = true;
    private RawImage image;
    private Material instance, previousMaterial;
    private Texture previousTexture;
    private RenderTexture boundLeft, boundRight;
    private static readonly int RightTextureId = Shader.PropertyToID("_RightTex");

    private void OnEnable()
    {
        image = GetComponent<RawImage>();
        previousMaterial = image.material;
        previousTexture = image.texture;
        Material source = compositeMaterial != null ? compositeMaterial : image.material;
        if (stereoRig == null || source == null || !source.HasProperty("_MainTex") || !source.HasProperty(RightTextureId))
        {
            Debug.LogError("AnaglyphOutput: assign StereoRig and the Custom/Anaglyph material.", this);
            enabled = false;
            return;
        }
        instance = new Material(source) { name = "Anaglyph runtime material" };
        image.material = instance;
        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        if (fullscreen)
        {
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        BindTextures();
    }

    private void LateUpdate() => BindTextures();
    private void BindTextures()
    {
        if (instance == null || stereoRig == null || stereoRig.LeftCamera == null || stereoRig.RightCamera == null) return;
        RenderTexture left = stereoRig.LeftCamera.targetTexture, right = stereoRig.RightCamera.targetTexture;
        if (left == boundLeft && right == boundRight) return;
        boundLeft = left;
        boundRight = right;
        image.texture = left;
        instance.SetTexture("_MainTex", left);
        instance.SetTexture(RightTextureId, right);
        if (left == null || right == null || left == right)
            Debug.LogWarning("AnaglyphOutput: two different RenderTextures are required.", this);
        else if (left.width != right.width || left.height != right.height || left.antiAliasing != right.antiAliasing)
            Debug.LogWarning("AnaglyphOutput: eye textures should have matching dimensions and MSAA.", this);
    }

    private void OnDisable()
    {
        if (image != null && instance != null)
        {
            if (image.material == instance) image.material = previousMaterial;
            if (image.texture == boundLeft) image.texture = previousTexture;
        }
        if (instance != null) Destroy(instance);
        instance = null;
        boundLeft = boundRight = null;
    }
}
