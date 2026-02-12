using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;
using MappingTool;

namespace Signals
{
    public class SignalProvider : MonoBehaviour, ISignalProvider
    {
        [Header("Scene refs")]
        public XROrigin xrOrigin;
        public Camera hmdCamera;

        [Header("Signals enabled")]
        public bool provideLeftFingerDistance = true;
        public bool provideLeftWristHeight = true;
        public bool provideRightFingerDistance = true;
        public bool provideRightWristHeight = true;

        // -------------------------
        // Left finger distance
        // -------------------------
        [Header("Left finger distance (cm) -> 0..1")]
        public float leftMinApertureCm = 2f;
        public float leftMaxApertureCm = 10f;
        public bool leftFingerInvert = false;

        // -------------------------
        // Left wrist height (PURE VERTICAL)
        // -------------------------
        public enum WristYMode
        {
            WorldY,
            HmdRelativeY, 
            TrackingSpaceY 
        }

        [Header("Left wrist vertical mapping")]
        public WristYMode leftWristYMode = WristYMode.WorldY;

        [Tooltip("If using HmdRelativeY, this is wristY - hmdY (meters).")]
        public float leftHmdRelativeYOffsetM = 0.0f; 

        [Header("Map chosen wrist Y (cm) -> 0..1")]
        [Tooltip("Lower bound in cm for the chosen Y source. For WorldY typical 80; for HmdRelativeY typical -50.")]
        public float leftMinWristYcm = 80f;

        [Tooltip("Upper bound in cm for the chosen Y source. For WorldY typical 140; for HmdRelativeY typical 20.")]
        public float leftMaxWristYcm = 140f;

        public bool leftWristInvert = false;

        [Header("Legacy (not used for LEFT anymore)")]
        public float leftMinAboveWaistCm = -5f;
        public float leftMaxAboveWaistCm = 10f;
        [Tooltip("Legacy waist offset (not used for LEFT now)")]
        public float waistYOffsetFromHead = -0.7f;

        [Header("Left wrist noise controls")]
        [Tooltip("Ignore extremely fast wrist moves (m/s)")]
        public float leftMaxWristSpeed = 1.2f;
        public float leftHeightDeadZone01 = 0.03f;
        public float leftHeightHysteresis01 = 0.02f;
        public float leftHeightEmaTau = 0.18f;
        public float leftHeightMaxDeltaPerSec = 0.5f;

        // -------------------------
        // Right finger distance
        // -------------------------
        [Header("Right finger distance (cm) -> 0..1 (thumb tip ↔ index tip)")]
        public float rightMinFingerDistanceCm = 0.5f;
        public float rightMaxFingerDistanceCm = 8f;
        public bool rightFingerInvert = false;

        // -------------------------
        // Right wrist height
        // -------------------------
        [Header("Right wrist vertical mapping")]
        public WristYMode rightWristYMode = WristYMode.WorldY;

        [Tooltip("If using HmdRelativeY, this is wristY - hmdY (meters).")]
        public float rightHmdRelativeYOffsetM = 0.0f; // optional bias

        [Header("Map chosen RIGHT wrist Y (cm) -> 0..1")]
        [Tooltip("Lower bound in cm for the chosen Y source.")]
        public float rightMinWristYcm = 80f;

        [Tooltip("Upper bound in cm for the chosen Y source.")]
        public float rightMaxWristYcm = 140f;

        public bool rightWristInvert = false;

        [Header("Legacy (not used for RIGHT anymore)")]
        [Tooltip("Legacy mapping: (wristWorld.y - hipWorldY) cm -> 0..1 (not used for RIGHT now).")]
        public float rightMinAboveHipCm = -10f;
        public float rightMaxAboveHipCm = 30f;

        [Header("Hip model (HMD-local) (legacy, not used for RIGHT now)")]
        [Tooltip("Approx hip vertical offset from HMD in meters (negative below eyes).")]
        public float hipYOffsetFromHead = -0.8f;

        [Header("Right wrist noise controls")]
        public float rightMaxWristSpeed = 1.2f;
        public float rightHeightDeadZone01 = 0.03f;
        public float rightHeightHysteresis01 = 0.02f;
        public float rightHeightEmaTau = 0.18f;
        public float rightHeightMaxDeltaPerSec = 0.5f;

        [Header("Torso (shoulder) height mapping")]
        public bool provideTorsoShoulderHeight = true;

        [Tooltip("Approx shoulder vertical offset from HMD in meters (negative below eyes).")]
        public float shoulderYOffsetFromHead = -0.20f;

        [Tooltip("Approx left shoulder offset from HMD in meters (negative = left).")]
        public float leftShoulderXOffsetFromHead = -0.18f;

        [Tooltip("forward/back offset from HMD in meters (positive = forward).")]
        public float leftShoulderZOffsetFromHead = 0.05f;


        public WristYMode torsoYMode = WristYMode.WorldY;

        [Tooltip("If using HmdRelativeY, this is (shoulderY - hmdY) + offset (meters).")]
        public float torsoHmdRelativeYOffsetM = 0.0f;

        [Header("Map chosen shoulder Y (cm) -> 0..1")]
        public float torsoMinYcm = 90f;
        public float torsoMaxYcm = 130f;
        public bool torsoInvert = false;

        [Header("Torso noise controls")]
        public float torsoDeadZone01 = 0.02f;
        public float torsoHysteresis01 = 0.01f;
        public float torsoEmaTau = 0.18f;
        public float torsoMaxDeltaPerSec = 0.8f;

        [Header("Torso baseline (recommended for sitting posture)")]
        public bool torsoUseBaseline = true;

        [Tooltip("Optional baseline bias in meters (rarely needed).")]
        public float torsoBaselineBiasM = 0.0f;

        


        private bool _torsoBaselineSeeded;
        private float _torsoBaselineY_m;


        // smoother state
        private float _torso01 = 0.5f;
        private bool _torsoSeeded;

        [Header("Torso Lean (forward/back)")]
        public bool torsoLeanUseBaseline = true;
        public float torsoLeanBaselineBiasDeg = 0f; 
        public float torsoLeanMinDeg = -15f;  
        public float torsoLeanMaxDeg =  15f;   
        public bool torsoLeanInvert = false;

        // filtering
        public float torsoLeanDeadZone01 = 0.03f;
        public float torsoLeanHysteresis01 = 0.01f;
        public float torsoLeanEmaTau = 0.18f;
        public float torsoLeanMaxDeltaPerSec = 0.7f;

        private bool _torsoLeanBaselineSeeded;
        private float _torsoLeanBaselineDeg;
        private bool _torsoLeanSeeded;
        private float _torsoLean01;

        public bool provideTorsoLean = true;

        // -------------------------
        // Torso Yaw
        // -------------------------

        [Header("Torso Yaw (around spine)")]
        public bool provideTorsoYaw = true;

        public bool torsoYawUseBaseline = true;
        public float torsoYawBaselineBiasDeg = 0f;

        [Tooltip("Yaw delta range (deg) mapped to 0..1. Negative = rotate left, positive = rotate right.")]
        public float torsoYawMinDeg = -45f;
        public float torsoYawMaxDeg =  45f;
        public bool torsoYawInvert = false;

        // filtering
        public float torsoYawDeadZone01 = 0.03f;
        public float torsoYawHysteresis01 = 0.01f;
        public float torsoYawEmaTau = 0.18f;
        public float torsoYawMaxDeltaPerSec = 0.7f;

        // state
        private bool _torsoYawBaselineSeeded;
        private float _torsoYawBaselineDeg;
        private bool _torsoYawSeeded;
        private float _torsoYaw01 = 0.5f;

        // -------------------------
        // Head Pitch
        // -------------------------
        [Header("Head Pitch (neck pitch)")]
        public bool provideHeadPitch = true;

        public bool headPitchUseBaseline = true;
        public float headPitchBaselineBiasDeg = 0f;

        [Tooltip("Pitch delta range (deg) mapped to 0..1. Negative = looking down, positive = looking up.")]
        public float headPitchMinDeg = -25f;
        public float headPitchMaxDeg =  25f;
        public bool headPitchInvert = false;

        // filtering
        public float headPitchDeadZone01 = 0.03f;
        public float headPitchHysteresis01 = 0.01f;
        public float headPitchEmaTau = 0.18f;
        public float headPitchMaxDeltaPerSec = 0.7f;

        // state
        private bool _headPitchBaselineSeeded;
        private float _headPitchBaselineDeg;
        private bool _headPitchSeeded;
        private float _headPitch01 = 0.5f;

        // -------------------------
        // Left Arm Shoulder
        // -------------------------
        [Header("Left Arm Shoulder Flexion (approx)")]
        public bool provideLeftShoulderFlexion = true;
        public bool leftShoulderFlexUseBaseline = true;
        public float leftShoulderFlexBaselineBiasDeg = 0f;

        [Tooltip("Flexion delta range (deg) mapped to 0..1. 0 = arm down, positive = lifting forward/up.")]
        public float leftShoulderFlexMinDeg = 0f;
        public float leftShoulderFlexMaxDeg = 90f;
        public bool leftShoulderFlexInvert = false;

