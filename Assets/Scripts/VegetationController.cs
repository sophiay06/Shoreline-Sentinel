using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VegetationController : MonoBehaviour
{
    [Header("Control")]
    [Range(0f, 1f)] public float density = 0.6f;
    public int maxInstances = 300;
    public int seed = 12345;

    [Tooltip("Use existing child objects as the first candidates (keeps scene trees).")]
    public bool useExistingChildrenAsSeeds = true;

    [Header("Tree Types")]
    [Tooltip("If assigned, these override 'prefabs[]' for type selection.")]
    public List<TreeVariant> variants = new List<TreeVariant>();
    [Tooltip("If true, spawn a random mix of all variants; otherwise use Selected Variant Index.")]
    public bool mixedTypes = false;
    [Tooltip("Used when mixedTypes = false.")]
    public int selectedVariantIndex = 0;

    // Path Band
    [Header("Path Band (shoreline)")]
    [Tooltip("Waypoints along the coast (children of your CoastPath). Drag P0..Pn here.")]
    public Transform[] path;
    [Tooltip("Total band width (meters). Trees spawn within ±bandWidth/2 from the path centerline.")]
    public float bandWidth = 12f;
    [Range(0f, 1f)] public float alongRandomness = 1f;
    [Tooltip("Reject points steeper than this (deg). Requires collider on ground.")]
    public float slopeMax = 35f;

    // Placement helpers
    [Header("Placement")]
    public bool projectToGround = true;
    public LayerMask groundMask = ~0;
    public float raycastHeight = 200f;
    public float yOffset = 0.05f;

    [Header("Spacing")]
    public float minSeparation = 2.0f;// simple blue-noise via rejection

    [Header("Prefabs & Look")]
    public GameObject[] prefabs;

    public Vector2 uniformScale = new Vector2(0.9f, 1.2f);
    public Vector2 yRotationJitter = new Vector2(0f, 360f);

    public enum Transition { None, PopScale }
    [Header("Transitions")]
    public Transition transition = Transition.PopScale;
    [Tooltip("How many trees can change state per second.")]
    public float changesPerSecond = 20f;
    [Tooltip("Seconds for pop-in/out tween.")]
    public float popDuration = 0.18f;
    [Tooltip("Start scale for pop-in (0.3=small sprout).")]
    public float popInStartScale = 0.35f;

    // Tree Height Feature (Y-only) 
    [Header("Tree Height (Y-only)")]
    [Tooltip("Multiplies tree height (Y scale) without widening trunks. 1 = original.")]
    public float minTreeHeightScale = 0.5f;
    public float maxTreeHeightScale = 2.0f;
    [HideInInspector] public float treeHeightScale = 1.0f;

    //internal 
    readonly List<GameObject> pool = new();
    readonly List<Vector3> candidates = new();
    System.Random rng;
    float lastDensity = -1f;
    int shown = 0;

    // work queue so we add/remove gradually
    readonly Queue<int> toEnable = new();
    readonly Queue<int> toDisable = new();
    float budget; // how many changes left this frame

    //tree anchors & baselines for Y scaling 
    class TreeAnchor
    {
        public Transform t;
        public Vector3 baseLocalPos;// position before Y-only scaling
        public Vector3 baseLocalScale;  // scale before Y-only scaling
        public float   baseBottomLocalY;// bottom of renderer bounds in local space
        public Vector3 baseLocalCenter;// not strictly needed for Y-only, but handy
    }
    readonly List<TreeAnchor> anchors = new();
    bool anchorsCaptured;

    void Start()
    {
        BuildPoolAndCandidates();

        // apply once without animation to sync initial state
        shown = 0;
        int target = Mathf.RoundToInt(Mathf.Clamp01(density) * pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            bool on = i < target;
            if (pool[i]) { pool[i].SetActive(on); SetScaleImmediate(pool[i], 1f); }
        }
        shown = target;

        CaptureAnchorsFromPool();
    }

    void OnValidate()
    {
        if (maxInstances < 0) maxInstances = 0;
        if (uniformScale.x <= 0f) uniformScale.x = 0.01f;
        if (uniformScale.y < uniformScale.x) uniformScale.y = uniformScale.x;
        if (minSeparation < 0f) minSeparation = 0f;
        if (bandWidth < 0f) bandWidth = 0f;
        if (popDuration < 0.01f) popDuration = 0.01f;
        if (changesPerSecond < 1f) changesPerSecond = 1f;

        if (minTreeHeightScale <= 0f) minTreeHeightScale = 0.05f;
        if (maxTreeHeightScale < minTreeHeightScale) maxTreeHeightScale = minTreeHeightScale;

#if UNITY_EDITOR
        // prune nulls to avoid Inspector issues
        if (path != null)
        {
            var tmp = new List<Transform>(path.Length);
            foreach (var t in path) if (t != null) tmp.Add(t);
            if (tmp.Count != path.Length) path = tmp.ToArray();
        }
        if (prefabs != null)
        {
            var tp = new List<GameObject>(prefabs.Length);
            foreach (var p in prefabs) if (p != null) tp.Add(p);
            if (tp.Count != prefabs.Length) prefabs = tp.ToArray();
        }
        if (variants != null)
        {
            // keep list tidy (no null assets)
            for (int i = variants.Count - 1; i >= 0; i--)
                if (variants[i] == null) variants.RemoveAt(i);
        }
#endif
        // clamp selected index
        if (variants != null && variants.Count > 0)
            selectedVariantIndex = Mathf.Clamp(selectedVariantIndex, 0, variants.Count - 1);
        else
            selectedVariantIndex = 0;
    }

    void Update()
    {
        // enqueue work if density changed
        if (!Mathf.Approximately(lastDensity, density))
        {
            lastDensity = density;
            int target = Mathf.RoundToInt(Mathf.Clamp01(density) * pool.Count);

            if (target > shown)
            {
                for (int i = shown; i < target && i < pool.Count; i++)
                    toEnable.Enqueue(i);
            }
            else if (target < shown)
            {
                for (int i = shown - 1; i >= target; i--)
                    toDisable.Enqueue(i);
            }
            shown = Mathf.Clamp(target, 0, pool.Count);
        }

        // process a budgeted number of changes with animation
        budget += changesPerSecond * Time.deltaTime;

        while (budget >= 1f)
        {
            bool did = false;

            if (toDisable.Count > 0)
            {
                int idx = toDisable.Dequeue();
                if (ValidIndex(idx))
                {
                    var go = pool[idx];
                    if (transition == Transition.PopScale) StartCoroutine(CoPopOut(go));
                    else go.SetActive(false);
                    did = true;
                }
            }
            else if (toEnable.Count > 0)
            {
                int idx = toEnable.Dequeue();
                if (ValidIndex(idx))
                {
                    var go = pool[idx];
                    if (transition == Transition.PopScale) StartCoroutine(CoPopIn(go));
                    else go.SetActive(true);
                    did = true;
                }
            }

            if (!did) break;
            budget -= 1f;
        }
    }

    void LateUpdate()
    {
        // Apply the Tree Height (Y-only) every frame so it stacks cleanly
        if (!anchorsCaptured) return;

        float h = Mathf.Clamp(treeHeightScale, minTreeHeightScale, maxTreeHeightScale);

        for (int i = 0; i < anchors.Count; i++)
        {
            var a = anchors[i];
            if (a == null || a.t == null) continue;

            // Scale only Y, keep XZ from the captured baseline (which already includes uniformScale)
            Vector3 s = a.baseLocalScale;
            s.y *= h;
            a.t.localScale = s;

            // Keep the base pinned
            float dy = (1f - h) * a.baseBottomLocalY;
            a.t.localPosition = new Vector3(a.baseLocalPos.x, a.baseLocalPos.y + dy, a.baseLocalPos.z);
        }
    }

    bool ValidIndex(int i) => i >= 0 && i < pool.Count && pool[i] != null;

    void BuildPoolAndCandidates()
    {
        rng = new System.Random(seed);
        pool.Clear();
        candidates.Clear();

        // Existing children first (stable)
        if (useExistingChildrenAsSeeds)
        {
            foreach (Transform t in transform)
            {
                if (!t || !t.gameObject) continue;
                t.gameObject.SetActive(false);
                pool.Add(t.gameObject);
            }
        }

        // generate and spawn new ones up to maxInstances
        int need = Mathf.Max(0, maxInstances - pool.Count);
        if (need > 0)
        {
            GeneratePathBandCandidates(need);

            for (int i = 0; i < candidates.Count; i++)
            {
                var pos = candidates[i];
                var prefab = PickPrefab();

                if (prefab == null)
                {
                    // No variants and no prefabs: nothing to spawn
                    break;
                }

                var go = Instantiate(prefab, pos, Quaternion.identity, transform);

                float yrot = Mathf.Lerp(yRotationJitter.x, yRotationJitter.y, (float)rng.NextDouble());
                go.transform.rotation = Quaternion.Euler(0f, yrot, 0f);
                float s = Mathf.Lerp(uniformScale.x, uniformScale.y, (float)rng.NextDouble());
                go.transform.localScale = Vector3.one * s;

                go.SetActive(false);
                pool.Add(go);
            }
        }

        // Cap pool
        if (pool.Count > maxInstances)
        {
            for (int i = maxInstances; i < pool.Count; i++)
                if (pool[i] != null) Destroy(pool[i]);
            pool.RemoveRange(maxInstances, pool.Count - maxInstances);
        }
    }

    //Choose a prefab based on current type settings
    GameObject PickPrefab()
    {
        // If variants are set, prefer them
        if (variants != null && variants.Count > 0)
        {
            if (mixedTypes)
            {
                // random among all variant prefabs
                int vi = rng.Next(variants.Count);
                var v = variants[vi];
                return v ? v.prefab : null;
            }
            else
            {
                int idx = Mathf.Clamp(selectedVariantIndex, 0, variants.Count - 1);
                var v = variants[idx];
                return v ? v.prefab : null;
            }
        }

        // Fallback: old behavior (random from prefabs[])
        if (prefabs != null && prefabs.Length > 0)
        {
            int k = rng.Next(prefabs.Length);
            return prefabs[Mathf.Clamp(k, 0, prefabs.Length - 1)];
        }

        return null;
    }

    void GeneratePathBandCandidates(int target)
    {
        if (path == null || path.Length < 2) return;

        var pts = new Vector3[path.Length];
        float[] segLen = new float[path.Length - 1];
        float total = 0f;
        for (int i = 0; i < path.Length; i++) pts[i] = path[i].position;
        for (int i = 0; i < segLen.Length; i++) { segLen[i] = Vector3.Distance(pts[i], pts[i + 1]); total += segLen[i]; }
        if (total <= 0.01f) return;

        var accepted = new List<Vector3>(target);
        int tries = 0, maxTries = target * 60;

        while (candidates.Count < target && tries < maxTries)
        {
            tries++;

            // sample distance along polyline
            double u = rng.NextDouble();
            float dist = (float)(u * total * Mathf.Max(0.0001f, alongRandomness));

            // find segment
            int si = 0; float acc = 0f;
            for (; si < segLen.Length; si++) { if (dist <= acc + segLen[si]) break; acc += segLen[si]; }
            si = Mathf.Clamp(si, 0, segLen.Length - 1);
            float t = segLen[si] > 0f ? (dist - acc) / segLen[si] : 0f;

            Vector3 a = pts[si], b = pts[si + 1];
            Vector3 center = Vector3.Lerp(a, b, t);

            // perpendicular jitter in XZ
            Vector3 dir = b - a; dir.y = 0f;
            Vector3 right = dir.sqrMagnitude > 1e-4f ? Vector3.Normalize(new Vector3(-dir.z, 0f, dir.x)) : Vector3.right;

            float half = bandWidth * 0.5f;
            float offset = (float)((rng.NextDouble() * 2.0 - 1.0) * half);
            Vector3 world = center + right * offset;

            if (!PlaceOnGround(ref world)) continue;

            // simple separation
            bool ok = true;
            for (int i = 0; i < accepted.Count; i++)
            {
                Vector3 d = world - accepted[i]; d.y = 0f;
                if (d.sqrMagnitude < (minSeparation * minSeparation)) { ok = false; break; }
            }
            if (!ok) continue;

            accepted.Add(world);
            candidates.Add(world);
        }
    }

    bool PlaceOnGround(ref Vector3 world)
    {
        float y = world.y;
        if (projectToGround)
        {
            Vector3 origin = new Vector3(world.x, world.y + raycastHeight, world.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundMask))
            {
                // slope filter
                if (slopeMax > 0f)
                {
                    float angle = Vector3.Angle(hit.normal, Vector3.up);
                    if (angle > slopeMax) return false;
                }
                y = hit.point.y;
            }
        }
        world.y = y + yOffset;
        return true;
    }

    IEnumerator CoPopIn(GameObject go)
    {
        if (!go) yield break;
        go.SetActive(true);

        Vector3 baseScale = go.transform.localScale;
        float targetMag = baseScale.x; // assumes uniform
        float t = 0f;
        SetUniformScale(go, popInStartScale * targetMag);

        while (t < popDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            float s = Mathf.Lerp(popInStartScale, 1f, k);
            SetUniformScale(go, s * targetMag);
            yield return null;
        }
        SetUniformScale(go, targetMag);

        // ensure anchors reflect whatever scale we ended up at
        CaptureAnchorsFromPool();
    }

    IEnumerator CoPopOut(GameObject go)
    {
        if (!go) yield break;

        Vector3 baseScale = go.transform.localScale;
        float startMag = baseScale.x;
        float t = 0f;

        while (t < popDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            float s = Mathf.Lerp(1f, 0.25f, k);
            SetUniformScale(go, s * startMag);
            yield return null;
        }
        go.SetActive(false);
        SetUniformScale(go, startMag); // reset for reuse

        // After pop-out we still keep anchors for active items; no need to recapture here
    }

    void SetUniformScale(GameObject go, float s)
    {
        if (!go) return;
        go.transform.localScale = new Vector3(s, s, s);
    }

    void SetScaleImmediate(GameObject go, float s)
    {
        if (!go) return;
        var ls = go.transform.localScale;
        float mag = ls.x;
        go.transform.localScale = new Vector3(mag * s, mag * s, mag * s);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        if (path != null && path.Length > 1)
        {
            // centerline
            for (int i = 0; i < path.Length - 1; i++)
                Gizmos.DrawLine(path[i].position, path[i + 1].position);

            // band edges
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i].position, b = path[i + 1].position;
                Vector3 dir = b - a; dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                Vector3 right = Vector3.Normalize(new Vector3(-dir.z, 0f, dir.x));
                float half = bandWidth * 0.5f;
                Gizmos.DrawLine(a + right * half, b + right * half);
                Gizmos.DrawLine(a - right * half, b - right * half);
            }
        }
    }

    [ContextMenu("Rebuild Vegetation")]
    public void RebuildNow()
    {
        // Destroy runtime-spawned entries (keep scene seeds)
        foreach (var go in pool)
            if (go != null && go.transform.parent == transform && !IsSceneSeed(go))
                Destroy(go);

        pool.Clear();
        candidates.Clear();
        BuildPoolAndCandidates();

        // re-apply density instantly
        shown = 0;
        int target = Mathf.RoundToInt(Mathf.Clamp01(density) * pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            bool on = i < target;
            if (pool[i]) { pool[i].SetActive(on); SetScaleImmediate(pool[i], 1f); }
        }
        shown = target;

        // recapture anchors after rebuild
        CaptureAnchorsFromPool();
    }

    bool IsSceneSeed(GameObject go)
    {
#if UNITY_EDITOR
        return UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(go) != null;
#else
        return false;
#endif
    }

    //public APIs for UI to change tree type
    public void SetTreeTypeIndex(int idx)
    {
        mixedTypes = false;
        if (variants != null && variants.Count > 0)
            selectedVariantIndex = Mathf.Clamp(idx, 0, variants.Count - 1);
        else
            selectedVariantIndex = 0;
        RebuildNow(); // respawn with the new type
    }

    public void SetMixedTypes(bool on)
    {
        mixedTypes = on;
        RebuildNow(); // respawn with mixed/solo mode
    }

    //smooth, in-place retargeting of tree heights when uniformScale changes
    public void ApplyHeightNow(bool tween = true)
    {
        if (!tween)
        {
            // Instant retarget of all trees (active or inactive)
            foreach (var go in pool)
            {
                if (!go) continue;
                float s = Random.Range(uniformScale.x, uniformScale.y);
                go.transform.localScale = Vector3.one * s;
            }
            // Y-only height sits on top of this, so refresh anchors
            CaptureAnchorsFromPool();
            return;
        }

        // Smoothly tween active trees to new random sizes within uniformScale
        StopCoroutine(nameof(CoRetargetHeights));
        StartCoroutine(CoRetargetHeights(popDuration));
    }

    IEnumerator CoRetargetHeights(float duration)
    {
        var targets = new List<(Transform t, Vector3 start, Vector3 end)>(pool.Count);
        targets.Clear();

        // Sample target scales up front for determinism during this tween
        foreach (var go in pool)
        {
            if (!go || !go.activeSelf) continue;
            float s = Random.Range(uniformScale.x, uniformScale.y);
            var tr = go.transform;
            targets.Add((tr, tr.localScale, Vector3.one * s));
        }

        if (targets.Count == 0)
        {
            // Still make sure anchors reflect current state
            CaptureAnchorsFromPool();
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // Smoothstep for pleasant growth
            float e = k * k * (3f - 2f * k);

            for (int i = 0; i < targets.Count; i++)
            {
                var tuple = targets[i];
                tuple.t.localScale = Vector3.Lerp(tuple.start, tuple.end, e);
            }
            yield return null;
        }

        // Snap to exact targets
        for (int i = 0; i < targets.Count; i++)
            targets[i].t.localScale = targets[i].end;

        // After uniform scale changes, refresh anchors so Y-only scaling stays pinned
        CaptureAnchorsFromPool();
    }

    //capture per-tree anchors
    void CaptureAnchorsFromPool()
    {
        anchors.Clear();

        for (int i = 0; i < pool.Count; i++)
        {
            var go = pool[i];
            if (!go) continue;
            var t = go.transform;

            // Use combined bounds of all renderers under this tree (prefab might have multiple parts)
            var rs = t.GetComponentsInChildren<Renderer>(includeInactive: true);
            Bounds? worldB = null;
            for (int r = 0; r < rs.Length; r++)
            {
                if (!rs[r]) continue;
                worldB = worldB.HasValue ? Enc(worldB.Value, rs[r].bounds) : rs[r].bounds;
            }

            var a = new TreeAnchor();
            a.t = t;
            a.baseLocalPos   = t.localPosition;
            a.baseLocalScale = t.localScale;

            if (worldB.HasValue)
            {
                Bounds wb = worldB.Value;
                Vector3 localCenter = t.InverseTransformPoint(wb.center);
                Vector3 worldBottom = new Vector3(wb.center.x, wb.min.y, wb.center.z);
                float   localBottomY = t.InverseTransformPoint(worldBottom).y;

                a.baseLocalCenter  = localCenter;
                a.baseBottomLocalY = localBottomY;
            }
            else
            {
                a.baseLocalCenter  = Vector3.zero;
                a.baseBottomLocalY = 0f;
            }

            anchors.Add(a);
        }

        anchorsCaptured = true;

        static Bounds Enc(Bounds b, Bounds add)
        {
            b.Encapsulate(add.min);
            b.Encapsulate(add.max);
            return b;
        }
    }
}
