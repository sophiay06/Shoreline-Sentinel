using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class DuneHeightFromRightHand : MonoBehaviour
{
    [Header("Scene refs")]
    public UIBinder uiBinder;
    public XROrigin xrOrigin;
    public Camera hmdCamera;

    [Header("Mapping: (RIGHT wrist Y relative to hip) cm → dune height [0..1]")]
    [Tooltip("Lower bound of RIGHT wrist height relative to hip (cm). Can be negative for 'below hip'.")]
    public float minAboveHipCm = -10f;
    [Tooltip("Upper bound of RIGHT wrist height relative to hip (cm).")]
    public float maxAboveHipCm = 30f;
    public bool invert = false;

    [Header("Hip model (HMD-local)")]
    [Tooltip("Approx hip vertical offset from the HMD (meters, negative = below eyes).")]
    public float hipYOffsetFromHead = -0.8f;

    [Header("Output range")]
    [Range(0f, 1f)] public float minHeight01 = 0.0f;
    [Range(0f, 1f)] public float maxHeight01 = 1.0f;

    [Header("Smoothing")]
    public OneDimSmoother duneHeightFilter = new OneDimSmoother
    {
        value01 = 0.0f,
        deadZone = 0.02f,
        hysteresis = 0.01f,
        emaTau = 0.18f,
        maxDeltaPerSec = 0.7f
    };

    [Tooltip("Ignore extremely fast wrist moves (m/s).")]
    public float maxWristSpeed = 1.2f;

    [Header("Debug")]
    public bool detailedDebug = true;
    public bool drawDebugRays = true;

    // internal
    XRHandSubsystem _hands;
    float _height01;
    bool  _hasPrevWrist;
    Vector3 _prevWrist;

    void OnEnable()
    {
        _height01 = 0f;
        duneHeightFilter.Snap(0f);
        _hasPrevWrist = false;

        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0)
            _hands = list[0];

        if (detailedDebug)
            Debug.Log("[DuneHeight] Using RIGHT wrist vertical mapping (no SlimeVR).");
    }

    void Update()
    {
        // everything needed is assigned
        if (!uiBinder || uiBinder.dune == null)
        {
            if (detailedDebug)
                Debug.LogWarning("[DuneHeight] Missing reference (uiBinder / dune).");
            return;
        }

        if (_hands == null || !_hands.running)
        {
            if (detailedDebug)
                Debug.Log("[DuneHeight] XRHandSubsystem not running.");
            return;
        }

        XRHand right = _hands.rightHand;
        if (!right.isTracked)
        {
            if (detailedDebug)
                Debug.Log("[DuneHeight] Right hand NOT tracked.");
            return;
        }

        // Get RIGHT wrist pose
        Pose wristPose;
        var joint = right.GetJoint(XRHandJointID.Wrist);
        if (!joint.TryGetPose(out wristPose))
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // ignore very fast wrist moves
        if (_hasPrevWrist)
        {
            float speed = Vector3.Distance(wristPose.position, _prevWrist) / Mathf.Max(dt, 0.0001f);
            if (speed > maxWristSpeed)
            {
                if (detailedDebug)
                    Debug.Log($"[DuneHeight] Ignoring frame due to high wrist speed: {speed:F2} m/s");
                _prevWrist = wristPose.position;
                return;
            }
        }
        _prevWrist = wristPose.position;
        _hasPrevWrist = true;

        // Resolve camera (for HMD-local space)
        Camera cam = hmdCamera;
        if (!cam)
        {
            if (xrOrigin && xrOrigin.Camera) cam = xrOrigin.Camera;
            if (!cam) cam = Camera.main;
            if (!cam) return;
        }

        // convert wrist position into camera space
        Vector3 wristWorld = wristPose.position;
        Vector3 wristCam   = cam.transform.InverseTransformPoint(wristWorld);

        // Hip baseline in camera space (approx)
        float hipCamY = hipYOffsetFromHead;
        float deltaCm = (wristCam.y - hipCamY) * 100f;

        // map vertical delta to [0,1]
        float clamped = Mathf.Clamp(deltaCm, minAboveHipCm, maxAboveHipCm);
        float raw01   = Mathf.InverseLerp(minAboveHipCm, maxAboveHipCm, clamped);
        if (invert) raw01 = 1f - raw01;

        float lo = Mathf.Min(minHeight01, maxHeight01);
        float hi = Mathf.Max(minHeight01, maxHeight01);
        float target01 = Mathf.Lerp(lo, hi, raw01);

        // smooth
        _height01 = duneHeightFilter.Step(target01, dt);

        // apply to dune system
        ApplyDuneHeight01(_height01, deltaCm, raw01);

        if (drawDebugRays)
        {
            Debug.DrawRay(wristWorld, (wristPose.rotation * Vector3.up) * 0.1f, Color.cyan);
            Debug.DrawRay(wristWorld, (wristPose.rotation * Vector3.forward) * 0.1f, Color.magenta);
        }
    }

    void ApplyDuneHeight01(float v01, float deltaCm, float raw01)
    {
        var dune = uiBinder.dune;
        if (dune == null) return;

        float t01 = Mathf.Clamp01(v01);

        dune.heightScale = Mathf.Lerp(dune.minHeightScale, dune.maxHeightScale, t01);

        if (uiBinder.sDuneHeight)
            uiBinder.sDuneHeight.SetValueWithoutNotify(t01);

        uiBinder.RecalculateWaveHeight();

        if (detailedDebug)
        {
            Debug.Log($"[DuneHeight] deltaY={deltaCm:F1}cm  raw01={raw01:F2}  duneHeight01={t01:F2}");
        }
    }
}