        // filtering
        public float leftShoulderFlexDeadZone01 = 0.03f;
        public float leftShoulderFlexHysteresis01 = 0.01f;
        public float leftShoulderFlexEmaTau = 0.18f;
        public float leftShoulderFlexMaxDeltaPerSec = 0.9f;

        // state
        private bool _leftShoulderFlexBaselineSeeded;
        private float _leftShoulderFlexBaselineDeg;
        private bool _leftShoulderFlexSeeded;
        private float _leftShoulderFlex01 = 0f;

        // -------------------------
        // Right Arm Shoulder
        // -------------------------
        [Header("Right Arm Shoulder Abduction (approx)")]
        public bool provideRightShoulderAbduction = true;
        public bool rightShoulderAbdUseBaseline = true;
        public float rightShoulderAbdBaselineBiasDeg = 0f;

        [Tooltip("Abduction delta range (deg) mapped to 0..1. 0 = arm down, + = arm out to the side/up.")]
        public float rightShoulderAbdMinDeg = 0f;
        public float rightShoulderAbdMaxDeg = 90f;
        public bool rightShoulderAbdInvert = false;

        // filtering
        public float rightShoulderAbdDeadZone01 = 0.0f;
        public float rightShoulderAbdHysteresis01 = 0.0f;
        public float rightShoulderAbdEmaTau = 0.12f;
        public float rightShoulderAbdMaxDeltaPerSec = 3.0f;

        // state
        private bool _rightShoulderAbdBaselineSeeded;
        private float _rightShoulderAbdBaselineDeg;
        private bool _rightShoulderAbdSeeded;
        private float _rightShoulderAbd01 = 0f;

        // -------------------------
        // Left Hand Fingers Spread
        // -------------------------
        [Header("Left Hand Finger Spread (Expert)")]
        public bool provideLeftFingerSpread = true;

        [Tooltip("Min average fingertip distance from palm (cm)")]
        public float leftSpreadMinCm = 2f;

        [Tooltip("Max average fingertip distance from palm (cm)")]
        public float leftSpreadMaxCm = 9f;

        public bool leftSpreadInvert = false;

        // filtering
        public float leftSpreadDeadZone01 = 0.01f;
        public float leftSpreadHysteresis01 = 0.005f;
        public float leftSpreadEmaTau = 0.15f;
        public float leftSpreadMaxDeltaPerSec = 2.0f;

        // state
        private bool _leftSpreadSeeded;
        private float _leftSpread01 = 0f;


        // -------------------------
        // Forearm distances
        // -------------------------
        [Header("Forearms distance (Expert)")]
        public bool provideForearmDistances = true;

        [Tooltip("Vertical distance |ΔY| between forearms (cm) mapped to 0..1.")]
        public float forearmsVertMinCm = 0f;
        public float forearmsVertMaxCm = 25f;

        [Tooltip("Invert vertical mapping. For dunes height: closer -> lower dunes, so set TRUE.")]
        public bool forearmsVertInvert = true;

        // filtering (vertical)
        public float forearmsVertDeadZone01 = 0.01f;
        public float forearmsVertHysteresis01 = 0.005f;
        public float forearmsVertEmaTau = 0.18f;
        public float forearmsVertMaxDeltaPerSec = 2.0f;

        // state
        private bool _forearmsVSeeded;
        private float _forearmsV01 = 0f;

        [Tooltip("Horizontal distance (XZ) between forearms (cm) mapped to 0..1.")]
        public float forearmsHorizMinCm = 10f;
        public float forearmsHorizMaxCm = 60f;

        public bool forearmsHorizInvert = false;

        // filtering
        public float forearmsHorizDeadZone01 = 0.01f;
        public float forearmsHorizHysteresis01 = 0.005f;
        public float forearmsHorizEmaTau = 0.18f;
        public float forearmsHorizMaxDeltaPerSec = 2.0f;

        // state
        private bool _forearmsHSeeded;
        private float _forearmsH01 = 0f;

        // -------------------------
        // Right Hand Height
        // -------------------------
        [Header("Right Hand Height (Expert sandbar) - full body")]
        public bool provideRightHandHeight = true;

        public float rightHandMinYcm = 20f;
        public float rightHandMaxYcm = 160f;
        public bool rightHandHeightInvert = false;

        // filtering
        public float rightHandHeightDeadZone01 = 0.01f;
        public float rightHandHeightHysteresis01 = 0.005f;
        public float rightHandHeightEmaTau = 0.20f;
        public float rightHandHeightMaxDeltaPerSec = 1.2f;

        // state
        private bool _rightHandHeightSeeded2;
        private float _rightHandHeight01_2 = 0f;

        // -------------------------
        //Locomotion - walk distance
        // -------------------------
        [Header("Locomotion (Expert) - forward distance from start (XZ)")]
        public bool provideLocomotionDistance = true;

        [Tooltip("0 cm -> 0. Increase from baseline forward movement.")]
        public float walkMinCm = 0f;

        [Tooltip("This cm -> 1. Set small (e.g., 60-100cm) to avoid Guardian.")]
        public float walkMaxCm = 80f;

        [Tooltip("If true, only forward increases (backward clamps to 0).")]
        public bool walkClampBackwardToZero = true;

        public bool walkInvert = false;

        // filtering
        public float walkDeadZone01 = 0.01f;
        public float walkHysteresis01 = 0.005f;
        public float walkEmaTau = 0.25f;
        public float walkMaxDeltaPerSec = 1.5f;

        // baseline
        public bool walkUseBaseline = true;

        [Tooltip("If your tracking recenters and causes a huge jump, reseed baseline.")]
        public float walkRecenterJumpCm = 200f;

        // state
        private bool _walkBaselineSeeded;
        private Vector3 _walkBaselinePosXZ_world;
        private Vector3 _walkBaselineFwdXZ_world;

        private bool _walkSeeded;
        private float _walk01 = 0f;


        // -------------------------
        // Debug
        // -------------------------
        [Header("Debug")]
        public bool debugLogs = true;
        public float debugInterval = 0.25f;

        [Tooltip("Extra per-event debug (spammy). Use only temporarily.")]
        public bool verboseDebug = false;

        private XRHandSubsystem _hands;
        private readonly Dictionary<InputSignal, float> _signals = new();
        private float _nextLog;

        // speed gate state
        private Vector3 _prevLeftWristWorld;
        private bool _hasPrevLeft;
        private Vector3 _prevRightWristWorld;
        private bool _hasPrevRight;

        // smoother state
        private float _leftHeight01 = 0.5f;
        private bool _leftHeightSeeded;
        private float _rightHeight01 = 0.5f;
        private bool _rightHeightSeeded;

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

            if (debugLogs)
            {
                Debug.Log($"[SignalProvider] OnEnable. XRHandSubsystem found? {(_hands != null)} running? {(_hands != null && _hands.running)}");
                Debug.Log($"[SignalProvider] xrOrigin={(xrOrigin ? xrOrigin.name : "NULL")} hmdCamera={(EnsureCamera() ? EnsureCamera().name : "NULL")}");
                Debug.Log($"[SignalProvider] LEFT mode={leftWristYMode} range cm: [{leftMinWristYcm},{leftMaxWristYcm}] invert={leftWristInvert}");
                Debug.Log($"[SignalProvider][OnEnable] OBJ={name} ID={GetInstanceID()} enabled={enabled}");
            }
        }

        void Update()
        {
            if (_hands == null)
            {
                if (debugLogs && Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 1.0f;
                    Debug.LogWarning("[SignalProvider] No XRHandSubsystem found. Hand signals will not update.");
                }
                return;
            }

            if (!_hands.running)
            {
                if (debugLogs && Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 1.0f;
                    Debug.LogWarning("[SignalProvider] XRHandSubsystem exists but is not running yet.");
                }
                return;
            }

            float dt = Time.deltaTime;

            if (provideLeftFingerDistance) UpdateLeftFingerDistance();
            if (provideLeftWristHeight)    UpdateLeftWristHeight(dt);

            if (provideRightFingerDistance) UpdateRightFingerDistance();
            if (provideRightWristHeight)    UpdateRightWristHeight(dt);

            if (debugLogs && Time.time >= _nextLog)
            {
                _nextLog = Time.time + Mathf.Max(0.1f, debugInterval);

                XRHand l = _hands.leftHand;
                XRHand r = _hands.rightHand;

                _signals.TryGetValue(InputSignal.LeftHand_FingerDistance01, out var lfd);
                _signals.TryGetValue(InputSignal.LeftHand_WristHeight01, out var lwh01);
                _signals.TryGetValue(InputSignal.LeftHand_WristAboveWaistCm, out var lwhCm);

                _signals.TryGetValue(InputSignal.RightHand_FingerDistance01, out var rfd);
                _signals.TryGetValue(InputSignal.RightHand_WristHeight01, out var rwh01);
                _signals.TryGetValue(InputSignal.RightHand_WristAboveHipCm, out var rwhCm);

                Debug.Log(
                    $"[SignalProvider] tracked(L={l.isTracked}, R={r.isTracked}) " +
                    $"LEFT: Finger01={lfd:F3} WristYcm={lwhCm:F1} Wrist01={lwh01:F3} | " +
                    $"RIGHT: Finger01={rfd:F3} WristCm={rwhCm:F1} Wrist01={rwh01:F3}"
                );
            }
            if (provideTorsoShoulderHeight) UpdateTorsoShoulderHeight(dt);
            if (provideTorsoLean) UpdateTorsoLean(dt); 
            if (provideTorsoYaw) UpdateTorsoYaw(dt);
            if (provideHeadPitch) UpdateHeadPitch(dt);
            if (provideLeftShoulderFlexion) UpdateLeftShoulderFlexion(dt);
            if (provideRightShoulderAbduction) UpdateRightShoulderAbduction(dt);

            if (provideLeftFingerSpread) UpdateLeftFingerSpread(dt);
            if (provideForearmDistances) UpdateForearmDistances(dt);
            if (provideRightHandHeight) UpdateRightHandHeight_FullBody(dt);    
            if (provideLocomotionDistance) UpdateLocomotionDistanceForwardOnly(dt);
        }

