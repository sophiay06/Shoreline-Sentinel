using System.Collections;
using UnityEngine;

public class SandbarController : MonoBehaviour
{
    [Header("Height scale (Y only)")]
    public float minHeightScale = 0.2f;
    public float maxHeightScale = 3f;

    [Header("Z Position (UI writes here)")]
    public float minZOffset = -10f; // further from beach
    public float maxZOffset = 10f; // closer to
    [HideInInspector] public float zOffset = 0f;

    [Header("Current factor (UI writes here)")]
    [HideInInspector] public float heightScale = 1f;

    // baselines
    Vector3 baseRootLocalPos;
    Vector3 baseRootScale;

    Renderer[] renderers;
    float baseBottomLocalY;

    bool initialized;

    void Start() { StartCoroutine(CaptureNextFrame()); }

    IEnumerator CaptureNextFrame()
    {
        yield return null; // let other Start()s settle

        baseRootLocalPos = transform.localPosition;
        baseRootScale = transform.localScale;

        heightScale = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);
        zOffset = Mathf.Clamp(zOffset, minZOffset, maxZOffset);

        // Precompute bottom offset from combined bounds
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds worldB = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) worldB.Encapsulate(r.bounds);

            Vector3 worldBottom = new Vector3(worldB.center.x, worldB.min.y, worldB.center.z);
            baseBottomLocalY = transform.InverseTransformPoint(worldBottom).y;
        }
        else
        {
            baseBottomLocalY = 0f;
        }

        initialized = true;
    }

    void LateUpdate()
    {
        if (!initialized) return;

        float h = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);

        Vector3 targetScale = new Vector3(baseRootScale.x, baseRootScale.y * h, baseRootScale.z);
        transform.localScale = targetScale;

        // Anchor base using precomputed bottom offset if we have bounds
        Vector3 p = baseRootLocalPos;
        if (renderers != null && renderers.Length > 0)
        {
            // baseBottomLocalY <= 0, so this delta compensates to keep bottom at same world height
            float deltaLocalY = -(h - 1f) * baseBottomLocalY;
            p.y += deltaLocalY;
        }

        // Apply Z movement
        zOffset = Mathf.Clamp(zOffset, minZOffset, maxZOffset);
        p.z = baseRootLocalPos.z + zOffset;

        transform.localPosition = p;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (maxHeightScale < minHeightScale) maxHeightScale = minHeightScale;
        if (maxZOffset < minZOffset) maxZOffset = minZOffset;
    }
#endif
}
