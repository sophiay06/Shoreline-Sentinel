using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class VegDensityFromGrip : MonoBehaviour
{
    [Header("Finger distance (cm) → density (LEFT hand)")]
    public UIBinder uiBinder;
    public XROrigin xrOrigin;
    public Camera hmdCamera;

    [Header("Finger spacing (cm) → density")]
    public float minApertureCm = 2f;
    public float maxApertureCm = 10f;
    public bool invert = false;

    [Header("Density response shaping")]
    [Range(0.25f, 4f)] public float responseExponent = 0.7f;
    [Range(0f, 1f)] public float minDensity01 = 0f;
    [Range(0f, 1f)] public float maxDensity01 = 1f;

    [Header("Startup density")]
    public bool initializeFromUI = true;
    [Range(0f, 1f)] public float fallbackInitialDensity = 0.35f;

    [Header("Noise controls")]
    public OneDimSmoother densityFilter = new OneDimSmoother
    {
        value01 = 0f,
        deadZone = 0.03f,
        hysteresis = 0.02f,
        emaTau = 0.16f,
        maxDeltaPerSec = 0.5f
    };
    

    public bool detailedDebug = false;
    public bool drawGizmos = false;

    XRHandSubsystem _hands;
    float _density01;
    bool _seeded;

    void Awake()
    {
        if (!hmdCamera)
        {
            if (xrOrigin && xrOrigin.Camera) hmdCamera = xrOrigin.Camera;
            if (!hmdCamera) hmdCamera = Camera.main;
        }
    }

    void OnEnable()
    {
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0) _hands = list[0];

        if (!_seeded)
        {
            float start = fallbackInitialDensity;

            if (initializeFromUI && uiBinder != null)
            {
                try
                {
                    // uiBinder.veg.density
                    var vegField = uiBinder.GetType().GetField("veg");
                    if (vegField != null)
                    {
                        var veg = vegField.GetValue(uiBinder);
                        if (veg != null)
                        {
                            var densityField = veg.GetType().GetField("density");
                            if (densityField != null)
                            {
                                start = Mathf.Clamp01((float)densityField.GetValue(veg));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (detailedDebug)
                        Debug.LogWarning($"[VegDensityFromGrip] Failed to seed from UI: {e.Message}");
                }
            }

            _density01 = Mathf.Clamp01(start);
            densityFilter.Snap(_density01);
            _seeded = true;

            if (uiBinder != null)
                uiBinder.SetVegDensity01(_density01, recalcWave: true);

            if (detailedDebug)
                Debug.Log($"[VegDensityFromGrip] Seeded starting density = {_density01:F2}");
        }
    }

    void Update()
    {
        if (_hands == null || uiBinder == null) return;
        if (!_hands.running) return;

        XRHand left = _hands.leftHand;
        if (!left.isTracked)
        {
            if (detailedDebug) Debug.Log("[VegDensityLeftDebug] Left hand NOT tracked.");
            return;
        }

        // distance between fingers
        if (!TryGetPoseSafe(left, XRHandJointID.IndexTip, out Pose indexTip)) return;
        if (!TryGetPoseSafe(left, XRHandJointID.MiddleTip, out Pose middleTip)) return;

        float fingerSpacingCm = Vector3.Distance(indexTip.position, middleTip.position) * 100f;

        // Map spacing
        float clamped = Mathf.Clamp(fingerSpacingCm, minApertureCm, maxApertureCm);
        float tLin = Mathf.InverseLerp(minApertureCm, maxApertureCm, clamped);
        if (invert) tLin = 1f - tLin;

        float tShaped = Mathf.Pow(Mathf.Clamp01(tLin), responseExponent);
        float low  = Mathf.Min(minDensity01, maxDensity01);
        float high = Mathf.Max(minDensity01, maxDensity01);
        float t = Mathf.Lerp(low, high, tShaped);

        float blendedTarget = t;

        //smoothing
        float dt = Time.deltaTime;
        _density01 = densityFilter.Step(blendedTarget, dt);

        //to UI 
        uiBinder.SetVegDensity01(_density01, recalcWave: true);

        if (detailedDebug)
        {
            Debug.Log(
                $"[VegDensityLeftDebug] fingerSpacing={fingerSpacingCm:F1}cm  t={t:F2}  " +
                $"density01={_density01:F2}"
            );
        }
    }

    bool TryGetPoseSafe(XRHand hand, XRHandJointID id, out Pose pose)
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

    void OnDrawGizmos()
    {
        if (!drawGizmos || _hands == null || !_hands.running) return;

        XRHand left = _hands.leftHand;
        if (!left.isTracked) return;

        if (TryGetPoseSafe(left, XRHandJointID.IndexTip, out Pose index))
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(index.position, 0.01f);
        }
        if (TryGetPoseSafe(left, XRHandJointID.MiddleTip, out Pose middle))
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(middle.position, 0.01f);
        }
    }
}
