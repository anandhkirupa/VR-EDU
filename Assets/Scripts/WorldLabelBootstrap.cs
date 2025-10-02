using UnityEngine;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

// Place this on any GameObject in the main scene.
// Ensure a layer named "Labels" exists, and put all marker objects on that layer.
public class WorldLabelBootstrap : MonoBehaviour
{
    [Header("Layer setup")]
    [Tooltip("Name of the layer used by all label objects.")]
    public string labelsLayerName = "Labels";

    [Header("Overlay camera clip planes")]
    [Tooltip("Near clip for overlay; keep small so labels close to camera render.")]
    public float overlayNear = 0.01f;
    [Tooltip("Far clip for overlay; large so distant labels remain visible.")]
    public float overlayFar  = 1_000_000_000f; // 1e9

    [Header("Verification")]
    [Tooltip("Tint overlay to visually confirm the overlay pass draws.")]
    public bool debugOverlayTint = false;
    public Color debugOverlayColor = new Color(0f, 1f, 0f, 0.1f);

    [Header("References (auto if empty)")]
    public Camera mainCam;
    public Camera labelsCam;

    int labelsLayer = -1;
    bool attemptedSetup = false;

    void Awake()
    {
        labelsLayer = LayerMask.NameToLayer(labelsLayerName);
        if (labelsLayer < 0)
        {
            Debug.LogWarning($"Layer '{labelsLayerName}' not found; create it in Project Settings > Tags and Layers and assign markers to it.");
        }
    }

    void Start()
    {
        TryBindMainCamera();
        if (mainCam) EnsureLabelsOverlay();
    }

    void LateUpdate()
    {
        // Handle the case where the camera is spawned later at runtime.
        if (!mainCam)
        {
            TryBindMainCamera();
            if (mainCam) EnsureLabelsOverlay();
        }

        // If something else resets camera types/stack later in frame, re-assert once.
        if (mainCam && labelsCam && !attemptedSetup)
        {
            attemptedSetup = true;
            EnsureLabelsOverlay();
        }
    }

    void TryBindMainCamera()
    {
        if (!mainCam) mainCam = Camera.main;
        if (!mainCam)
        {
            // Fallback: find any camera if MainCamera tag not set yet.
            mainCam = FindFirstObjectByType<Camera>();
        }
        if (!mainCam)
        {
            Debug.Log("WorldLabelBootstrap: Waiting for main camera...");
        }
    }

    void EnsureLabelsOverlay()
    {
        // Create and configure overlay camera if missing.
        if (!labelsCam)
        {
            var go = new GameObject("LabelsCamera");
            labelsCam = go.AddComponent<Camera>();

            // Follow the base camera.
            labelsCam.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
            labelsCam.transform.SetParent(mainCam.transform, worldPositionStays: true);

            // Match optics so frustums align.
            labelsCam.orthographic = mainCam.orthographic;
            labelsCam.fieldOfView = mainCam.fieldOfView;
            labelsCam.orthographicSize = mainCam.orthographicSize;

            // Very wide clip to avoid distance culling of labels.
            labelsCam.nearClipPlane = overlayNear;
            labelsCam.farClipPlane  = overlayFar;

            // Only render the Labels layer.
            int mask = labelsLayer >= 0 ? (1 << labelsLayer) : 0;
            labelsCam.cullingMask = mask;

            // Clear behavior: for debug we tint to prove overlay draws; otherwise composite on top.
            if (debugOverlayTint)
            {
                labelsCam.clearFlags = CameraClearFlags.SolidColor;
                labelsCam.backgroundColor = debugOverlayColor;
            }
            else
            {
                labelsCam.clearFlags = CameraClearFlags.Nothing;
            }
        }

        // Ensure Base camera does NOT render the Labels layer to avoid duplicate rendering.
        if (labelsLayer >= 0)
        {
            mainCam.cullingMask &= ~(1 << labelsLayer);
        }

        // URP camera stacking or Built-in fallback.
        #if UNITY_RENDER_PIPELINE_UNIVERSAL
        // Get or add the Universal Additional Camera Data components per URP docs.
        var baseData = mainCam.GetComponent<UniversalAdditionalCameraData>();
        if (!baseData) baseData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();

        var overData = labelsCam.GetComponent<UniversalAdditionalCameraData>();
        if (!overData) overData = labelsCam.gameObject.AddComponent<UniversalAdditionalCameraData>();

        // Set camera types.
        baseData.renderType = CameraRenderType.Base;
        overData.renderType = CameraRenderType.Overlay;

        // Add overlay to the base stack.
        var stack = baseData.cameraStack;
        if (!stack.Contains(labelsCam))
        {
            stack.Add(labelsCam);
        }

        // Verification log.
        Debug.Log($"URP stack: base={baseData.renderType}, overlay={overData.renderType}, inStack={stack.Contains(labelsCam)}");
        #else
        // Built-in pipeline: render overlay after base by depth ordering.
        labelsCam.depth = mainCam.depth + 1f;
        Debug.Log("Built-in RP fallback active (URP not detected).");
        #endif
    }
}
