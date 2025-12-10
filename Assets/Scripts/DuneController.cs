using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DuneController : MonoBehaviour
{
    [Header("Scale ranges (relative factors)")]
    public float minHeightScale = 0.2f;
    public float maxHeightScale = 4f;
    public float minWidthScale  = 0.5f;
    public float maxWidthScale  = 2.5f;

    [Header("Current factors (UI writes here)")]
    [HideInInspector] public float heightScale = 1f;
    [HideInInspector] public float widthScale  = 1f;

    Vector3 baseRootLocalPos;
    Vector3 baseRootScale;

    [System.Serializable]
    class DunePiece
    {
        public Transform t;
        public Vector3 baseScale;
        public Vector3 baseLocalPos;

        public Vector3 baseLocalCenter;
        public float   baseBottomLocalY;
    }

    readonly List<DunePiece> pieces = new List<DunePiece>();
    bool initialized;

    void Start()
    {
        StartCoroutine(CaptureNextFrame());
    }

    IEnumerator CaptureNextFrame()
    {
        yield return null; // let other Start() settle so bounds/inits are valid

        baseRootLocalPos = transform.localPosition;
        baseRootScale    = transform.localScale;

        // Clamp to given ranges (keeps UI sane)
        heightScale = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);
        widthScale  = Mathf.Clamp(widthScale,  minWidthScale,  maxWidthScale);

        var rs = GetComponentsInChildren<Renderer>(includeInactive: true); // find all renderers under this controller; treat each as an individual dune

        pieces.Clear();
        foreach (var r in rs)
        {
            var t = r.transform;

            // Cache base transform
            var entry = new DunePiece
            {
                t = t,
                baseScale = t.localScale,
                baseLocalPos = t.localPosition
            };

            // Use this renderer's world-space bounds, then convert to local of t
            Bounds worldB = r.bounds;

            Vector3 localCenter = t.InverseTransformPoint(worldB.center);
            Vector3 worldBottom = new Vector3(worldB.center.x, worldB.min.y, worldB.center.z);
            float   localBottomY = t.InverseTransformPoint(worldBottom).y;

            entry.baseLocalCenter  = localCenter;
            entry.baseBottomLocalY = localBottomY;

            pieces.Add(entry);
        }

        initialized = true;
    }

    void LateUpdate()
    {
        if (!initialized) return;

        // Clamp requested factors
        float h = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);
        float w = Mathf.Clamp(widthScale,  minWidthScale,  maxWidthScale);

        // Keep the controller root fixed
        transform.localScale = baseRootScale;
        transform.localPosition = baseRootLocalPos;

        // Apply per-renderer scaling around bottom-center, with anti-drift correction
        Vector3 perAxis = new Vector3(w, h, w);

        foreach (var d in pieces)
        {
            d.t.localScale = new Vector3(
                d.baseScale.x * perAxis.x,
                d.baseScale.y * perAxis.y,
                d.baseScale.z * perAxis.z
            );

            // anchor: bottom-center of this piece's bounds in its local space
            Vector3 anchorLocal = new Vector3(
                d.baseLocalCenter.x,
                d.baseBottomLocalY,
                d.baseLocalCenter.z
            );

            // pos correction for non-uniform scaling: delta = anchor * (1 - scalePerAxis)
            Vector3 correctionLocal = new Vector3(
                anchorLocal.x * (1f - perAxis.x),
                anchorLocal.y * (1f - perAxis.y),
                anchorLocal.z * (1f - perAxis.z)
            );

            d.t.localPosition = d.baseLocalPos + correctionLocal;
        }
    }
}