        public bool TryGetSignal(InputSignal signal, out float value) => _signals.TryGetValue(signal, out value);

        // -----------------------------
        // left
        // -----------------------------
        void UpdateLeftFingerDistance()
        {
            XRHand left = _hands.leftHand;
            if (!left.isTracked) return;

            if (!TryGetPoseSafe(left, XRHandJointID.IndexTip, out Pose indexTip)) return;
            if (!TryGetPoseSafe(left, XRHandJointID.MiddleTip, out Pose middleTip)) return;

            float cm = Vector3.Distance(indexTip.position, middleTip.position) * 100f;
            float clamped = Mathf.Clamp(cm, leftMinApertureCm, leftMaxApertureCm);
            float t = Mathf.InverseLerp(leftMinApertureCm, leftMaxApertureCm, clamped);
            if (leftFingerInvert) t = 1f - t;

            _signals[InputSignal.LeftHand_FingerDistance01] = Mathf.Clamp01(t);
        }

        void UpdateLeftWristHeight(float dt)
        {
            XRHand left = _hands.leftHand;
            if (!left.isTracked)
            {
                _signals[InputSignal.LeftHand_WristAboveWaistCm] = float.NaN;
                _signals[InputSignal.LeftHand_WristHeight01] = 0f;
                return;
            }

            if (!TryGetPoseSafe(left, XRHandJointID.Wrist, out Pose wristPose)) return;

            // speed gate
            if (_hasPrevLeft)
            {
                float speed = Vector3.Distance(wristPose.position, _prevLeftWristWorld) / Mathf.Max(dt, 0.0001f);
                if (speed > leftMaxWristSpeed)
                {
                    _prevLeftWristWorld = wristPose.position;
                    return;
                }
            }
            _prevLeftWristWorld = wristPose.position;
            _hasPrevLeft = true;

            Camera cam = EnsureCamera();
            float worldY_m = wristPose.position.y;

            // tracking-space y (only meaningful if trackingSpace transform is correct)
            Transform trackingSpace = null;
            if (xrOrigin != null && xrOrigin.Origin != null) trackingSpace = xrOrigin.Origin.transform;
            if (trackingSpace == null && xrOrigin != null) trackingSpace = xrOrigin.transform;

            float trackingY_m = (trackingSpace != null)
                ? trackingSpace.InverseTransformPoint(wristPose.position).y
                : worldY_m;

            float usedY_m;
            switch (leftWristYMode)
            {
                default:
                case WristYMode.WorldY:
                    usedY_m = worldY_m;
                    break;

                case WristYMode.HmdRelativeY:
                    if (cam == null) usedY_m = worldY_m;
                    else usedY_m = (worldY_m - cam.transform.position.y) + leftHmdRelativeYOffsetM;
                    break;

                case WristYMode.TrackingSpaceY:
                    usedY_m = trackingY_m;
                    break;
            }

            float heightCm = usedY_m * 100f;

            // debug
            _signals[InputSignal.LeftHand_WristAboveWaistCm] = heightCm;

            float minCm = Mathf.Min(leftMinWristYcm, leftMaxWristYcm);
            float maxCm = Mathf.Max(leftMinWristYcm, leftMaxWristYcm);

            float clamped = Mathf.Clamp(heightCm, minCm, maxCm);
            float t = Mathf.InverseLerp(minCm, maxCm, clamped);
            if (leftWristInvert) t = 1f - t;

            float target01 = Mathf.Clamp01(t);

            float before = _leftHeight01;
            _leftHeight01 = Filter01(
                seeded: ref _leftHeightSeeded,
                current: _leftHeight01,
                target: target01,
                dt: dt,
                deadZone01: leftHeightDeadZone01,
                hysteresis01: leftHeightHysteresis01,
                emaTau: leftHeightEmaTau,
                maxDeltaPerSec: leftHeightMaxDeltaPerSec
            );

            _signals[InputSignal.LeftHand_WristHeight01] = _leftHeight01;

            if (verboseDebug)
            {
                Debug.Log(
                    $"[SignalProvider][LEFT] mode={leftWristYMode} trackingSpace={(trackingSpace ? trackingSpace.name : "NULL")} " +
                    $"worldY={worldY_m:F3}m trackingY={trackingY_m:F3}m usedY={usedY_m:F3}m ({heightCm:F1}cm) " +
                    $"range=[{minCm:F1},{maxCm:F1}] clamp={clamped:F1} target01={target01:F3} filtered {before:F3}->{_leftHeight01:F3}"
                );
            }
        }

        // -----------------------------
        // right
        // -----------------------------
        void UpdateRightFingerDistance()
        {
            XRHand right = _hands.rightHand;
            if (!right.isTracked) return;

            if (!TryGetPoseSafe(right, XRHandJointID.ThumbTip, out Pose thumbTip)) return;
            if (!TryGetPoseSafe(right, XRHandJointID.IndexTip, out Pose indexTip)) return;

            float cm = Vector3.Distance(thumbTip.position, indexTip.position) * 100f;
            float clamped = Mathf.Clamp(cm, rightMinFingerDistanceCm, rightMaxFingerDistanceCm);
            float t = Mathf.InverseLerp(rightMinFingerDistanceCm, rightMaxFingerDistanceCm, clamped);
            if (rightFingerInvert) t = 1f - t;

            _signals[InputSignal.RightHand_FingerDistance01] = Mathf.Clamp01(t);
        }

        void UpdateRightWristHeight(float dt)
        {
            XRHand right = _hands.rightHand;
            if (!right.isTracked)
            {
                _signals[InputSignal.RightHand_WristAboveHipCm] = float.NaN;
                _signals[InputSignal.RightHand_WristHeight01] = 0f;
                return;
            }

            if (!TryGetPoseSafe(right, XRHandJointID.Wrist, out Pose wristPose)) return;

            // speed gate
            if (_hasPrevRight)
            {
                float speed = Vector3.Distance(wristPose.position, _prevRightWristWorld) / Mathf.Max(dt, 0.0001f);
                if (speed > rightMaxWristSpeed)
                {
                    _prevRightWristWorld = wristPose.position;
                    return;
                }
            }
            _prevRightWristWorld = wristPose.position;
            _hasPrevRight = true;

            Camera cam = EnsureCamera();
            float worldY_m = wristPose.position.y;

            // tracking-space y (only meaningful if trackingSpace transform is correct)
            Transform trackingSpace = null;
            if (xrOrigin != null && xrOrigin.Origin != null) trackingSpace = xrOrigin.Origin.transform;
            if (trackingSpace == null && xrOrigin != null) trackingSpace = xrOrigin.transform;

            float trackingY_m = (trackingSpace != null)
                ? trackingSpace.InverseTransformPoint(wristPose.position).y
                : worldY_m;

            float usedY_m;
            switch (rightWristYMode)
            {
                default:
                case WristYMode.WorldY:
                    usedY_m = worldY_m;
                    break;

                case WristYMode.HmdRelativeY:
                    if (cam == null) usedY_m = worldY_m;
                    else usedY_m = (worldY_m - cam.transform.position.y) + rightHmdRelativeYOffsetM;
                    break;

                case WristYMode.TrackingSpaceY:
                    usedY_m = trackingY_m;
                    break;
            }

            float heightCm = usedY_m * 100f;

            // debug
            _signals[InputSignal.RightHand_WristAboveHipCm] = heightCm;

            float minCm = Mathf.Min(rightMinWristYcm, rightMaxWristYcm);
            float maxCm = Mathf.Max(rightMinWristYcm, rightMaxWristYcm);

            float clamped = Mathf.Clamp(heightCm, minCm, maxCm);
            float t = Mathf.InverseLerp(minCm, maxCm, clamped);
            if (rightWristInvert) t = 1f - t;

            float target01 = Mathf.Clamp01(t);

            float before = _rightHeight01;
            _rightHeight01 = Filter01(
                seeded: ref _rightHeightSeeded,
                current: _rightHeight01,
                target: target01,
                dt: dt,
                deadZone01: rightHeightDeadZone01,
                hysteresis01: rightHeightHysteresis01,
                emaTau: rightHeightEmaTau,
                maxDeltaPerSec: rightHeightMaxDeltaPerSec
            );

            _signals[InputSignal.RightHand_WristHeight01] = _rightHeight01;

            if (verboseDebug)
            {
                Debug.Log(
                    $"[SignalProvider][RIGHT] mode={rightWristYMode} trackingSpace={(trackingSpace ? trackingSpace.name : "NULL")} " +
                    $"worldY={worldY_m:F3}m trackingY={trackingY_m:F3}m usedY={usedY_m:F3}m ({heightCm:F1}cm) " +
                    $"range=[{minCm:F1},{maxCm:F1}] clamp={clamped:F1} target01={target01:F3} filtered {before:F3}->{_rightHeight01:F3}"
                );
            }
        }

