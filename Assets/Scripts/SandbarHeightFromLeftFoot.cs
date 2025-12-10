using UnityEngine;

public class SandbarHeightFromLeftFoot : MonoBehaviour
{
    [Header("Scene refs")]
    public UIBinder uiBinder;
    [Tooltip("Transform driven by SlimeVROscReceiver for LEFT ankle/foot.")]
    public Transform leftAnkle;

    [Header("Calibration (left foot flat)")]
    [Tooltip("Automatically capture baseline foot rotation after entering Play mode.")]
    public bool autoCalibrateOnStart = true;
    [Tooltip("Seconds to stand with LEFT FOOT FLAT during calibration.")]
    public float baselineSampleSeconds = 0.7f;

    [Header("Mapping: dorsiflexion angle (deg) → sandbar height [0..1]")]
    [Tooltip("Minimum dorsiflexion angle in degrees (typically 0 = foot flat).")]
    public float minDorsiDeg = 0f;
    [Tooltip("Maximum dorsiflexion angle in degrees mapped to barHeight = 1 (e.g., 20–30°).")]
    public float maxDorsiDeg = 25f;
    [Tooltip("If true, more dorsiflexion LOWERS the sandbar instead of raising it.")]
    public bool invert = false;

    [Header("Output range")]
    [Range(0f, 1f)] public float minHeight01 = 0.0f;
    [Range(0f, 1f)] public float maxHeight01 = 1.0f;

    [Header("Smoothing")]
    public OneDimSmoother barHeightFilter = new OneDimSmoother
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
    bool   _calibrated;
    float  _sampleTimer;
    Quaternion _baselineRot;
    Quaternion _sampleRotAccum;
    int    _sampleCount;
    float  _height01;

    void OnEnable()
    {
        _calibrated      = false;
        _sampleTimer     = 0f;
        _sampleRotAccum  = Quaternion.identity;
        _sampleCount     = 0;

        if (autoCalibrateOnStart && detailedDebug)
            Debug.Log("[BarHeight] Stand with LEFT FOOT FLAT for ~0.7s to calibrate.");
    }

    void Update()
    {
        if (!uiBinder || uiBinder.bar == null || !leftAnkle)
        {
            if (detailedDebug)
                Debug.LogWarning("[BarHeight] Missing reference (uiBinder / bar / leftAnkle).");
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Quaternion currentRot = leftAnkle.rotation;

        //calibration
        if (!_calibrated)
        {
            if (autoCalibrateOnStart)
            {
                _sampleTimer += dt;
                _sampleRotAccum = (_sampleCount == 0)
                    ? currentRot
                    : SlerpWeighted(_sampleRotAccum, currentRot, _sampleCount);
                _sampleCount++;

                if (_sampleTimer >= baselineSampleSeconds && _sampleCount > 0)
                {
                    _baselineRot  = _sampleRotAccum;
                    _calibrated   = true;
                    barHeightFilter.Snap(0f);
                    _height01 = 0f;

                    if (detailedDebug)
                        Debug.Log("[BarHeight] Baseline rotation (left foot flat) captured.");
                }
            }
            else
            {
                _baselineRot  = currentRot;
                _calibrated   = true;
                barHeightFilter.Snap(0f);
                _height01 = 0f;

                if (detailedDebug)
                    Debug.Log("[BarHeight] Baseline rotation (single frame) captured.");
            }

            return;
        }

        // dorsiflexion angle, compute rotation of current foot relative to baseline flat rotation
        Quaternion delta = Quaternion.Inverse(_baselineRot) * currentRot;
        Vector3 deltaEuler = delta.eulerAngles;

        // Convert to signed pitch around local X axis
        float pitch = deltaEuler.x;
        if (pitch > 180f) pitch -= 360f;

        // treat positive pitch as dorsiflexion (toes up); ignore plantarflexion (negative)
        float dorsiDeg = Mathf.Max(0f, pitch);

        // Clamp to configured range
        float clamped = Mathf.Clamp(dorsiDeg, minDorsiDeg, maxDorsiDeg);

        float t = 0f;
        if (!Mathf.Approximately(maxDorsiDeg - minDorsiDeg, 0f))
            t = Mathf.InverseLerp(minDorsiDeg, maxDorsiDeg, clamped);

        if (invert) t = 1f - t;

        float lo = Mathf.Min(minHeight01, maxHeight01);
        float hi = Mathf.Max(minHeight01, maxHeight01);
        float target01 = Mathf.Lerp(lo, hi, t);

        _height01 = barHeightFilter.Step(target01, dt);

        ApplyBarHeight01(_height01, dorsiDeg, clamped, t);
    }

    void ApplyBarHeight01(float v01, float dorsiDeg, float clamped, float t)
    {
        var bar = uiBinder.bar;
        if (bar == null) return;

        float t01 = Mathf.Clamp01(v01);

        bar.heightScale = Mathf.Lerp(bar.minHeightScale, bar.maxHeightScale, t01);

        if (uiBinder.sBarHeight)
            uiBinder.sBarHeight.SetValueWithoutNotify(t01);

        uiBinder.RecalculateWaveHeight();

        if (detailedDebug)
        {
            Debug.Log(
                $"[BarHeight] dorsi={dorsiDeg:F1}° (clamped {clamped:F1}°) " +
                $"t={t:F2} barHeight01={t01:F2}"
            );
        }
    }

    Quaternion SlerpWeighted(Quaternion acc, Quaternion newRot, int count)
    {
        float alpha = 1f / (count + 1);
        return Quaternion.Slerp(acc, newRot, alpha);
    }
}
