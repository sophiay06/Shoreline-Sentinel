//using System.Collections;
//using UnityEngine;

//public class SandbarController : MonoBehaviour
//{
//    [Header("Height scale (Y only)")]
//    public float minHeightScale = 0.2f;
//    public float maxHeightScale = 3f;

//    [Header("Current factor")]
//    [HideInInspector] public float heightScale = 1f;

//    [Header("Z Position")]
//    public float minZOffset = -10f;   // sandbar further away
//    public float maxZOffset = 10f;    // sandbar closer
//    [HideInInspector] public float zOffset = 0f;


//    // Baselines captured after one frame
//    Vector3 baseRootLocalPos;
//    Vector3 baseRootScale;

//    // For bounds-based anchoring
//    Renderer[] renderers;
//    float baseBottomLocalY; // distance from pivot to mesh bottom in local units at baseline

//    bool initialized;

//    void Start() { StartCoroutine(CaptureNextFrame());}

//    IEnumerator CaptureNextFrame() {
//        yield return null; // let other Start()s settle

//        baseRootLocalPos = transform.localPosition;
//        baseRootScale = transform.localScale;

//        heightScale = 1f;
//        if (heightScale < minHeightScale) minHeightScale = heightScale;
//        if (heightScale > maxHeightScale) maxHeightScale = heightScale;

//        // Precompute bottom offset from combined bounds
//        renderers = GetComponentsInChildren<Renderer>();
//        if (renderers != null && renderers.Length > 0) {
//            Bounds worldB = new Bounds(renderers[0].bounds.center, Vector3.zero);
//            foreach (var r in renderers) worldB.Encapsulate(r.bounds);

//            // World-space bottom point, then convert to local
//            Vector3 worldBottom = new Vector3(worldB.center.x, worldB.min.y, worldB.center.z);
//            baseBottomLocalY = transform.InverseTransformPoint(worldBottom).y;
//            // Typically <= 0: ~0 if pivot at base, ~-halfHeight if pivot centered.
//        } else {
//            baseBottomLocalY = 0f; // fallback if no renderers found
//        }

//        initialized = true;
//    }

//    //void LateUpdate() {
//    //    if (!initialized) return;

//    //    float h = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);

//    //    // Apply Y scaling to self (sandbar height)
//    //    Vector3 targetScale = new Vector3(baseRootScale.x, baseRootScale.y * h, baseRootScale.z);
//    //    transform.localScale = targetScale;

//    //    // Anchor base using precomputed bottom offset if we have bounds
//    //    if (renderers != null && renderers.Length > 0) {
//    //        // Points below the pivot move by (h-1)*distanceFromPivot.
//    //        // baseBottomLocalY <= 0, so this delta compensates to keep bottom at same world height.
//    //        float deltaLocalY = -(h - 1f) * baseBottomLocalY;

//    //        Vector3 p = baseRootLocalPos;
//    //        p.y += deltaLocalY;
//    //        transform.localPosition = p;
//    //    } else {
//    //        transform.localPosition = baseRootLocalPos; // keep original root position
//    //    }
//    //}

//    void LateUpdate()
//    {
//        if (!initialized) return;

//        float h = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);

//        // Scale Y (height)
//        Vector3 targetScale = new Vector3(baseRootScale.x, baseRootScale.y * h, baseRootScale.z);
//        transform.localScale = targetScale;

//        // Apply base anchoring
//        Vector3 p = baseRootLocalPos;
//        if (renderers != null && renderers.Length > 0)
//        {
//            float deltaLocalY = -(h - 1f) * baseBottomLocalY;
//            p.y += deltaLocalY;
//        }

//        // Apply Z movement
//        p.z = baseRootLocalPos.z + zOffset;
//        transform.localPosition = p;
//    }

//}
using System.Collections;
using UnityEngine;

public class SandbarController : MonoBehaviour
{
    [Header("Height scale (Y only)")]
    public float minHeightScale = 0.2f;
    public float maxHeightScale = 3f;

    [Header("Z Position (UI writes here)")]
    public float minZOffset = -10f;   // further from beach
    public float maxZOffset = 10f;   // closer to beach
    [HideInInspector] public float zOffset = 0f;

    [Header("Current factor (UI writes here)")]
    [HideInInspector] public float heightScale = 1f;

    // Baselines captured after one frame
    Vector3 baseRootLocalPos;
    Vector3 baseRootScale;

    // For bounds-based anchoring
    Renderer[] renderers;
    float baseBottomLocalY; // distance from pivot to mesh bottom in local units at baseline

    bool initialized;

    void Start() { StartCoroutine(CaptureNextFrame()); }

    IEnumerator CaptureNextFrame()
    {
        yield return null; // let other Start()s settle

        baseRootLocalPos = transform.localPosition;
        baseRootScale = transform.localScale;

        // Keep initial ranges sane
        heightScale = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);
        zOffset = Mathf.Clamp(zOffset, minZOffset, maxZOffset);

        // Precompute bottom offset from combined bounds
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds worldB = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) worldB.Encapsulate(r.bounds);

            Vector3 worldBottom = new Vector3(worldB.center.x, worldB.min.y, worldB.center.z);
            baseBottomLocalY = transform.InverseTransformPoint(worldBottom).y; // <= 0 if pivot near base
        }
        else
        {
            baseBottomLocalY = 0f; // fallback
        }

        initialized = true;
    }

    void LateUpdate()
    {
        if (!initialized) return;

        float h = Mathf.Clamp(heightScale, minHeightScale, maxHeightScale);

        // Apply Y scaling (height)
        Vector3 targetScale = new Vector3(baseRootScale.x, baseRootScale.y * h, baseRootScale.z);
        transform.localScale = targetScale;

        // Anchor base using precomputed bottom offset if we have bounds
        Vector3 p = baseRootLocalPos;
        if (renderers != null && renderers.Length > 0)
        {
            // baseBottomLocalY <= 0, so this delta compensates to keep bottom at same world height.
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