        void UpdateTorsoShoulderHeight(float dt)
        {
            Camera cam = EnsureCamera();
            if (!cam)
            {
                _signals[InputSignal.Torso_ShoulderYcm] = float.NaN;
                _signals[InputSignal.Torso_ShoulderHeight01] = 0f;
                return;
            }

            // Shoulder proxy point in world space: HMD + vertical offset
            Vector3 shoulderWorld = cam.transform.position + Vector3.up * shoulderYOffsetFromHead;

            float worldY_m = shoulderWorld.y;

            // tracking-space y (only meaningful if trackingSpace transform is correct)
            Transform trackingSpace = null;
            if (xrOrigin != null && xrOrigin.Origin != null) trackingSpace = xrOrigin.Origin.transform;
            if (trackingSpace == null && xrOrigin != null) trackingSpace = xrOrigin.transform;

            float trackingY_m = (trackingSpace != null)
                ? trackingSpace.InverseTransformPoint(shoulderWorld).y
                : worldY_m;

            float usedY_m;
            switch (torsoYMode)
            {
                default:
                case WristYMode.WorldY:
                    usedY_m = worldY_m;
                    break;

                case WristYMode.HmdRelativeY:
                    usedY_m = (worldY_m - cam.transform.position.y) + torsoHmdRelativeYOffsetM;
                    break;

                case WristYMode.TrackingSpaceY:
                    usedY_m = trackingY_m;
                    break;
            }

            float yUsed_m = usedY_m;

            // =========================
            // BASELINE MODE (use this for sitting posture)
            // =========================
            if (torsoUseBaseline)
            {
                if (!_torsoBaselineSeeded)
                {
                    _torsoBaselineSeeded = true;
                    _torsoBaselineY_m = yUsed_m + torsoBaselineBiasM;
                }

                float deltaCm = (yUsed_m - _torsoBaselineY_m) * 100f;

                // autorecalibrate if tracking/recenter caused a huge jump
                if (Mathf.Abs(deltaCm) > 30f) // threshold in cm (try 20–60)
                {
                    _torsoBaselineY_m = yUsed_m + torsoBaselineBiasM;
                    deltaCm = 0f; // prevent slamming to min/max this frame
                }
                // debug channel = posture delta in cm
                _signals[InputSignal.Torso_ShoulderYcm] = deltaCm;

                float minCm = Mathf.Min(torsoMinYcm, torsoMaxYcm);
                float maxCm = Mathf.Max(torsoMinYcm, torsoMaxYcm);

                float clamped = Mathf.Clamp(deltaCm, minCm, maxCm);
                float t = Mathf.InverseLerp(minCm, maxCm, clamped);
                if (torsoInvert) t = 1f - t;

                float target01 = Mathf.Clamp01(t);

                _torso01 = Filter01(
                    seeded: ref _torsoSeeded,
                    current: _torso01,
                    target: target01,
                    dt: dt,
                    deadZone01: torsoDeadZone01,
                    hysteresis01: torsoHysteresis01,
                    emaTau: torsoEmaTau,
                    maxDeltaPerSec: torsoMaxDeltaPerSec
                );

                _signals[InputSignal.Torso_ShoulderHeight01] = _torso01;

                if (verboseDebug)
                {
                    Debug.Log(
                        $"[SignalProvider][TORSO] BASELINE mode={torsoYMode} usedY={yUsed_m:F3}m baseline={_torsoBaselineY_m:F3}m " +
                        $"deltaCm={deltaCm:F1} range=[{minCm:F1},{maxCm:F1}] clamp={clamped:F1} target01={target01:F3} filtered={_torso01:F3}"
                    );
                }

                return;
            }

            // =========================
            // Fallback: absolute cm
            // =========================
            float yCm_abs = yUsed_m * 100f;

            _signals[InputSignal.Torso_ShoulderYcm] = yCm_abs;

            float minCm_abs = Mathf.Min(torsoMinYcm, torsoMaxYcm);
            float maxCm_abs = Mathf.Max(torsoMinYcm, torsoMaxYcm);

            float clamped_abs = Mathf.Clamp(yCm_abs, minCm_abs, maxCm_abs);
            float t_abs = Mathf.InverseLerp(minCm_abs, maxCm_abs, clamped_abs);
            if (torsoInvert) t_abs = 1f - t_abs;

            float target01_abs = Mathf.Clamp01(t_abs);

            _torso01 = Filter01(
                seeded: ref _torsoSeeded,
                current: _torso01,
                target: target01_abs,
                dt: dt,
                deadZone01: torsoDeadZone01,
                hysteresis01: torsoHysteresis01,
                emaTau: torsoEmaTau,
                maxDeltaPerSec: torsoMaxDeltaPerSec
            );

            _signals[InputSignal.Torso_ShoulderHeight01] = _torso01;

            if (verboseDebug)
            {
                Debug.Log(
                    $"[SignalProvider][TORSO] ABS mode={torsoYMode} worldY={worldY_m:F3}m usedY={usedY_m:F3}m ({yCm_abs:F1}cm) " +
                    $"range=[{minCm_abs:F1},{maxCm_abs:F1}] clamp={clamped_abs:F1} target01={target01_abs:F3} filtered={_torso01:F3}"
                );
            }
        }

        void UpdateTorsoLean(float dt)
        {
            Camera cam = EnsureCamera();
            if (!cam)
            {
                _signals[InputSignal.Torso_LeanDeg] = float.NaN;
                _signals[InputSignal.Torso_Lean01] = 0f;
                return;
            }

            Vector3 f = cam.transform.forward;

            float pitchDeg = Mathf.Atan2(f.y, new Vector2(f.x, f.z).magnitude) * Mathf.Rad2Deg;

            // =========================
            // BASELINE MODE (sitting normally)
            // =========================
            if (torsoLeanUseBaseline)
            {
                if (!_torsoLeanBaselineSeeded)
                {
                    _torsoLeanBaselineSeeded = true;
                    _torsoLeanBaselineDeg = pitchDeg + torsoLeanBaselineBiasDeg;
                }

                float deltaDeg = pitchDeg - _torsoLeanBaselineDeg;

                // Auto-recalibrate on big jump (recenter / tracking change)
                if (Mathf.Abs(deltaDeg) > 45f)
                {
                    _torsoLeanBaselineDeg = pitchDeg + torsoLeanBaselineBiasDeg;
                    deltaDeg = 0f;
                }

                _signals[InputSignal.Torso_LeanDeg] = deltaDeg;

                float minD = Mathf.Min(torsoLeanMinDeg, torsoLeanMaxDeg);
                float maxD = Mathf.Max(torsoLeanMinDeg, torsoLeanMaxDeg);

                float clamped = Mathf.Clamp(deltaDeg, minD, maxD);
                float t = Mathf.InverseLerp(minD, maxD, clamped);
                if (torsoLeanInvert) t = 1f - t;

                float target01 = Mathf.Clamp01(t);

                _torsoLean01 = Filter01(
                    seeded: ref _torsoLeanSeeded,
                    current: _torsoLean01,
                    target: target01,
                    dt: dt,
                    deadZone01: torsoLeanDeadZone01,
                    hysteresis01: torsoLeanHysteresis01,
                    emaTau: torsoLeanEmaTau,
                    maxDeltaPerSec: torsoLeanMaxDeltaPerSec
                );

                _signals[InputSignal.Torso_Lean01] = _torsoLean01;

                if (verboseDebug)
                {
                    Debug.Log(
                        $"[SignalProvider][LEAN] pitch={pitchDeg:F1}° baseline={_torsoLeanBaselineDeg:F1}° " +
                        $"delta={deltaDeg:F1}° range=[{minD:F1},{maxD:F1}] clamp={clamped:F1} target01={target01:F3} filtered={_torsoLean01:F3}"
                    );
                }

                return;
            }

            // Fallback: absolute pitch mapping (rare)
            _signals[InputSignal.Torso_LeanDeg] = pitchDeg;

            float minAbs = Mathf.Min(torsoLeanMinDeg, torsoLeanMaxDeg);
            float maxAbs = Mathf.Max(torsoLeanMinDeg, torsoLeanMaxDeg);

            float clampedAbs = Mathf.Clamp(pitchDeg, minAbs, maxAbs);
            float tAbs = Mathf.InverseLerp(minAbs, maxAbs, clampedAbs);
            if (torsoLeanInvert) tAbs = 1f - tAbs;

            float target01Abs = Mathf.Clamp01(tAbs);

            _torsoLean01 = Filter01(
                seeded: ref _torsoLeanSeeded,
                current: _torsoLean01,
                target: target01Abs,
                dt: dt,
                deadZone01: torsoLeanDeadZone01,
                hysteresis01: torsoLeanHysteresis01,
                emaTau: torsoLeanEmaTau,
                maxDeltaPerSec: torsoLeanMaxDeltaPerSec
            );

            _signals[InputSignal.Torso_Lean01] = _torsoLean01;
        }

