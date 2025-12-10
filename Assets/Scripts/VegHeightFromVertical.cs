using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class VegHeightFromVertical : MonoBehaviour
{
    [Header("Scene refs")]
    public UIBinder uiBinder;
    public XROrigin xrOrigin;
    public Camera hmdCamera;

    [Header("Mapping: (LEFT wrist Y relative to waist) cm → veg height [0..1]")]
    [Tooltip("Lower bound of LEFT wrist height relative to waist (cm). Can be negative for 'below waist'.")]
    public float minAboveWaistCm = -10f;
    [Tooltip("Upper bound of LEFT wrist height relative to waist (cm).")]
    public float maxAboveWaistCm = 30f;
    public bool invert = false;

    [Header("Height range (normalized output)")]
    [Range(0f, 1f)] public float minHeightNormalized = 0.3f;
    [Range(0f, 1f)] public float maxHeightNormalized = 1.0f;

    [Header("Waist model (HMD-local)")]
    [Tooltip("Approx waist vertical offset from the HMD (meters, negative = below eyes).")]
    public float waistYOffsetFromHead = -0.7f;

    [Header("Palm pose (informational only)")]
    [Tooltip("Palm 'inward' is not enforced in code; this is just documentation for the intended pose.")]
    [Range(0f, 1f)] public float palmNeutralAbsDotMax = 0.35f;
    [Tooltip("If true, auto-flip palm normal between -forward / +forward based on which is closer to horizontal.")]
    public bool autoFlipPalmNormal = true;

    [Header("Startup")]
    [Tooltip("Seed from VegetationController.treeHeightScale on start")]
    public bool initializeFromVeg = true;

    [Header("Noise controls")]
    public OneDimSmoother heightFilter = new OneDimSmoother
    {
        value01 = 0.5f,
        deadZone = 0.03f,
        hysteresis = 0.02f,
        emaTau = 0.18f,
        maxDeltaPerSec = 0.5f
    };

    [Tooltip("Ignore extremely fast wrist moves(m/s)")]
    public float maxPalmSpeed = 1.2f;

    [Header("Debug")]
    public bool detailedDebug = true;
    public bool drawDebugRays = true;

    XRHandSubsystem _hands;
    float _height01 = 0.5f;
    bool _seeded;
    Vector3 _prevPalm;
    bool _hasPrevPalm;

    void Awake()
    {
        if (!hmdCamera && xrOrigin && xrOrigin.Camera) hmdCamera = xrOrigin.Camera;
        if (!hmdCamera) hmdCamera = Camera.main;
    }

    void OnEnable()
    {
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0) _hands = list[0];

        if (initializeFromVeg && uiBinder != null && uiBinder.veg != null && !_seeded)
        {
            try
            {
                var veg = uiBinder.veg;
                float minH = veg.minTreeHeightScale;
                float maxH = veg.maxTreeHeightScale;
                float curH = Mathf.Clamp(veg.treeHeightScale, minH, maxH);
                _height01 = Mathf.InverseLerp(minH, maxH, curH);
                heightFilter.Snap(_height01);

                if (uiBinder.sVegHeight)
                    uiBinder.sVegHeight.SetValueWithoutNotify(_height01);

                uiBinder.RecalculateWaveHeight();
                _seeded = true;
            }
            catch (Exception e)
            {
                if (detailedDebug)
                    Debug.LogWarning($"[VegHeightFromVertical] Failed to seed from veg: {e.Message}");
            }
        }
    }

    void Update()
    {
        if (_hands == null || uiBinder == null || uiBinder.veg == null) return;
        if (!_hands.running) return;

        XRHand left = _hands.leftHand;
        if (!left.isTracked)
        {
            if (detailedDebug) Debug.Log("[HeightDebug] Left hand NOT tracked.");
            return;
        }

        if (!TryPose(left, XRHandJointID.Wrist, out Pose wristPose))
            return;

        float dt = Time.deltaTime;

        //ignore very fast hand/wrist moves
        if (_hasPrevPalm)
        {
            float speed = Vector3.Distance(wristPose.position, _prevPalm) / Mathf.Max(dt, 0.0001f);
            if (speed > maxPalmSpeed)
            {
                if (detailedDebug)
                    Debug.Log($"[HeightDebug] Ignoring frame due to high wrist speed: {speed:F2} m/s");
                _prevPalm = wristPose.position;
                return;
            }
        }
        _prevPalm = wristPose.position;
        _hasPrevPalm = true;

        Camera cam = hmdCamera;
        if (!cam)
        {
            if (xrOrigin && xrOrigin.Camera) cam = xrOrigin.Camera;
            if (!cam) cam = Camera.main;
            if (!cam) return;
        }

        // Camera-space wrist position
        Vector3 wristWorld = wristPose.position;
        Vector3 wristCam = cam.transform.InverseTransformPoint(wristWorld);

        // Waist baseline in camera space (approx)
        float waistCamY = waistYOffsetFromHead; // meters, negative below eyes
        float deltaCm = (wristCam.y - waistCamY) * 100f; // above-waist in cm

        // Map to [0,1]
        float clamped = Mathf.Clamp(deltaCm, minAboveWaistCm, maxAboveWaistCm);
        float t = Mathf.InverseLerp(minAboveWaistCm, maxAboveWaistCm, clamped);
        if (invert) t = 1f - t;

        //smoothing: no activation pose / dwell / cooldown
        _height01 = heightFilter.Step(t, dt);
        ApplyVegHeight01(_height01);

        if (detailedDebug)
        {
            Debug.Log(
                $"[HeightDebug] wristWorld={wristWorld:F3}  wristCam={wristCam:F3}\n" +
                $"  WaistCamY={waistCamY:F3}  deltaY={deltaCm:F1}cm  mapped={t:F2}  height01={_height01:F2}"
            );
        }

        if (drawDebugRays)
        {
            Debug.DrawRay(wristWorld, (wristPose.rotation * Vector3.up) * 0.1f, Color.cyan);
            Debug.DrawRay(wristWorld, (wristPose.rotation * Vector3.forward) * 0.1f, Color.magenta);
        }
    }

    void ApplyVegHeight01(float v01)
    {
        if (uiBinder == null || uiBinder.veg == null) return;

        // Clamp gesture output to [0,1]
        float t01 = Mathf.Clamp01(v01);

        float low  = Mathf.Min(minHeightNormalized, maxHeightNormalized);
        float high = Mathf.Max(minHeightNormalized, maxHeightNormalized);

        // Remap gesture 0..1 into [low, high]
        float tScaled = Mathf.Lerp(low, high, t01);

        var veg = uiBinder.veg;
        float minH = veg.minTreeHeightScale;
        float maxH = veg.maxTreeHeightScale;

        veg.treeHeightScale = Mathf.Lerp(minH, maxH, tScaled);

        if (uiBinder.sVegHeight)
            uiBinder.sVegHeight.SetValueWithoutNotify(tScaled);

        uiBinder.RecalculateWaveHeight();
    }

    bool TryPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        pose = default;
        if (_hands == null || !_hands.running || !hand.isTracked) return false;
        try
        {
            var j = hand.GetJoint(id);
            return j.TryGetPose(out pose);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
