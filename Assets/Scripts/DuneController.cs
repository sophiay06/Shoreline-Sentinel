using System.Collections;
using UnityEngine;

public class DuneController : MonoBehaviour
{
    [Header("Scale ranges (relative factors)")]
    public float minHeightScale = 0.2f;
    public float maxHeightScale = 4f;
    public float minWidthScale = 0.5f;
    public float maxWidthScale = 2.5f;

    [Header("Current factors (UI writes here)")]
    [HideInInspector] public float heightScale = 1f;
    [HideInInspector] public float widthScale = 1f;

    // Baselines captured after one frame
    Vector3 baseRootLocalPos;
    Vector3 baseRootScale;

    // For bounds-based anchoring
    Renderer[] renderers;
    float baseBottomLocalY; // distance from pivot to mesh bottom in local units at baseline

    bool initialized;

    void Start() { StartCoroutine(CaptureNextFrame());}

    IEnumerator CaptureNextFrame() {
        yield return null; // let other Start()s settle

        baseRootLocalPos = transform.localPosition;
        baseRootScale = transform.localScale;

        heightScale = 1f;
        widthScale = 1f;

        // Expand ranges to include current look (avoid clamping surprises)
        if (heightScale < minHeightScale) minHeightScale = heightScale;
        if (heightScale > maxHeightScale) maxHeightScale = heightScale;
        if (widthScale < minWidthScale) minWidthScale = widthScale;
        if (widthScale > maxWidthScale) maxWidthScale = widthScale;

        // Precompute bottom offset from combined bounds
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds worldB = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) worldB.Encapsulate(r.bounds);

            // World-space bottom point, then convert to local
            Vector3 worldBottom = new Vector3(worldB.center.x, worldB.min.y, worldB.center.z);
            baseBottomLocalY = transform.InverseTransformPoint(worldBottom).y;
            // Typically <= 0: ~0 if pivot at base, ~-halfHeight if pivot centered.
        }

        initialized = true;
    }

    void LateUpdate() {
        if (!initialized) return;

        float h = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);
        float w = Mathf.Clamp(widthScale, minWidthScale, maxWidthScale);

        // 1) Apply scale to self (x/z = width, y = height)
        Vector3 targetScale = new Vector3(baseRootScale.x * w, baseRootScale.y * h, baseRootScale.z * w);
        transform.localScale = targetScale;

        // 2) Anchor base using the precomputed bottom offset if we have bounds
        if (renderers != null && renderers.Length > 0) {
            // Points below the pivot move by (h-1)*distance_from_pivot.
            // baseBottomLocalY <= 0, so this delta compensates to keep bottom at same world height.
            float deltaY = -(h - 1f) * baseBottomLocalY;

            Vector3 p = baseRootLocalPos;
            p.y += deltaY;
            transform.localPosition = p;
        } else {
            // Fallback if no renderers found: keep original root position
            transform.localPosition = baseRootLocalPos;
        }
    }
}