        void UpdateTorsoYaw(float dt)
        {
            Camera cam = EnsureCamera();
            if (!cam)
            {
                _signals[InputSignal.Torso_YawDeg] = float.NaN;
                _signals[InputSignal.Torso_Yaw01] = 0f;
                return;
            }

            // Use HMD forward projected onto XZ plane to get yaw angle
            Vector3 f = cam.transform.forward;
            f.y = 0f;

            if (f.sqrMagnitude < 1e-6f)
            {
                _signals[InputSignal.Torso_YawDeg] = 0f;
                _signals[InputSignal.Torso_Yaw01] = _torsoYaw01;
                return;
            }

            f.Normalize();

            // Yaw in degrees: atan2(x, z) gives [-180, 180]
            float yawDeg = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;

            if (torsoYawUseBaseline)
            {
                if (!_torsoYawBaselineSeeded)
                {
                    _torsoYawBaselineSeeded = true;
                    _torsoYawBaselineDeg = yawDeg + torsoYawBaselineBiasDeg;
                }

                float deltaDeg = Mathf.DeltaAngle(_torsoYawBaselineDeg, yawDeg);

                // auto-recalibrate on huge jump (recentering/tracking)
                if (Mathf.Abs(deltaDeg) > 120f)
                {
                    _torsoYawBaselineDeg = yawDeg + torsoYawBaselineBiasDeg;
                    deltaDeg = 0f;
                }

                _signals[InputSignal.Torso_YawDeg] = deltaDeg;

                float minD = Mathf.Min(torsoYawMinDeg, torsoYawMaxDeg);
                float maxD = Mathf.Max(torsoYawMinDeg, torsoYawMaxDeg);

                float clamped = Mathf.Clamp(deltaDeg, minD, maxD);
                float t = Mathf.InverseLerp(minD, maxD, clamped);
                if (torsoYawInvert) t = 1f - t;

                float target01 = Mathf.Clamp01(t);

                _torsoYaw01 = Filter01(
                    seeded: ref _torsoYawSeeded,
                    current: _torsoYaw01,
                    target: target01,
                    dt: dt,
                    deadZone01: torsoYawDeadZone01,
                    hysteresis01: torsoYawHysteresis01,
                    emaTau: torsoYawEmaTau,
                    maxDeltaPerSec: torsoYawMaxDeltaPerSec
                );

                _signals[InputSignal.Torso_Yaw01] = _torsoYaw01;
                return;
            }

            // Fallback: absolute yaw mapping (rare)
            _signals[InputSignal.Torso_YawDeg] = yawDeg;

            float minAbs = Mathf.Min(torsoYawMinDeg, torsoYawMaxDeg);
            float maxAbs = Mathf.Max(torsoYawMinDeg, torsoYawMaxDeg);

            float clampedAbs = Mathf.Clamp(yawDeg, minAbs, maxAbs);
            float tAbs = Mathf.InverseLerp(minAbs, maxAbs, clampedAbs);
            if (torsoYawInvert) tAbs = 1f - tAbs;

            float target01Abs = Mathf.Clamp01(tAbs);

            _torsoYaw01 = Filter01(
                seeded: ref _torsoYawSeeded,
                current: _torsoYaw01,
                target: target01Abs,
                dt: dt,
                deadZone01: torsoYawDeadZone01,
                hysteresis01: torsoYawHysteresis01,
                emaTau: torsoYawEmaTau,
                maxDeltaPerSec: torsoYawMaxDeltaPerSec
            );

            _signals[InputSignal.Torso_Yaw01] = _torsoYaw01;
        }

        public void RecalibrateTorsoBaseline()
        {
            _torsoBaselineSeeded = false;
            _torsoSeeded = false;  
            if (debugLogs) Debug.Log("[SignalProvider][TORSO] Baseline recalibrated.");
        }

        void UpdateHeadPitch(float dt)
        {
            Camera cam = EnsureCamera();
            if (!cam)
            {
                _signals[InputSignal.Head_PitchDeg] = float.NaN;
                _signals[InputSignal.Head_Pitch01] = 0f;
                return;
            }

            // Pitch from HMD forward vector
            Vector3 f = cam.transform.forward;
            float pitchDeg = Mathf.Atan2(f.y, new Vector2(f.x, f.z).magnitude) * Mathf.Rad2Deg;

            if (headPitchUseBaseline)
            {
                if (!_headPitchBaselineSeeded)
                {
                    _headPitchBaselineSeeded = true;
                    _headPitchBaselineDeg = pitchDeg + headPitchBaselineBiasDeg;
                }

                float deltaDeg = pitchDeg - _headPitchBaselineDeg;

                // auto-recalibrate on huge jump (recenter/tracking)
                if (Mathf.Abs(deltaDeg) > 60f)
                {
                    _headPitchBaselineDeg = pitchDeg + headPitchBaselineBiasDeg;
                    deltaDeg = 0f;
                }

                _signals[InputSignal.Head_PitchDeg] = deltaDeg;

                float minD = Mathf.Min(headPitchMinDeg, headPitchMaxDeg);
                float maxD = Mathf.Max(headPitchMinDeg, headPitchMaxDeg);

                float clamped = Mathf.Clamp(deltaDeg, minD, maxD);
                float t = Mathf.InverseLerp(minD, maxD, clamped);
                if (headPitchInvert) t = 1f - t;

                float target01 = Mathf.Clamp01(t);

                _headPitch01 = Filter01(
                    seeded: ref _headPitchSeeded,
                    current: _headPitch01,
                    target: target01,
                    dt: dt,
                    deadZone01: headPitchDeadZone01,
                    hysteresis01: headPitchHysteresis01,
                    emaTau: headPitchEmaTau,
                    maxDeltaPerSec: headPitchMaxDeltaPerSec
                );

                _signals[InputSignal.Head_Pitch01] = _headPitch01;
                return;
            }

            // Fallback: absolute pitch mapping (rare)
            _signals[InputSignal.Head_PitchDeg] = pitchDeg;

            float minAbs = Mathf.Min(headPitchMinDeg, headPitchMaxDeg);
            float maxAbs = Mathf.Max(headPitchMinDeg, headPitchMaxDeg);

            float clampedAbs = Mathf.Clamp(pitchDeg, minAbs, maxAbs);
            float tAbs = Mathf.InverseLerp(minAbs, maxAbs, clampedAbs);
            if (headPitchInvert) tAbs = 1f - tAbs;

            float target01Abs = Mathf.Clamp01(tAbs);

            _headPitch01 = Filter01(
                seeded: ref _headPitchSeeded,
                current: _headPitch01,
                target: target01Abs,
                dt: dt,
                deadZone01: headPitchDeadZone01,
                hysteresis01: headPitchHysteresis01,
                emaTau: headPitchEmaTau,
                maxDeltaPerSec: headPitchMaxDeltaPerSec
            );

            _signals[InputSignal.Head_Pitch01] = _headPitch01;
        }

