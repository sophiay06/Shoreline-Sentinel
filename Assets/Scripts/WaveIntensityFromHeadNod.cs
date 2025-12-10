using System;
using UnityEngine;
using Unity.XR.CoreUtils;

public class WaveIntensityFromHeadNod : MonoBehaviour
{
    [Header("Scene refs")]
    public UIBinder uiBinder;
    public XROrigin xrOrigin;
    public Camera hmdCamera;

    [Header("Calibration (head upright, eyes forward)")]
    [Tooltip("Automatically capture baseline head pose on start.")]
    public bool autoCalibrateOnStart = true;

    [Tooltip("Seconds to hold head upright / eyes forward at start for averaging.")]
    public float baselineSampleSeconds = 0.5f;

    [Header("Mapping: head nod angle (deg) → wave intensity [0..1]")]
    [Tooltip("Angle (deg) below which we treat the head as 'still' (no intensity change).")]
    public float minNodDeg = 2.0f;

    [Tooltip("Angle (deg) at which wave intensity reaches 1.0. " + "Bigger nods beyond this are clamped.")]
    public float maxNodDeg = 25.0f;

    [Tooltip("If true, nodding UP increases intensity instead of nodding DOWN.")]
    public bool invert = false;

    [Header("Output range")]
    [Range(0f, 1f)] public float minIntensity01 = 0.0f;
    [Range(0f, 1f)] public float maxIntensity01 = 1.0f;

    [Header("Smoothing")]
    public OneDimSmoother nodFilter = new OneDimSmoother
    {
        value01       = 0.0f,
        deadZone      = 0.02f,
        hysteresis    = 0.01f,
        emaTau        = 0.18f,
        maxDeltaPerSec = 1.0f
    };

    [Header("Debug")]
    public bool detailedDebug = true;

    //internal
    bool _calibrated;
    float _calibTimer;
    Vector3 _baselineForwardAccum;
    Vector3 _baselineRightAccum;
    int _calibSamples;

    Vector3 _baselineForwardWorld;
    Vector3 _baselineRightWorld;

    float _intensity01;

    void Awake()
    {
        if (!hmdCamera && xrOrigin && xrOrigin.Camera)
            hmdCamera = xrOrigin.Camera;
        if (!hmdCamera)
            hmdCamera = Camera.main;

        if (!hmdCamera && detailedDebug)
            Debug.LogWarning("[WaveNod] No HMD Camera assigned or found.");
    }

    void OnEnable()
    {
        _calibrated           = false;
        _calibTimer           = 0f;
        _calibSamples         = 0;
        _baselineForwardAccum = Vector3.zero;
        _baselineRightAccum   = Vector3.zero;
        _intensity01          = 0f;
        nodFilter.Snap(0f);

        if (autoCalibrateOnStart && detailedDebug)
            Debug.Log("[WaveNod] Hold head upright, eyes forward for ~" + baselineSampleSeconds + "s to calibrate.");
    }

    void Update()
    {
        if (!uiBinder || !hmdCamera)
        {
            if (detailedDebug)
                Debug.LogWarning("[WaveNod] Missing uiBinder or HMD Camera.");
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        var cam = hmdCamera.transform;
        Vector3 fwd = cam.forward;
        Vector3 right = cam.right;

        //calibration
        if (!_calibrated)
        {
            if (autoCalibrateOnStart)
            {
                _calibTimer += dt;
                _baselineForwardAccum += fwd;
                _baselineRightAccum   += right;
                _calibSamples++;

                if (_calibTimer >= baselineSampleSeconds && _calibSamples > 0)
                {
                    _baselineForwardWorld =
                        (_baselineForwardAccum / _calibSamples).normalized;
                    _baselineRightWorld =
                        (_baselineRightAccum / _calibSamples).normalized;

                    _calibrated = true;
                    nodFilter.Snap(0f);
                    _intensity01 = 0f;

                    if (detailedDebug)
                        Debug.Log("[WaveNod] Baseline head pose captured.");
                }
            }
            else
            {
                _baselineForwardWorld = fwd.normalized;
                _baselineRightWorld   = right.normalized;
                _calibrated           = true;
                nodFilter.Snap(0f);
                _intensity01 = 0f;

                if (detailedDebug)
                    Debug.Log("[WaveNod] Baseline head pose captured (single frame).");
            }

            return; // don't drive waves until baseline known
        }

        //nod angle
        Vector3 currentForward = fwd.normalized;

        // Signed angle between baseline forward and current forward, measured around baseline right axis:
        //  - Positive = nodding downward (looking toward the ground)
        //  - Negative = nodding upward
        float signedAngle = Vector3.SignedAngle(
            _baselineForwardWorld,
            currentForward,
            _baselineRightWorld
        );

        // Decide which direction counts as "intensity"
        float nodDeg = invert
            ? Mathf.Max(0f, -signedAngle)
            : Mathf.Max(0f, signedAngle);

        // Small head jitter → 0
        if (Mathf.Abs(nodDeg) < minNodDeg)
            nodDeg = 0f;

        // Clamp to configured nod range
        float clamped = Mathf.Clamp(nodDeg, 0f, maxNodDeg);

        float t = 0f;
        if (!Mathf.Approximately(maxNodDeg, 0f))
            t = Mathf.InverseLerp(0f, maxNodDeg, clamped);

        float lo = Mathf.Min(minIntensity01, maxIntensity01);
        float hi = Mathf.Max(minIntensity01, maxIntensity01);
        float target01 = Mathf.Lerp(lo, hi, t);

        _intensity01 = nodFilter.Step(target01, dt);

        ApplyWaveIntensity(_intensity01, nodDeg, clamped, t);
    }

    void ApplyWaveIntensity(float v01, float nodDeg, float clamped, float t)
    {
        //clamp final intensity
        float intensity = Mathf.Clamp01(v01);

        if (uiBinder.sWaveHeight)
            uiBinder.sWaveHeight.SetValueWithoutNotify(intensity);

        uiBinder.OnWaveSliderChanged(intensity);

        if (detailedDebug)
        {
            Debug.Log($"[WaveNod] nodDeg={nodDeg:F1}° (clamped {clamped:F1}°) t={t:F2} intensity={intensity:F2}");
        }
    }
}
