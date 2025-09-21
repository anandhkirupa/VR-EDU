using UnityEngine;

[RequireComponent(typeof(Transform))]
public class PlanetAutoOrbitAndSpin : MonoBehaviour
{
    [Header("Center / Focus")]
    public Transform center; // Sun

    [Header("Orbit Derivation")]
    [Range(0f, 0.9f)] public float eccentricity = 0.03f;  // small e for planets
    [Range(-89f, 89f)] public float inclinationDeg = 0f;  // set per planet if desired
    [Range(0f, 360f)] public float longitudeOfPeriapsis = 0f;

    [Header("Time Scaling")]
    // Multiply speeds so they’re noticeable in a demo while preserving ratios.
    // Example: 1 real “year” lasts demoYearSeconds, applied proportionally by P_real.
    public float demoYearSeconds = 120f;      // Earth completes 1 orbit in 120 s
    public float demoDaySeconds = 10f;        // Earth completes 1 rotation in 10 s

    [Header("Path Rendering (optional)")]
    public LineRenderer line;
    [Range(16, 1024)] public int segments = 256;
    public float lineWidth = 0.08f;

    // Derived
    float a;                   // semi-major axis (scene units)
    float b;                   // semi-minor axis (scene units)
    float orbitProgress;       // [0,1)
    Quaternion planeRotation;
    float orbitPeriodSeconds;  // per planet, scaled to demoYearSeconds
    float spinDegPerSec;       // per planet, scaled to demoDaySeconds

    void Awake()
    {
        if (!center) Debug.LogWarning($"{name}: center not set; using world origin.");

        // Orient orbit plane
        var tilt = Quaternion.AngleAxis(inclinationDeg, Vector3.right);
        var inPlane = Quaternion.AngleAxis(longitudeOfPeriapsis, Vector3.up);
        planeRotation = inPlane * tilt;

        // Use current placement as baseline distance
        Vector3 c = center ? center.position : Vector3.zero;
        Vector3 r0 = transform.position - c;
        Vector3 n = planeRotation * Vector3.up;            // orbit plane normal
        Vector3 r0Proj = Vector3.ProjectOnPlane(r0, n);
        float R = r0Proj.magnitude;
        if (R < 1e-3f) R = 10f;                            // fallback

        a = R;
        float e = Mathf.Clamp01(eccentricity);
        b = a * Mathf.Sqrt(1f - e * e);

        // Initial azimuth -> starting phase
        Quaternion invPlane = Quaternion.Inverse(planeRotation);
        Vector3 localPlane = invPlane * r0Proj;
        if (localPlane.sqrMagnitude < 1e-6f) localPlane = new Vector3(a, 0f, 0f);
        float ang = Mathf.Atan2(localPlane.z / Mathf.Max(1e-6f, b),
                                localPlane.x / Mathf.Max(1e-6f, a));
        if (ang < 0f) ang += Mathf.PI * 2f;
        orbitProgress = ang / (Mathf.PI * 2f);

        // Assign physical baselines by name, then scale to demo times
        AssignPhysicalPeriodsByName(out float yearDays, out float dayHours, out bool retrograde, out float axialTiltDeg);

        // Revolution: scale so Earth’s real 365 d becomes demoYearSeconds
        // P_demo = P_real_days / 365 * demoYearSeconds (Earth => demoYearSeconds)
        orbitPeriodSeconds = Mathf.Max(0.01f, (yearDays / 365.0f) * Mathf.Max(0.1f, demoYearSeconds));

        // Rotation: scale so Earth’s real 24 h becomes demoDaySeconds
        // deg/s base = 360 / daySeconds; scaled by 24h mapping to demoDaySeconds
        float earthBaseDegPerSec = 360f / (24f * 3600f);
        float thisBaseDegPerSec  = 360f / (Mathf.Max(0.1f, dayHours) * 3600f);
        float scale = (demoDaySeconds > 0f) ? (thisBaseDegPerSec / earthBaseDegPerSec) * (360f / demoDaySeconds) * (demoDaySeconds / 360f) : 1f;
        // Simpler: Earth spins 360/10 deg/s when demoDaySeconds=10 -> 36 deg/s
        // So general: spinDegPerSec = 360 / (dayHours * 3600) * (24h * 3600) / demoDaySeconds
        spinDegPerSec = (360f / (Mathf.Max(0.1f, dayHours) * 3600f)) * ((24f * 3600f) / Mathf.Max(0.1f, demoDaySeconds));
        if (retrograde) spinDegPerSec = -spinDegPerSec;

        // Apply axial tilt to local spin axis
        transform.localRotation = Quaternion.AngleAxis(axialTiltDeg, Vector3.forward) * transform.localRotation;

        // Bake path
        line = GetComponent<LineRenderer>();
        if (line)
        {
            line.loop = true;
            line.widthMultiplier = lineWidth;
            line.positionCount = Mathf.Max(3, segments);
            BakePath();
        }

        // Snap to path
        transform.position = ComputeWorldPosition(orbitProgress);
    }