       void UpdateLeftShoulderFlexion(float dt)
        {
            if (_hands == null || !_hands.running) return;

            XRHand left = _hands.leftHand;
            if (!left.isTracked)
            {
                _signals[InputSignal.LeftArm_ShoulderFlexDeg] = float.NaN;
                _signals[InputSignal.LeftArm_ShoulderFlex01] = 0f;
                return;
            }

            if (!TryGetPoseSafe(left, XRHandJointID.Wrist, out Pose wristPose))
                return;

            Camera cam = EnsureCamera();
            if (!cam)
            {
                _signals[InputSignal.LeftArm_ShoulderFlexDeg] = float.NaN;
                _signals[InputSignal.LeftArm_ShoulderFlex01] = 0f;
                return;
            }

          
            Transform trackingSpace = null;
            if (xrOrigin != null && xrOrigin.Origin != null) trackingSpace = xrOrigin.Origin.transform;
            if (trackingSpace == null && xrOrigin != null) trackingSpace = xrOrigin.transform;

            Vector3 wristWorld = (trackingSpace != null)
                ? trackingSpace.TransformPoint(wristPose.position)
                : wristPose.position;

            // Shoulder proxy point in WORLD space: HMD + vertical offset
            Vector3 shoulderWorld = cam.transform.position + Vector3.up * shoulderYOffsetFromHead;

            // Vector from shoulder to wrist (WORLD)
            Vector3 armWorld = wristWorld - shoulderWorld;
            if (armWorld.sqrMagnitude < 1e-6f)
            {
                _signals[InputSignal.LeftArm_ShoulderFlexDeg] = 0f;
                _signals[InputSignal.LeftArm_ShoulderFlex01] = _leftShoulderFlex01;
                return;
            }

      
            Vector3 bodyFwd = cam.transform.forward; bodyFwd.y = 0f;
            if (bodyFwd.sqrMagnitude < 1e-6f)
            {
                _signals[InputSignal.LeftArm_ShoulderFlexDeg] = 0f;
                _signals[InputSignal.LeftArm_ShoulderFlex01] = _leftShoulderFlex01;
                return;
            }
            bodyFwd.Normalize();

            Vector3 bodyRight = Vector3.Cross(Vector3.up, bodyFwd); // yaw-only right
            bodyRight.Normalize();

            // Remove sideways component to stay in sagittal plane
            Vector3 armSag = Vector3.ProjectOnPlane(armWorld, bodyRight);
            if (armSag.sqrMagnitude < 1e-6f)
            {
                _signals[InputSignal.LeftArm_ShoulderFlexDeg] = 0f;
                _signals[InputSignal.LeftArm_ShoulderFlex01] = _leftShoulderFlex01;
                return;
            }
            armSag.Normalize();

            // SignedAngle from "down" (-up) to armSag about bodyRight:
            // ~0 when arm down, + when arm raises forward/up, - when arm goes backward
            float flexDeg = Vector3.SignedAngle(-Vector3.up, armSag, bodyRight);
            if (float.IsNaN(flexDeg) || float.IsInfinity(flexDeg)) flexDeg = 0f;

            // ---------------------------------------------------------
            // Baseline: "arm down at side"
            // ---------------------------------------------------------
            if (leftShoulderFlexUseBaseline)
            {
                if (!_leftShoulderFlexBaselineSeeded)
                {
                    _leftShoulderFlexBaselineSeeded = true;
                    _leftShoulderFlexBaselineDeg = flexDeg + leftShoulderFlexBaselineBiasDeg;
                }

                float deltaDeg = flexDeg - _leftShoulderFlexBaselineDeg;

                // Auto-recalibrate on huge jump (recenter/tracking change)
                if (Mathf.Abs(deltaDeg) > 120f)
                {
                    _leftShoulderFlexBaselineDeg = flexDeg + leftShoulderFlexBaselineBiasDeg;
                    deltaDeg = 0f;
                }

                _signals[InputSignal.LeftArm_ShoulderFlexDeg] = deltaDeg;

                float minD = Mathf.Min(leftShoulderFlexMinDeg, leftShoulderFlexMaxDeg);
                float maxD = Mathf.Max(leftShoulderFlexMinDeg, leftShoulderFlexMaxDeg);

                float clamped = Mathf.Clamp(deltaDeg, minD, maxD);
                float t = Mathf.InverseLerp(minD, maxD, clamped);
                if (leftShoulderFlexInvert) t = 1f - t;

                float target01 = Mathf.Clamp01(t);

                _leftShoulderFlex01 = Filter01(
                    seeded: ref _leftShoulderFlexSeeded,
                    current: _leftShoulderFlex01,
                    target: target01,
                    dt: dt,
                    deadZone01: leftShoulderFlexDeadZone01,
                    hysteresis01: leftShoulderFlexHysteresis01,
                    emaTau: leftShoulderFlexEmaTau,
                    maxDeltaPerSec: leftShoulderFlexMaxDeltaPerSec
                );

                _signals[InputSignal.LeftArm_ShoulderFlex01] = _leftShoulderFlex01;

                if (verboseDebug)
                {
                    Debug.Log(
                        $"[LShoulderFlex][OBJ={name}] ts={(trackingSpace ? trackingSpace.name : "NULL")} " +
                        $"wristRaw={wristPose.position:F3} wristWorld={wristWorld:F3} shoulderWorld={shoulderWorld:F3} " +
                        $"flexDeg={flexDeg:F1} base={_leftShoulderFlexBaselineDeg:F1} delta={deltaDeg:F1} out01={_leftShoulderFlex01:F3}"
                    );
                }

                return;
            }

            // Fallback: absolute mapping
            _signals[InputSignal.LeftArm_ShoulderFlexDeg] = flexDeg;

            float minAbs = Mathf.Min(leftShoulderFlexMinDeg, leftShoulderFlexMaxDeg);
            float maxAbs = Mathf.Max(leftShoulderFlexMinDeg, leftShoulderFlexMaxDeg);

            float clampedAbs = Mathf.Clamp(flexDeg, minAbs, maxAbs);
            float tAbs = Mathf.InverseLerp(minAbs, maxAbs, clampedAbs);
            if (leftShoulderFlexInvert) tAbs = 1f - tAbs;

            float target01Abs = Mathf.Clamp01(tAbs);

            _leftShoulderFlex01 = Filter01(
                seeded: ref _leftShoulderFlexSeeded,
                current: _leftShoulderFlex01,
                target: target01Abs,
                dt: dt,
                deadZone01: leftShoulderFlexDeadZone01,
                hysteresis01: leftShoulderFlexHysteresis01,
                emaTau: leftShoulderFlexEmaTau,
                maxDeltaPerSec: leftShoulderFlexMaxDeltaPerSec
            );

            _signals[InputSignal.LeftArm_ShoulderFlex01] = _leftShoulderFlex01;

            if (verboseDebug)
            {
                Debug.Log(
                    $"[LShoulderFlex][OBJ={name}] ABS ts={(trackingSpace ? trackingSpace.name : "NULL")} " +
                    $"wristRaw={wristPose.position:F3} wristWorld={wristWorld:F3} shoulderWorld={shoulderWorld:F3} " +
                    $"flexDeg={flexDeg:F1} out01={_leftShoulderFlex01:F3}"
                );
            }
        }

        void UpdateRightShoulderAbduction(float dt)
        {
            if (_hands == null || !_hands.running) return;

            XRHand rightHand = _hands.rightHand;
            if (!rightHand.isTracked)
            {
                _signals[InputSignal.RightArm_ShoulderAbdDeg] = float.NaN;
                _signals[InputSignal.RightArm_ShoulderAbd01] = 0f;
                return;
            }

            if (!TryGetPoseSafe(rightHand, XRHandJointID.Wrist, out Pose wristPose))
                return;

            Camera cam = EnsureCamera();
            if (!cam)
            {
                _signals[InputSignal.RightArm_ShoulderAbdDeg] = float.NaN;
                _signals[InputSignal.RightArm_ShoulderAbd01] = 0f;
                return;
            }

            // ---------------------------------------------------------
            // wrist pose -> world using XR Origin tracking space
            // ---------------------------------------------------------
            Transform trackingSpace = null;
            if (xrOrigin != null && xrOrigin.Origin != null) trackingSpace = xrOrigin.Origin.transform;
            if (trackingSpace == null && xrOrigin != null) trackingSpace = xrOrigin.transform;

            Vector3 wristWorld = (trackingSpace != null)
                ? trackingSpace.TransformPoint(wristPose.position)
                : wristPose.position;

         
            Vector3 shoulderWorld = cam.transform.position + Vector3.up * shoulderYOffsetFromHead;

            Vector3 armWorld = wristWorld - shoulderWorld;
            if (armWorld.sqrMagnitude < 1e-6f)
            {
                _signals[InputSignal.RightArm_ShoulderAbdDeg] = 0f;
                _signals[InputSignal.RightArm_ShoulderAbd01] = _rightShoulderAbd01;
                return;
            }

          
            Vector3 bodyFwd = cam.transform.forward; bodyFwd.y = 0f;
            if (bodyFwd.sqrMagnitude < 1e-6f)
            {
                _signals[InputSignal.RightArm_ShoulderAbdDeg] = 0f;
                _signals[InputSignal.RightArm_ShoulderAbd01] = _rightShoulderAbd01;
                return;
            }
            bodyFwd.Normalize();

            Vector3 bodyRight = Vector3.Cross(Vector3.up, bodyFwd);
            bodyRight.Normalize();

            // Project onto frontal plane (defined by Up + Right): remove forward component
            Vector3 armFront = Vector3.ProjectOnPlane(armWorld, bodyFwd);
            if (armFront.sqrMagnitude < 1e-6f)
            {
                _signals[InputSignal.RightArm_ShoulderAbdDeg] = 0f;
                _signals[InputSignal.RightArm_ShoulderAbd01] = _rightShoulderAbd01;
                return;
            }
            armFront.Normalize();

            // SignedAngle from down (-up) to armFront around bodyFwd:
            // + when moving toward bodyRight (arm out to the right), - toward left
            float abdDegSigned = Vector3.SignedAngle(-Vector3.up, armFront, bodyFwd);


            if (Mathf.Abs(abdDegSigned) < 2.0f) abdDegSigned = 0f;
            // For right arm abduction, we usually want magnitude in the "right" direction.
            // Clamp negative (crossing body) to 0 so it doesn't go weird.
            float abdDeg = Mathf.Max(0f, abdDegSigned);

            if (float.IsNaN(abdDeg) || float.IsInfinity(abdDeg)) abdDeg = 0f;

            // ---------------------------------------------------------
            // Baseline: "right arm down at side"
            // ---------------------------------------------------------
            if (rightShoulderAbdUseBaseline)
            {
                if (!_rightShoulderAbdBaselineSeeded)
                {
                    _rightShoulderAbdBaselineSeeded = true;
                    _rightShoulderAbdBaselineDeg = abdDeg + rightShoulderAbdBaselineBiasDeg;
                }

                float deltaDeg = abdDeg - _rightShoulderAbdBaselineDeg;

                // Auto-recalibrate on huge jump (recenter/tracking change)
                if (Mathf.Abs(deltaDeg) > 120f)
                {
                    _rightShoulderAbdBaselineDeg = abdDeg + rightShoulderAbdBaselineBiasDeg;
                    deltaDeg = 0f;
                }

                // Keep within [0..] so “arm down” is near 0
                deltaDeg = Mathf.Max(0f, deltaDeg);

                _signals[InputSignal.RightArm_ShoulderAbdDeg] = deltaDeg;

                float minD = Mathf.Min(rightShoulderAbdMinDeg, rightShoulderAbdMaxDeg);
                float maxD = Mathf.Max(rightShoulderAbdMinDeg, rightShoulderAbdMaxDeg);

                float clamped = Mathf.Clamp(deltaDeg, minD, maxD);
                float t = Mathf.InverseLerp(minD, maxD, clamped);
                if (rightShoulderAbdInvert) t = 1f - t;

                float target01 = Mathf.Clamp01(t);

                _rightShoulderAbd01 = Filter01(
                    seeded: ref _rightShoulderAbdSeeded,
                    current: _rightShoulderAbd01,
                    target: target01,
                    dt: dt,
                    deadZone01: rightShoulderAbdDeadZone01,
                    hysteresis01: rightShoulderAbdHysteresis01,
                    emaTau: rightShoulderAbdEmaTau,
                    maxDeltaPerSec: rightShoulderAbdMaxDeltaPerSec
                );

                _signals[InputSignal.RightArm_ShoulderAbd01] = _rightShoulderAbd01;

                if (verboseDebug)
                {
                    Debug.Log(
                        $"[RShoulderAbd][OBJ={name}] ts={(trackingSpace ? trackingSpace.name : "NULL")} " +
                        $"wristRaw={wristPose.position:F3} wristWorld={wristWorld:F3} shoulderWorld={shoulderWorld:F3} " +
                        $"abdSigned={abdDegSigned:F1} abdDeg={abdDeg:F1} base={_rightShoulderAbdBaselineDeg:F1} delta={deltaDeg:F1} out01={_rightShoulderAbd01:F3}"
                    );
                }

                return;
            }

            // Fallback: absolute mapping (rare)
            _signals[InputSignal.RightArm_ShoulderAbdDeg] = abdDeg;

            float minAbs = Mathf.Min(rightShoulderAbdMinDeg, rightShoulderAbdMaxDeg);
            float maxAbs = Mathf.Max(rightShoulderAbdMinDeg, rightShoulderAbdMaxDeg);

            float clampedAbs = Mathf.Clamp(abdDeg, minAbs, maxAbs);
            float tAbs = Mathf.InverseLerp(minAbs, maxAbs, clampedAbs);
            if (rightShoulderAbdInvert) tAbs = 1f - tAbs;

            float target01Abs = Mathf.Clamp01(tAbs);

            _rightShoulderAbd01 = Filter01(
                seeded: ref _rightShoulderAbdSeeded,
                current: _rightShoulderAbd01,
                target: target01Abs,
                dt: dt,
                deadZone01: rightShoulderAbdDeadZone01,
                hysteresis01: rightShoulderAbdHysteresis01,
                emaTau: rightShoulderAbdEmaTau,
                maxDeltaPerSec: rightShoulderAbdMaxDeltaPerSec
            );

            _signals[InputSignal.RightArm_ShoulderAbd01] = _rightShoulderAbd01;
        }

