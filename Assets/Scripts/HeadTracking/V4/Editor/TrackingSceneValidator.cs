using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only checks for loaded scenes. Never deletes components or rewrites scene data.</summary>
public static class TrackingSceneValidator
{
    [MenuItem("Tools/Anaglyph/Validate loaded scenes")]
    public static void Validate()
    {
        int issues = 0;
        GameObject[] objects = Object.FindObjectsOfType<GameObject>(true);
        foreach (GameObject go in objects)
        {
            if (!go.scene.IsValid()) continue;
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing > 0) { Debug.LogWarning("Missing scripts: " + missing, go); issues++; }
        }
        int listeners = 0;
        foreach (AudioListener listener in Object.FindObjectsOfType<AudioListener>(true))
            if (listener.isActiveAndEnabled) listeners++;
        if (listeners != 1) { Debug.LogWarning("Expected one active AudioListener; found " + listeners); issues++; }
        foreach (TrackingController controller in Object.FindObjectsOfType<TrackingController>(true))
        {
            var serialized = new SerializedObject(controller);
            bool sim = serialized.FindProperty("useTrackingSimulator").boolValue;
            string input = sim ? "trackingSimulator" : "frameSource";
            if (serialized.FindProperty(input).objectReferenceValue == null)
            { Debug.LogWarning("Tracking input is missing: " + input, controller); issues++; }
            if (serialized.FindProperty("stereoCameraRig").objectReferenceValue == null)
            { Debug.LogWarning("Stereo rig reference is missing.", controller); issues++; }
            PoseMappingSettings m = controller.MappingSettings;
            if (m != null && m.xyMode == XYMappingMode.Metric && (m.focalX <= 0f || m.focalY <= 0f))
            { Debug.LogWarning("Metric mode requires measured focalX and focalY.", controller); issues++; }
        }
        foreach (StereoCameraRig rig in Object.FindObjectsOfType<StereoCameraRig>(true))
        {
            if (!rig.ValidateReferences()) { issues++; continue; }
            RenderTexture left = rig.LeftCamera.targetTexture, right = rig.RightCamera.targetTexture;
            if (left == null || right == null || left == right) { issues++; continue; }
            if (left.width != right.width || left.height != right.height || left.antiAliasing != right.antiAliasing)
            { Debug.LogWarning("Stereo RenderTextures differ.", rig); issues++; }
            if (Mathf.Abs((float)left.width / left.height - rig.ScreenWidth / rig.ScreenHeight) > 0.01f)
            { Debug.LogWarning("Physical screen and RenderTexture aspect ratios differ.", rig); issues++; }
            if (rig.LeftCamera.nearClipPlane != rig.RightCamera.nearClipPlane ||
                rig.LeftCamera.farClipPlane != rig.RightCamera.farClipPlane ||
                rig.LeftCamera.cullingMask != rig.RightCamera.cullingMask)
            { Debug.LogWarning("Stereo camera clip planes or culling masks differ.", rig); issues++; }
        }
        foreach (AnaglyphOutput output in Object.FindObjectsOfType<AnaglyphOutput>(true))
        {
            RawImage image = output.GetComponent<RawImage>();
            if (image == null) continue;
            RectTransform rect = image.rectTransform;
            if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one ||
                rect.offsetMin != Vector2.zero || rect.offsetMax != Vector2.zero)
            { Debug.LogWarning("Anaglyph RawImage does not fill its parent exactly.", image); issues++; }
        }
        Debug.Log("Anaglyph scene validation completed: " + issues + " item(s) to review.");
    }
}
