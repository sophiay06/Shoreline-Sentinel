using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class DuneWidthFromRightGrip : MonoBehaviour
{
    [Header("Scene refs")]
    public UIBinder uiBinder;

    [Header("XR Hands")]
    public XROrigin xrOrigin;
    public Camera hmdCamera;

    [Header("Finger distance mapping (RIGHT hand)")]
    [Tooltip("Finger distance (cm) at which dune width = 0. Fingers almost touching.")]
    public float minFingerDistanceCm = 0.5f;

    [Tooltip("Finger distance (cm) at which dune width = 1. Fingers comfortably spread.")]
    public float maxFingerDistanceCm = 8f;

    [Tooltip("If true, larger finger distance makes dunes NARROWER instead of wider.")]
    public bool invert = false;

    [Header("Output range")]
    [Range(0f, 1f)] public float minWidth01 = 0.0f;
    [Range(0f, 1f)] public float maxWidth01 = 1.0f;

    [Header("Smoothing")]
    public OneDimSmoother duneWidthFilter = new OneDimSmoother
    {
        value01 = 0.0f,
        deadZone = 0.02f,
        hysteresis = 0.01f,
        emaTau = 0.18f,
        maxDeltaPerSec = 0.7f
    };

    [Tooltip("Ignore extremely fast changes in finger distance (cm/s).")]
    public float maxFingerSpeedCmPerSec = 150f;

    [Header("Debug")]
    public bool detailedDebug = true;

    // internal
    XRHandSubsystem _hands;
    float _width01;
    bool _hasPrevDist;
    float _prevDistCm;

    void OnEnable()
    {
        _width01 = 0f;
        duneWidthFilter.Snap(0f);
        _hasPrevDist = false;

        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0)
            _hands = list[0];

        if (detailedDebug)
            Debug.Log("[DuneWidth] Using RIGHT-hand finger distance mapping (no SlimeVR).");
    }

    void Update()
    {
        if (!uiBinder || uiBinder.dune == null)
        {
            if (detailedDebug)
                Debug.LogWarning("[DuneWidth] Missing reference (uiBinder / dune).");
            return;
        }

        if (_hands == null || !_hands.running)
        {
            if (detailedDebug)
                Debug.Log("[DuneWidth] XRHandSubsystem not running.");
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        XRHand right = _hands.rightHand;
        if (!right.isTracked)
        {
            if (detailedDebug)
                Debug.Log("[DuneWidth] Right hand NOT tracked.");
            return;
        }

        // Get thumb tip and index tip poses
        var thumbJoint = right.GetJoint(XRHandJointID.ThumbTip);
        var indexJoint = right.GetJoint(XRHandJointID.IndexTip);

        Pose thumbPose, indexPose;
        if (!thumbJoint.TryGetPose(out thumbPose) || !indexJoint.TryGetPose(out indexPose))
            return;

        // distance between fingers in meters → cm
        float distMeters = Vector3.Distance(thumbPose.position, indexPose.position);
        float distCm = distMeters * 100f;

        // ignore very fast finger changes
        if (_hasPrevDist)
        {
            float speedCmPerSec = Mathf.Abs(distCm - _prevDistCm) / Mathf.Max(dt, 0.0001f);
            if (speedCmPerSec > maxFingerSpeedCmPerSec)
            {
                if (detailedDebug)
                    Debug.Log($"[DuneWidth] Ignoring frame due to high finger speed: {speedCmPerSec:F1} cm/s");
                _prevDistCm = distCm;
                return;
            }
        }
        _prevDistCm = distCm;
        _hasPrevDist = true;

        // Map finger distance → [0,1]
        float clamped = Mathf.Clamp(distCm, minFingerDistanceCm, maxFingerDistanceCm);
        float raw01 = Mathf.InverseLerp(minFingerDistanceCm, maxFingerDistanceCm, clamped);
        if (invert) raw01 = 1f - raw01;

        float lo = Mathf.Min(minWidth01, maxWidth01);
        float hi = Mathf.Max(minWidth01, maxWidth01);
        float target01 = Mathf.Lerp(lo, hi, raw01);

        _width01 = duneWidthFilter.Step(target01, dt);

        ApplyDuneWidth01(_width01, distCm, raw01);
    }

    void ApplyDuneWidth01(float v01, float distCm, float raw01)
    {
        var dune = uiBinder.dune;
        if (dune == null) return;

        float t01 = Mathf.Clamp01(v01);

        // Scale dune width via DuneController
        dune.widthScale = Mathf.Lerp(dune.minWidthScale, dune.maxWidthScale, t01);

        if (uiBinder.sDuneWidth)
            uiBinder.sDuneWidth.SetValueWithoutNotify(t01);

        uiBinder.RecalculateWaveHeight();

        if (detailedDebug)
        {
            Debug.Log($"[DuneWidth] fingerDist={distCm:F1}cm  raw01={raw01:F2}  duneWidth01={t01:F2}");
        }
    }
}
