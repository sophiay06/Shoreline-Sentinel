using UnityEngine;

public class SandbarDistanceFromRightLeg : MonoBehaviour
{
    [Header("Scene refs")]
    public UIBinder uiBinder;

    [Tooltip("Transform representing the LEFT hip / upper leg root (e.g., left hip bone).")]
    public Transform leftHip;

    [Tooltip("Transform driven by SlimeVR / rig for LEFT ankle / foot.")]
    public Transform leftAnkle;

    [Header("Calibration (left foot under hip)")]
    [Tooltip("Automatically capture baseline LEFT FOOT UNDER HIP after entering Play mode.")]
    public bool autoCalibrateOnStart = true;

    [Tooltip("Seconds to hold baseline pose (left foot under hip) for averaging.")]
    public float baselineSampleSeconds = 0.7f;

    [Header("Mapping: forward shift (m) → distance [0..1]")]
    [Tooltip("Forward shift (meters) relative to baseline at which distance01 = 0.")]
    public float minForwardShift = 0.00f;

    [Tooltip("Forward shift (meters) relative to baseline at which distance01 = 1.")]
    public float maxForwardShift = 0.40f;

    [Tooltip("Invert mapping (more forward = closer instead of farther)?")]
    public bool invert = false;

    [Header("Output range")]
    [Range(0f, 1f)] public float minDistance01 = 0.0f;
    [Range(0f, 1f)] public float maxDistance01 = 1.0f;

    [Header("Smoothing")]
    public OneDimSmoother barZFilter = new OneDimSmoother
    {
        value01 = 0.0f,
        deadZone = 0.02f,
        hysteresis = 0.01f,
        emaTau = 0.18f,
        maxDeltaPerSec = 0.7f
    };

    [Header("Debug")]
    public bool detailedDebug = true;

    // internal
    bool  _calibrated;
    float _baselineForwardLocalZ;
    float _sampleTimer;
    float _sampleSumZ;
    int   _sampleCount;

    void OnEnable()
    {
        _calibrated   = false;
        _sampleTimer  = 0f;
        _sampleSumZ   = 0f;
        _sampleCount  = 0;

        if (detailedDebug)
        {
            Debug.Log(
                "[BarZ] Hold LEFT FOOT directly under LEFT HIP for ~" +
                baselineSampleSeconds + "s to calibrate baseline sandbar distance."
            );
        }
    }

    void Update()
    {
        if (!uiBinder || uiBinder.bar == null || !leftHip || !leftAnkle)
        {
            if (detailedDebug)
                Debug.LogWarning("[BarZ] Missing reference (uiBinder / bar / leftHip / leftAnkle)");
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Left ankle position in LEFT HIP local space
        Vector3 ankleLocal = leftHip.InverseTransformPoint(leftAnkle.position);
        float currentForwardZ = ankleLocal.z; // forward relative to left hip

        // calibration
        if (!_calibrated)
        {
            if (autoCalibrateOnStart)
            {
                _sampleTimer += dt;
                _sampleSumZ  += currentForwardZ;
                _sampleCount++;

                if (_sampleTimer >= baselineSampleSeconds && _sampleCount > 0)
                {
                    _baselineForwardLocalZ = _sampleSumZ / _sampleCount;
                    _calibrated = true;

                    if (detailedDebug)
                        Debug.Log($"[BarZ] Baseline leftHip-local ankle.z = {_baselineForwardLocalZ:F3} m");
                }
            }
            else
            {
                _baselineForwardLocalZ = currentForwardZ;
                _calibrated = true;

                if (detailedDebug)
                    Debug.Log($"[BarZ] Baseline leftHip-local ankle.z (single frame) = {_baselineForwardLocalZ:F3} m");
            }

            return;
        }

        // move forward
        float forwardShift = currentForwardZ - _baselineForwardLocalZ;

        float clamped = Mathf.Clamp(forwardShift, minForwardShift, maxForwardShift);
        float t = 0f;

        if (!Mathf.Approximately(maxForwardShift - minForwardShift, 0f))
            t = Mathf.InverseLerp(minForwardShift, maxForwardShift, clamped);

        if (invert) t = 1f - t;

        float lo = Mathf.Min(minDistance01, maxDistance01);
        float hi = Mathf.Max(minDistance01, maxDistance01);

        float target01 = Mathf.Lerp(lo, hi, t);
        float filtered = barZFilter.Step(target01, dt);

        ApplyDistance(filtered, currentForwardZ, forwardShift, clamped, t);
    }

    void ApplyDistance(float v01, float currentZ, float shift, float clamped, float t)
    {
        var bar = uiBinder.bar;
        if (bar == null) return;

        float offset = Mathf.Lerp(bar.minZOffset, bar.maxZOffset, v01);
        bar.zOffset = offset;

        if (uiBinder.sBarZ)
            uiBinder.sBarZ.SetValueWithoutNotify(v01);

        uiBinder.RecalculateWaveHeight();

        if (detailedDebug)
        {
            Debug.Log(
                $"[BarZ] ankleLocalZ={currentZ:F3}m shift={shift:F3}m (clamped {clamped:F3}m) " +
                $"t={t:F2} v01={v01:F2}"
            );
        }
    }
}
