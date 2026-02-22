using UnityEngine;
using OscJack;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Collections.Concurrent;

public class SlimeVROscReceiver : MonoBehaviour
{
    [Header("OSC")]
    [Tooltip("UDP port to receive tracker data from SlimeVR Server (VRChat OSC Trackers).")]
    public int listenPort = 9011;

    [Tooltip("If true, will try subsequent ports (listenPort+1..+5) if the chosen port is busy.")]
    public bool tryAlternatePorts = false;

    [Header("XR Rig Root (usually your XR Origin)")]
    public Transform xrOrigin;

    [Header("Debug")]
    [Tooltip("How often to print tracker data (frames). Set to 1 to print every frame.")]
    public int logEveryNFrames = 30;

    [Serializable]
    public class TrackerMap
    {
        [Range(1, 8)] public int id = 1;// VRChat tracker id
        public Transform target;
    }
    public TrackerMap[] maps;

    OscServer _server;
    readonly Dictionary<int, Transform> _idToTarget = new();
    int _boundPort = -1;

    //data coming from OSC thread
    enum UpdateType { Position, Rotation }

    struct TrackerUpdate
    {
        public int id;
        public UpdateType type;
        public Vector3 vec;// position OR euler angles
    }

    // Thread-safe queue shared between OSC thread and main thread
    readonly ConcurrentQueue<TrackerUpdate> _pendingUpdates = new();

    void OnEnable()
    {
        // Build map
        _idToTarget.Clear();
        foreach (var m in maps)
            if (m.target)
                _idToTarget[m.id] = m.target;

        if (!xrOrigin)
            xrOrigin = transform;

        // Bind server (with optional fallback ports)
        int attempts = tryAlternatePorts ? 6 : 1;
        int port = listenPort;
        SocketException lastEx = null;

        for (int i = 0; i < attempts; i++)
        {
            try
            {
                _server = new OscServer(port);
                _server.MessageDispatcher.AddCallback("", OnAnyMessage);

                _boundPort = port;
                Debug.Log($"[SlimeVR] OSC server bound on UDP {port}");
                return;
            }
            catch (SocketException ex)
            {
                lastEx = ex;
                _server?.Dispose();
                _server = null;
                port++;
            }
        }

        Debug.LogError($"[SlimeVR] Failed to bind OSC server on {listenPort}. Last error: {lastEx?.Message}");
        enabled = false;
    }

    void OnDisable()
    {
        _server?.Dispose();
        _server = null;
        _boundPort = -1;
    }

    void OnDestroy()
    {
        _server?.Dispose();
        _server = null;
        _boundPort = -1;
    }

    Transform FindTarget(int id) =>
        _idToTarget.TryGetValue(id, out var t) ? t : null;

    Vector3 WorldToLocalPos(Vector3 world) =>
        xrOrigin.InverseTransformPoint(world);

    Quaternion WorldToLocalRot(Quaternion world) =>
        Quaternion.Inverse(xrOrigin.rotation) * world;

    static Vector3 FlipZ(Vector3 v) => new(v.x, v.y, -v.z);
    static Quaternion EulerFlipZ(Vector3 e) =>
        Quaternion.Euler(e.x, e.y, -e.z);

    //osc thread
    void OnAnyMessage(string address, OscDataHandle data)
    {
        if (!address.StartsWith("/tracking/trackers/"))
            return;

        var parts = address.Split('/');
        if (parts.Length < 5) return;

        if (!int.TryParse(parts[3], out int id))
            return;

        string tail = parts[4];

        if (tail == "position")
        {
            if (data.GetElementCount() < 3) return;

            var pos = new Vector3(
                data.GetElementAsFloat(0),
                data.GetElementAsFloat(1),
                data.GetElementAsFloat(2));

            _pendingUpdates.Enqueue(new TrackerUpdate
            {
                id = id,
                type = UpdateType.Position,
                vec = pos
            });
        }
        else if (tail == "rotation")
        {
            if (data.GetElementCount() < 3) return;

            var euler = new Vector3(
                data.GetElementAsFloat(0),
                data.GetElementAsFloat(1),
                data.GetElementAsFloat(2));

            _pendingUpdates.Enqueue(new TrackerUpdate
            {
                id = id,
                type = UpdateType.Rotation,
                vec = euler
            });
        }
    }


    // mainthread
    void Update()
    {
        while (_pendingUpdates.TryDequeue(out var upd))
        {
            var t = FindTarget(upd.id);
            if (!t) continue;
            //Debug.Log($"Tracker {upd.id} updating instanceID {t.GetInstanceID()} name {t.name}");
            if (upd.type == UpdateType.Position)
            {
                var worldPos = upd.vec;
                t.localPosition = WorldToLocalPos(worldPos);

                if (logEveryNFrames > 0 && Time.frameCount % logEveryNFrames == 0)
                {
                    Debug.Log($"[SlimeVR] Tracker {upd.id} Position = {t.localPosition}");
                }
            }
            else// Rotation
            {
                var worldRot = Quaternion.Euler(upd.vec);
                t.localRotation = WorldToLocalRot(worldRot);

                if (logEveryNFrames > 0 && Time.frameCount % logEveryNFrames == 0)
                {
                    Debug.Log($"[SlimeVR] Tracker {upd.id} Rotation = {t.localRotation.eulerAngles}");
                }
            }
        }
    }
}