        void UpdateLeftFingerSpread(float dt)
        {
            if (_hands == null || !_hands.running) return;

            XRHand left = _hands.leftHand;
            if (!left.isTracked)
            {
                _signals[InputSignal.LeftHand_FingerSpreadCm] = float.NaN;
                _signals[InputSignal.LeftHand_FingerSpread01] = 0f;
                return;
            }

            // We measure spread as average fingertip distance from palm center.
            if (!TryGetPoseSafe(left, XRHandJointID.Palm, out Pose palmPose))
                return;

            // Convert tracking-space pose to world
            Transform trackingSpace = null;
            if (xrOrigin != null && xrOrigin.Origin != null)
                trackingSpace = xrOrigin.Origin.transform;
            if (trackingSpace == null && xrOrigin != null)
                trackingSpace = xrOrigin.transform;

            Vector3 palmWorld = (trackingSpace != null)
                ? trackingSpace.TransformPoint(palmPose.position)
                : palmPose.position;

            // Collect fingertip joints
            XRHandJointID[] tips =
            {
                XRHandJointID.ThumbTip,
                XRHandJointID.IndexTip,
                XRHandJointID.MiddleTip,
                XRHandJointID.RingTip,
                XRHandJointID.LittleTip
            };

            float sum = 0f;
            int count = 0;

            foreach (var tipID in tips)
            {
                if (!TryGetPoseSafe(left, tipID, out Pose tipPose))
                    continue;

                Vector3 tipWorld = (trackingSpace != null)
                    ? trackingSpace.TransformPoint(tipPose.position)
                    : tipPose.position;

                float distCm = Vector3.Distance(palmWorld, tipWorld) * 100f;
                sum += distCm;
                count++;
            }

            if (count == 0)
                return;

            float avgCm = sum / count;

            // debug
            _signals[InputSignal.LeftHand_FingerSpreadCm] = avgCm;

            // Clamp to calibrated range
            float minCm = Mathf.Min(leftSpreadMinCm, leftSpreadMaxCm);
            float maxCm = Mathf.Max(leftSpreadMinCm, leftSpreadMaxCm);

            float clamped = Mathf.Clamp(avgCm, minCm, maxCm);
            float t = Mathf.InverseLerp(minCm, maxCm, clamped);

            if (leftSpreadInvert)
                t = 1f - t;

            float target01 = Mathf.Clamp01(t);

            _leftSpread01 = Filter01(
                seeded: ref _leftSpreadSeeded,
                current: _leftSpread01,
                target: target01,
                dt: dt,
                deadZone01: leftSpreadDeadZone01,
                hysteresis01: leftSpreadHysteresis01,
                emaTau: leftSpreadEmaTau,
                maxDeltaPerSec: leftSpreadMaxDeltaPerSec
            );

            _signals[InputSignal.LeftHand_FingerSpread01] = _leftSpread01;

            if (verboseDebug)
            {
                Debug.Log(
                    $"[LSpread] avgCm={avgCm:F2} range=[{minCm:F1},{maxCm:F1}] " +
                    $"clamp={clamped:F2} out01={_leftSpread01:F3}"
                );
            }
        }

        void UpdateForearmDistances(float dt)
        {
            if (_hands == null || !_hands.running) return;

            XRHand l = _hands.leftHand;
            XRHand r = _hands.rightHand;

            if (!l.isTracked || !r.isTracked)
            {
                _signals[InputSignal.Forearms_VerticalDistanceCm] = float.NaN;
                _signals[InputSignal.Forearms_VerticalDistance01] = 0f;

                _signals[InputSignal.Forearms_HorizontalDistanceCm] = float.NaN;
                _signals[InputSignal.Forearms_HorizontalDistance01] = 0f;
                return;
            }

            if (!TryGetPoseSafe(l, XRHandJointID.Wrist, out Pose lWrist)) return;
            if (!TryGetPoseSafe(r, XRHandJointID.Wrist, out Pose rWrist)) return;

            // Convert tracking-space → world
            Transform trackingSpace = null;
            if (xrOrigin != null && xrOrigin.Origin != null) trackingSpace = xrOrigin.Origin.transform;
            if (trackingSpace == null && xrOrigin != null) trackingSpace = xrOrigin.transform;

            Vector3 lWorld = (trackingSpace != null) ? trackingSpace.TransformPoint(lWrist.position) : lWrist.position;
            Vector3 rWorld = (trackingSpace != null) ? trackingSpace.TransformPoint(rWrist.position) : rWrist.position;

            // =========================
            // 1️Vertical Distance (Height control)
            // =========================
            float vertCm = Mathf.Abs(lWorld.y - rWorld.y) * 100f;
            _signals[InputSignal.Forearms_VerticalDistanceCm] = vertCm;

            float vMin = Mathf.Min(forearmsVertMinCm, forearmsVertMaxCm);
            float vMax = Mathf.Max(forearmsVertMinCm, forearmsVertMaxCm);

            float vClamped = Mathf.Clamp(vertCm, vMin, vMax);
            float vT = Mathf.InverseLerp(vMin, vMax, vClamped);
            if (forearmsVertInvert) vT = 1f - vT;

            float vTarget = Mathf.Clamp01(vT);

            _forearmsV01 = Filter01(
                seeded: ref _forearmsVSeeded,
                current: _forearmsV01,
                target: vTarget,
                dt: dt,
                deadZone01: forearmsVertDeadZone01,
                hysteresis01: forearmsVertHysteresis01,
                emaTau: forearmsVertEmaTau,
                maxDeltaPerSec: forearmsVertMaxDeltaPerSec
            );

            _signals[InputSignal.Forearms_VerticalDistance01] = _forearmsV01;


            // =========================
            // 2️Horizontal Distance (Width control)
            // =========================
            Vector3 lXZ = new Vector3(lWorld.x, 0f, lWorld.z);
            Vector3 rXZ = new Vector3(rWorld.x, 0f, rWorld.z);

            float horizCm = Vector3.Distance(lXZ, rXZ) * 100f;
            _signals[InputSignal.Forearms_HorizontalDistanceCm] = horizCm;

            float hMin = Mathf.Min(forearmsHorizMinCm, forearmsHorizMaxCm);
            float hMax = Mathf.Max(forearmsHorizMinCm, forearmsHorizMaxCm);

            float hClamped = Mathf.Clamp(horizCm, hMin, hMax);
            float hT = Mathf.InverseLerp(hMin, hMax, hClamped);
            if (forearmsHorizInvert) hT = 1f - hT;

            float hTarget = Mathf.Clamp01(hT);

            _forearmsH01 = Filter01(
                seeded: ref _forearmsHSeeded,
                current: _forearmsH01,
                target: hTarget,
                dt: dt,
                deadZone01: forearmsHorizDeadZone01,
                hysteresis01: forearmsHorizHysteresis01,
                emaTau: forearmsHorizEmaTau,
                maxDeltaPerSec: forearmsHorizMaxDeltaPerSec
            );

            _signals[InputSignal.Forearms_HorizontalDistance01] = _forearmsH01;

            if (verboseDebug)
            {
                Debug.Log(
                    $"[Forearms] V={vertCm:F1}cm→{_forearmsV01:F3} | H={horizCm:F1}cm→{_forearmsH01:F3}"
                );
            }
        }