    void Update()
    {
        // Spin
        if (spinDegPerSec != 0f)
            transform.Rotate(Vector3.up, spinDegPerSec * Time.deltaTime, Space.Self);

        // Revolution
        float dProg = (orbitPeriodSeconds > 0f) ? (Time.deltaTime / orbitPeriodSeconds) : 0f;
        orbitProgress = Mathf.Repeat(orbitProgress + dProg, 1f);
        transform.position = ComputeWorldPosition(orbitProgress);
    }

    Vector3 ComputeWorldPosition(float t)
    {
        float theta = t * Mathf.PI * 2f;
        float x = Mathf.Cos(theta) * a;
        float z = Mathf.Sin(theta) * b;
        Vector3 local = new Vector3(x, 0f, z);
        Vector3 worldOffset = planeRotation * local;
        Vector3 c = center ? center.position : Vector3.zero;
        return c + worldOffset;
    }

    public void BakePath()
    {
        if (!line) return;
        Vector3 c = center ? center.position : Vector3.zero;
        int n = Mathf.Max(3, segments);
        if (line.positionCount != n) line.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float theta = t * Mathf.PI * 2f;
            float x = Mathf.Cos(theta) * a;
            float z = Mathf.Sin(theta) * b;
            Vector3 local = new Vector3(x, 0f, z);
            line.SetPosition(i, c + (planeRotation * local));
        }
        line.loop = true;
    }

    void AssignPhysicalPeriodsByName(out float yearDays, out float dayHours, out bool retrograde, out float axialTiltDeg)
    {
        string nm = gameObject.name.ToUpperInvariant();
        retrograde = false;
        axialTiltDeg = 0f;

        // Defaults to Earth-like if no match
        yearDays = 365f; dayHours = 24f; axialTiltDeg = 23.5f;

        if (nm.Contains("MERCURY")) { yearDays = 88f; dayHours = 1408f; axialTiltDeg = 0.03f; }            // very slow spin [web:77][web:76]
        else if (nm.Contains("VENUS")) { yearDays = 225f; dayHours = 5832f; retrograde = true; axialTiltDeg = 177.4f; } // slow retrograde [web:77][web:76]
        else if (nm.Contains("EARTH")) { yearDays = 365f; dayHours = 24f; axialTiltDeg = 23.5f; }         // baseline [web:77][web:76]
        else if (nm.Contains("MARS")) { yearDays = 687f; dayHours = 24.6f; axialTiltDeg = 25.2f; }        // sol ~24.6 h [web:77][web:82]
        else if (nm.Contains("JUPITER")) { yearDays = 4333f; dayHours = 9.9f; axialTiltDeg = 3.1f; }      // fast spin [web:77][web:76]
        else if (nm.Contains("SATURN")) { yearDays = 10759f; dayHours = 10.7f; axialTiltDeg = 26.7f; }    // fast spin [web:77][web:79]
        else if (nm.Contains("URANUS")) { yearDays = 30687f; dayHours = 17.2f; retrograde = true; axialTiltDeg = 97.8f; } // tilted, retrograde [web:77][web:79]
        else if (nm.Contains("NEPTUNE")) { yearDays = 60190f; dayHours = 16.1f; axialTiltDeg = 28.3f; }   // [web:77][web:79]
        else if (nm.Contains("PLUTO")) { yearDays = 90520f; dayHours = 153.3f; axialTiltDeg = 122.5f; }   // ~248 y, ~6.4 d [web:79][web:77]
    }
}
