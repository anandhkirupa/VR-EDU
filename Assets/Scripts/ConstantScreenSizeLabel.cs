using UnityEngine;

public class ConstantScreenSizeLabel : MonoBehaviour
{
    public Camera mainCamera;
    [Tooltip("Distance -> Scale. X: distance, Y: scale")]
    private AnimationCurve distanceToScale = new AnimationCurve(
        new Keyframe(0f, 3f),
        new Keyframe(25f, 3f),
        new Keyframe(75f, 6f),
        new Keyframe(440f, 30f),
        new Keyframe(3000f, 200f),
        new Keyframe(10000f, 300f)
    );

    private float minScale = 1f;
    private float maxScale = 300f;
    private bool faceCamera = true;

    void SetWorldScaleUniform(float worldScale)
    {
        var parent = transform.parent;
        if (parent == null)
        {
            transform.localScale = Vector3.one * worldScale;
            return;
        }

        Vector3 p = parent.lossyScale; // parent world scale
        // Avoid divide-by-zero; treat very small as 1
        float lx = Mathf.Approximately(p.x, 0f) ? worldScale : worldScale / p.x;
        float ly = Mathf.Approximately(p.y, 0f) ? worldScale : worldScale / p.y;
        float lz = Mathf.Approximately(p.z, 0f) ? worldScale : worldScale / p.z;
        transform.localScale = new Vector3(lx, ly, lz);
    }
    void LateUpdate()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!mainCamera) return;

        float d = Vector3.Distance(mainCamera.transform.position, transform.position);

        float s = distanceToScale.Evaluate(d);
        s = Mathf.Clamp(s, minScale, maxScale);
        SetWorldScaleUniform(s);

        if (faceCamera)
        {
            // 1) Compute desired world rotation that faces the camera (yaw-only for upright labels)
            Vector3 toCam = mainCamera.transform.position - transform.position; // world-space direction [web:99]
            bool flip180Y = true;
           
                // Full billboard (pitch + yaw), still cancels parent
                if (toCam.sqrMagnitude < 1e-6f) toCam = mainCamera.transform.forward; // stable fallback [web:99]
                Quaternion desiredWorld = Quaternion.LookRotation(toCam.normalized, Vector3.up); // full-facing rotation [web:99]

                if (flip180Y) desiredWorld = desiredWorld * Quaternion.Euler(0f, 180f, 0f); // optional flip [web:99]

                if (transform.parent != null)
                    transform.localRotation = Quaternion.Inverse(transform.parent.rotation) * desiredWorld; // parent cancel [web:99]
                else
                    transform.rotation = desiredWorld; // direct set when unparented [web:99]
            
        }

    }
}