        void UpdateRightHandHeight_FullBody(float dt)
        {
            if (_hands == null || !_hands.running) return;

            XRHand right = _hands.rightHand;
            if (!right.isTracked)
            {
                _signals[InputSignal.RightHand_HandHeightYcm] = float.NaN;
                _signals[InputSignal.RightHand_HandHeight01] = 0f;
                return;
            }

            if (!TryGetPoseSafe(right, XRHandJointID.Wrist, out Pose wristPose))
                return;

            // Full-body: use tracking-space Y (height above origin/floor)
            Transform trackingSpace = null;
            if (xrOrigin != null && xrOrigin.Origin != null) trackingSpace = xrOrigin.Origin.transform;
            if (trackingSpace == null && xrOrigin != null) trackingSpace = xrOrigin.transform;

            // Convert wrist pose -> world, then measure tracking-local Y
            Vector3 wristWorld = (trackingSpace != null)
                ? trackingSpace.TransformPoint(wristPose.position)
                : wristPose.position;

            float yLocal_m = (trackingSpace != null)
                ? trackingSpace.InverseTransformPoint(wristWorld).y
                : wristWorld.y;

            float yCm = yLocal_m * 100f;

            // Debug raw cm
            _signals[InputSignal.RightHand_HandHeightYcm] = yCm;

            float minCm = Mathf.Min(rightHandMinYcm, rightHandMaxYcm);
            float maxCm = Mathf.Max(rightHandMinYcm, rightHandMaxYcm);

            float clamped = Mathf.Clamp(yCm, minCm, maxCm);
            float t = Mathf.InverseLerp(minCm, maxCm, clamped);
            if (rightHandHeightInvert) t = 1f - t;

            float target01 = Mathf.Clamp01(t);

            _rightHandHeight01_2 = Filter01(
                seeded: ref _rightHandHeightSeeded2,
                current: _rightHandHeight01_2,
                target: target01,
                dt: dt,
                deadZone01: rightHandHeightDeadZone01,
                hysteresis01: rightHandHeightHysteresis01,
                emaTau: rightHandHeightEmaTau,
                maxDeltaPerSec: rightHandHeightMaxDeltaPerSec
            );

            _signals[InputSignal.RightHand_HandHeight01] = _rightHandHeight01_2;

            if (verboseDebug)
            {
                Debug.Log(
                    $"[RightHandHeight] yCm={yCm:F1} range=[{minCm:F1},{maxCm:F1}] clamp={clamped:F1} out01={_rightHandHeight01_2:F3}"
                );
            }
        }

        void UpdateLocomotionDistanceForwardOnly(float dt)
        {
            Camera cam = EnsureCamera();
            if (!cam)
            {
                _signals[InputSignal.Locomotion_DistanceFromStartCm] = float.NaN;
                _signals[InputSignal.Locomotion_DistanceFromStart01] = 0f;
                return;
            }

            // Current position projected to XZ
            Vector3 p = cam.transform.position;
            Vector3 pXZ = new Vector3(p.x, 0f, p.z);

            // Seed baseline (start point + start forward direction)
            if (walkUseBaseline)
            {
                if (!_walkBaselineSeeded)
                {
                    _walkBaselineSeeded = true;
                    _walkBaselinePosXZ_world = pXZ;

                    Vector3 f = cam.transform.forward; f.y = 0f;
                    _walkBaselineFwdXZ_world = (f.sqrMagnitude < 1e-6f) ? Vector3.forward : f.normalized;
                }
            }
            else
            {
                // No baseline: world origin + world forward (rarely desired)
                if (!_walkBaselineSeeded)
                {
                    _walkBaselineSeeded = true;
                    _walkBaselinePosXZ_world = Vector3.zero;
                    _walkBaselineFwdXZ_world = Vector3.forward;
                }
            }

            // Displacement from baseline
            Vector3 d = pXZ - _walkBaselinePosXZ_world;

            // Signed forward distance along baseline forward axis
            float signedCm = Vector3.Dot(d, _walkBaselineFwdXZ_world) * 100f;

            // Only "walk away" increases
            float distCm = walkClampBackwardToZero ? Mathf.Max(0f, signedCm) : signedCm;

            // Optional recenter-jump detection (tracking reset)
            if (walkUseBaseline && walkRecenterJumpCm > 0f && Mathf.Abs(distCm) > walkRecenterJumpCm)
            {
                _walkBaselinePosXZ_world = pXZ;

                Vector3 f = cam.transform.forward; f.y = 0f;
                _walkBaselineFwdXZ_world = (f.sqrMagnitude < 1e-6f) ? _walkBaselineFwdXZ_world : f.normalized;

                distCm = 0f;
            }

            // Debug raw cm
            _signals[InputSignal.Locomotion_DistanceFromStartCm] = distCm;

            // Map cm -> 0..1
            float minCm = Mathf.Min(walkMinCm, walkMaxCm);
            float maxCm = Mathf.Max(walkMinCm, walkMaxCm);

            float clamped = Mathf.Clamp(distCm, minCm, maxCm);
            float t = Mathf.InverseLerp(minCm, maxCm, clamped);
            if (walkInvert) t = 1f - t;

            float target01 = Mathf.Clamp01(t);

            _walk01 = Filter01(
                seeded: ref _walkSeeded,
                current: _walk01,
                target: target01,
                dt: dt,
                deadZone01: walkDeadZone01,
                hysteresis01: walkHysteresis01,
                emaTau: walkEmaTau,
                maxDeltaPerSec: walkMaxDeltaPerSec
            );

            _signals[InputSignal.Locomotion_DistanceFromStart01] = _walk01;

            if (verboseDebug)
            {
                Debug.Log(
                    $"[WalkForwardOnly] signedCm={signedCm:F1} distCm={distCm:F1} " +
                    $"range=[{minCm:F1},{maxCm:F1}] clamp={clamped:F1} out01={_walk01:F3}"
                );
            }
        }



         // -----------------------------
        // Helpers
        // -----------------------------
        Camera EnsureCamera()
        {
            if (hmdCamera) return hmdCamera;
            if (xrOrigin && xrOrigin.Camera) return xrOrigin.Camera;
            return Camera.main;
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
            catch (ObjectDisposedException) { return false; }
            catch { return false; }
        }

        static float Filter01(
            ref bool seeded,
            float current,
            float target,
            float dt,
            float deadZone01,
            float hysteresis01,
            float emaTau,
            float maxDeltaPerSec
        )
        {
            if (!seeded)
            {
                seeded = true;
                return Mathf.Clamp01(target);
            }

            if (Mathf.Abs(target - current) <= Mathf.Max(0f, deadZone01)) target = current;
            if (Mathf.Abs(target - current) <= Mathf.Max(0f, hysteresis01)) target = current;

            float alpha = (emaTau <= 1e-4f) ? 1f : (1f - Mathf.Exp(-dt / emaTau));
            float next = Mathf.Lerp(current, target, alpha);

            if (maxDeltaPerSec > 0f)
            {
                float maxDelta = maxDeltaPerSec * dt;
                next = Mathf.Clamp(next, current - maxDelta, current + maxDelta);
            }

            return Mathf.Clamp01(next);
        }

        private static float SignedPitchDeg(Quaternion worldRot)
        {
            // Convert forward vector to pitch around X in world space
            Vector3 f = worldRot * Vector3.forward;
            // Pitch: angle between forward vector and its projection on XZ plane
            float pitch = Mathf.Atan2(f.y, new Vector2(f.x, f.z).magnitude) * Mathf.Rad2Deg;
            return pitch; // + = up tilt, - = down tilt depending on orientation; we'll use delta vs baseline so it's fine
        }


    }
}
