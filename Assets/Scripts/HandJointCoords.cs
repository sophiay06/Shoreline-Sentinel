// HandJointCoords.cs (safe)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class HandJointCoords : MonoBehaviour
{
    [Header("Optional: drag your XROrigin (XR Rig) here if it's not at identity")]
    public XROrigin xrOrigin;

    XRHandSubsystem _hands;

    static readonly XRHandJointID[] k_JointsToReport = new XRHandJointID[]
    {
        XRHandJointID.Wrist,
        XRHandJointID.Palm,
        XRHandJointID.ThumbTip,
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip
    };

    void OnEnable()
    {
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        _hands = list.Find(s => s != null && s.running);
    }

    void OnDisable()
    {
        _hands = null;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (_hands == null || !_hands.running) return;

        var left = _hands.leftHand;
        var right = _hands.rightHand;

        if (left.isTracked) ReportHand(left, "Left");
        if (right.isTracked) ReportHand(right, "Right");
    }

    void ReportHand(XRHand hand, string label)
    {
        if (!hand.isTracked) return;

        foreach (var id in k_JointsToReport)
        {
            var j = hand.GetJoint(id);
            if (j.TryGetPose(out Pose jointPose))
            {
                var worldPos = jointPose.position;
                var worldRot = jointPose.rotation;

                if (xrOrigin != null)
                {
                    var p = xrOrigin.Origin.transform.TransformPose(jointPose);
                    worldPos = p.position;
                    worldRot = p.rotation;
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        // Edit-time gizmos fire when subsystems are gone
        if (!Application.isPlaying) return;
        if (_hands == null || !_hands.running) return;

        DrawGizmoHand(_hands.leftHand, Color.green);
        DrawGizmoHand(_hands.rightHand, Color.cyan);
    }

    void DrawGizmoHand(XRHand hand, Color c)
    {
        if (!hand.isTracked) return;
        Gizmos.color = c;

        foreach (var id in k_JointsToReport)
        {
            var j = hand.GetJoint(id);
            if (!j.TryGetPose(out Pose p)) continue;
            if (xrOrigin != null) p = xrOrigin.Origin.transform.TransformPose(p);
            Gizmos.DrawSphere(p.position, 0.01f);
        }
    }
}
