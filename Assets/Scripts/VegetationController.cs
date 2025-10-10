//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class VegetationController : MonoBehaviour {
//    [Header("Control")]
//    [Range(0f, 1f)] public float density = 0.6f;
//    public int maxInstances = 300;
//    public int seed = 12345;

//    [Tooltip("Use existing child objects as the first candidates (keeps scene trees).")]
//    public bool useExistingChildrenAsSeeds = true;

//    // ---------- Path Band (ONLY mode) ----------
//    [Header("Path Band (shoreline)")]
//    [Tooltip("Waypoints along the coast (children of your CoastPath). Drag P0..Pn here.")]
//    public Transform[] path;            // P0..Pn along shoreline
//    [Tooltip("Total band width (meters). Trees spawn within ±bandWidth/2 from the path centerline.")]
//    public float bandWidth = 12f;
//    [Range(0f, 1f)] public float alongRandomness = 1f; // 1 = uniform along path
//    [Tooltip("Reject points steeper than this (deg). Requires collider on ground.")]
//    public float slopeMax = 35f;

//    // ---------- Placement helpers ----------
//    [Header("Placement")]
//    public bool projectToGround = true;
//    public LayerMask groundMask = ~0;
//    public float raycastHeight = 200f;
//    public float yOffset = 0.05f;

//    [Header("Spacing")]
//    public float minSeparation = 2.0f;  // simple blue-noise via rejection

//    // ---------- Prefabs / Look ----------
//    [Header("Prefabs & Look")]
//    public GameObject[] prefabs;
//    public Vector2 uniformScale = new Vector2(0.9f, 1.2f);
//    public Vector2 yRotationJitter = new Vector2(0f, 360f);

//    // ---------- Transition (anti-blink) ----------
//    public enum Transition { None, PopScale }
//    [Header("Transitions")]
//    public Transition transition = Transition.PopScale;
//    [Tooltip("How many trees can change state per second.")]
//    public float changesPerSecond = 20f;
//    [Tooltip("Seconds for pop-in/out tween.")]
//    public float popDuration = 0.18f;
//    [Tooltip("Start scale for pop-in (0.3=small sprout).")]
//    public float popInStartScale = 0.35f;

//    // ---------- Internal state ----------
//    readonly List<GameObject> pool = new();   // existing + spawned (stable order)
//    readonly List<Vector3> candidates = new();
//    System.Random rng;
//    float lastDensity = -1f;
//    int shown = 0;

//    // work queue so we add/remove gradually
//    readonly Queue<int> toEnable = new();
//    readonly Queue<int> toDisable = new();
//    float budget; // how many changes left this frame

//    void Start() {
//        BuildPoolAndCandidates();

//        // apply once without animation to sync initial state
//        shown = 0;
//        int target = Mathf.RoundToInt(Mathf.Clamp01(density) * pool.Count);
//        for (int i = 0; i < pool.Count; i++) {
//            bool on = i < target;
//            if (pool[i]) { pool[i].SetActive(on); SetScaleImmediate(pool[i], 1f); }
//        }
//        shown = target;
//    }

//    void OnValidate() {
//        if (maxInstances < 0) maxInstances = 0;
//        if (uniformScale.x <= 0f) uniformScale.x = 0.01f;
//        if (uniformScale.y < uniformScale.x) uniformScale.y = uniformScale.x;
//        if (minSeparation < 0f) minSeparation = 0f;
//        if (bandWidth < 0f) bandWidth = 0f;
//        if (popDuration < 0.01f) popDuration = 0.01f;
//        if (changesPerSecond < 1f) changesPerSecond = 1f;

//        // prune nulls from arrays to avoid Inspector binding issues
//#if UNITY_EDITOR
//        if (path != null) {
//            var tmp = new List<Transform>(path.Length);
//            foreach (var t in path) if (t != null) tmp.Add(t);
//            if (tmp.Count != path.Length) path = tmp.ToArray();
//        }
//        if (prefabs != null) {
//            var tp = new List<GameObject>(prefabs.Length);
//            foreach (var p in prefabs) if (p != null) tp.Add(p);
//            if (tp.Count != prefabs.Length) prefabs = tp.ToArray();
//        }
//#endif
//    }

//    void Update() {
//        // enqueue work if density changed
//        if (!Mathf.Approximately(lastDensity, density)) {
//            lastDensity = density;
//            int target = Mathf.RoundToInt(Mathf.Clamp01(density) * pool.Count);

//            if (target > shown) {
//                for (int i = shown; i < target && i < pool.Count; i++)
//                    toEnable.Enqueue(i);
//            } else if (target < shown) {
//                for (int i = shown - 1; i >= target; i--)
//                    toDisable.Enqueue(i);
//            }
//            shown = Mathf.Clamp(target, 0, pool.Count);
//        }

//        // process a budgeted number of changes with animation
//        budget += changesPerSecond * Time.deltaTime;

//        while (budget >= 1f) {
//            bool did = false;

//            if (toDisable.Count > 0) {
//                int idx = toDisable.Dequeue();
//                if (ValidIndex(idx)) {
//                    var go = pool[idx];
//                    if (transition == Transition.PopScale) StartCoroutine(CoPopOut(go));
//                    else go.SetActive(false);
//                    did = true;
//                }
//            } else if (toEnable.Count > 0) {
//                int idx = toEnable.Dequeue();
//                if (ValidIndex(idx)) {
//                    var go = pool[idx];
//                    if (transition == Transition.PopScale) StartCoroutine(CoPopIn(go));
//                    else go.SetActive(true);
//                    did = true;
//                }
//            }

//            if (!did) break;
//            budget -= 1f;
//        }
//    }

//    bool ValidIndex(int i) => i >= 0 && i < pool.Count && pool[i] != null;

//    void BuildPoolAndCandidates() {
//        rng = new System.Random(seed);
//        pool.Clear();
//        candidates.Clear();

//        // Existing children first (stable)
//        if (useExistingChildrenAsSeeds) {
//            foreach (Transform t in transform) {
//                if (!t || !t.gameObject) continue;
//                t.gameObject.SetActive(false);
//                pool.Add(t.gameObject);
//            }
//        }

//        // generate and spawn new ones up to maxInstances
//        int need = Mathf.Max(0, maxInstances - pool.Count);
//        if (need > 0) {
//            GeneratePathBandCandidates(need);

//            for (int i = 0; i < candidates.Count; i++) {
//                var pos = candidates[i];
//                if (prefabs == null || prefabs.Length == 0) break; // no fallback stones

//                int k = rng.Next(prefabs.Length);
//                var prefab = prefabs[Mathf.Clamp(k, 0, prefabs.Length - 1)];
//                var go = Instantiate(prefab, pos, Quaternion.identity, transform);

//                float yrot = Mathf.Lerp(yRotationJitter.x, yRotationJitter.y, (float)rng.NextDouble());
//                go.transform.rotation = Quaternion.Euler(0f, yrot, 0f);
//                float s = Mathf.Lerp(uniformScale.x, uniformScale.y, (float)rng.NextDouble());
//                go.transform.localScale = Vector3.one * s;

//                go.SetActive(false);
//                pool.Add(go);
//            }
//        }

//        // Cap pool
//        if (pool.Count > maxInstances) {
//            for (int i = maxInstances; i < pool.Count; i++)
//                if (pool[i] != null) Destroy(pool[i]);
//            pool.RemoveRange(maxInstances, pool.Count - maxInstances);
//        }
//    }

//    void GeneratePathBandCandidates(int target) {
//        if (path == null || path.Length < 2) return;

//        var pts = new Vector3[path.Length];
//        float[] segLen = new float[path.Length - 1];
//        float total = 0f;
//        for (int i = 0; i < path.Length; i++) pts[i] = path[i].position;
//        for (int i = 0; i < segLen.Length; i++) { segLen[i] = Vector3.Distance(pts[i], pts[i + 1]); total += segLen[i]; }
//        if (total <= 0.01f) return;

//        var accepted = new List<Vector3>(target);
//        int tries = 0, maxTries = target * 60;

//        while (candidates.Count < target && tries < maxTries) {
//            tries++;

//            // sample distance along polyline
//            double u = rng.NextDouble();
//            float dist = (float)(u * total * Mathf.Max(0.0001f, alongRandomness));

//            // find segment
//            int si = 0; float acc = 0f;
//            for (; si < segLen.Length; si++) { if (dist <= acc + segLen[si]) break; acc += segLen[si]; }
//            si = Mathf.Clamp(si, 0, segLen.Length - 1);
//            float t = segLen[si] > 0f ? (dist - acc) / segLen[si] : 0f;

//            Vector3 a = pts[si], b = pts[si + 1];
//            Vector3 center = Vector3.Lerp(a, b, t);

//            // perpendicular jitter in XZ
//            Vector3 dir = b - a; dir.y = 0f;
//            Vector3 right = dir.sqrMagnitude > 1e-4f ? Vector3.Normalize(new Vector3(-dir.z, 0f, dir.x)) : Vector3.right;

//            float half = bandWidth * 0.5f;
//            float offset = (float)((rng.NextDouble() * 2.0 - 1.0) * half);
//            Vector3 world = center + right * offset;

//            if (!PlaceOnGround(ref world)) continue;

//            // simple separation
//            bool ok = true;
//            for (int i = 0; i < accepted.Count; i++) {
//                Vector3 d = world - accepted[i]; d.y = 0f;
//                if (d.sqrMagnitude < (minSeparation * minSeparation)) { ok = false; break; }
//            }
//            if (!ok) continue;

//            accepted.Add(world);
//            candidates.Add(world);
//        }
//    }

//    bool PlaceOnGround(ref Vector3 world) {
//        float y = world.y;
//        if (projectToGround) {
//            Vector3 origin = new Vector3(world.x, world.y + raycastHeight, world.z);
//            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundMask)) {
//                // slope filter
//                if (slopeMax > 0f) {
//                    float angle = Vector3.Angle(hit.normal, Vector3.up);
//                    if (angle > slopeMax) return false;
//                }
//                y = hit.point.y;
//            }
//        }
//        world.y = y + yOffset;
//        return true;
//    }

//    IEnumerator CoPopIn(GameObject go) {
//        if (!go) yield break;
//        go.SetActive(true);

//        Vector3 baseScale = go.transform.localScale;
//        float targetMag = baseScale.x; // assumes uniform
//        float t = 0f;
//        SetUniformScale(go, popInStartScale * targetMag);

//        while (t < popDuration) {
//            t += Time.deltaTime;
//            float k = Mathf.Clamp01(t / popDuration);
//            float s = Mathf.Lerp(popInStartScale, 1f, k);
//            SetUniformScale(go, s * targetMag);
//            yield return null;
//        }
//        SetUniformScale(go, targetMag);
//    }

//    IEnumerator CoPopOut(GameObject go) {
//        if (!go) yield break;

//        Vector3 baseScale = go.transform.localScale;
//        float startMag = baseScale.x;
//        float t = 0f;

//        while (t < popDuration) {
//            t += Time.deltaTime;
//            float k = Mathf.Clamp01(t / popDuration);
//            float s = Mathf.Lerp(1f, 0.25f, k);
//            SetUniformScale(go, s * startMag);
//            yield return null;
//        }
//        go.SetActive(false);
//        SetUniformScale(go, startMag); // reset for reuse
//    }

//    void SetUniformScale(GameObject go, float s) {
//        if (!go) return;
//        go.transform.localScale = new Vector3(s, s, s);
//    }

//    void SetScaleImmediate(GameObject go, float s) {
//        if (!go) return;
//        var ls = go.transform.localScale;
//        float mag = ls.x;
//        go.transform.localScale = new Vector3(mag * s, mag * s, mag * s);
//    }

//    void OnDrawGizmosSelected() {
//        Gizmos.color = Color.green;

//        if (path != null && path.Length > 1) {
//            // centerline
//            for (int i = 0; i < path.Length - 1; i++)
//                Gizmos.DrawLine(path[i].position, path[i + 1].position);

//            // band edges
//            for (int i = 0; i < path.Length - 1; i++) {
//                Vector3 a = path[i].position, b = path[i + 1].position;
//                Vector3 dir = b - a; dir.y = 0f;
//                if (dir.sqrMagnitude < 1e-4f) continue;
//                Vector3 right = Vector3.Normalize(new Vector3(-dir.z, 0f, dir.x));
//                float half = bandWidth * 0.5f;
//                Gizmos.DrawLine(a + right * half, b + right * half);
//                Gizmos.DrawLine(a - right * half, b - right * half);
//            }
//        }
//    }

//    [ContextMenu("Rebuild Vegetation")]
//    public void RebuildNow() {
//        // Destroy runtime-spawned entries (keep scene seeds)
//        foreach (var go in pool)
//            if (go != null && go.transform.parent == transform && !IsSceneSeed(go))
//                Destroy(go);

//        pool.Clear();
//        candidates.Clear();
//        BuildPoolAndCandidates();

//        // re-apply density instantly
//        shown = 0;
//        int target = Mathf.RoundToInt(Mathf.Clamp01(density) * pool.Count);
//        for (int i = 0; i < pool.Count; i++) {
//            bool on = i < target;
//            if (pool[i]) { pool[i].SetActive(on); SetScaleImmediate(pool[i], 1f); }
//        }
//        shown = target;
//    }

//    bool IsSceneSeed(GameObject go) {
//#if UNITY_EDITOR
//        return UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(go) != null;
//#else
//        return false;
//#endif
//    }
//}
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

    // ---------- Path Band (ONLY mode) ----------
    [Header("Path Band (shoreline)")]
    [Tooltip("Waypoints along the coast (children of your CoastPath). Drag P0..Pn here.")]
    public Transform[] path;            // P0..Pn along shoreline
    [Tooltip("Total band width (meters). Trees spawn within ±bandWidth/2 from the path centerline.")]
    public float bandWidth = 12f;
    [Range(0f, 1f)] public float alongRandomness = 1f; // 1 = uniform along path
    [Tooltip("Reject points steeper than this (deg). Requires collider on ground.")]
    public float slopeMax = 35f;

    // ---------- Placement helpers ----------
    [Header("Placement")]
    public bool projectToGround = true;
    public LayerMask groundMask = ~0;
    public float raycastHeight = 200f;
    public float yOffset = 0.05f;

    [Header("Spacing")]
    public float minSeparation = 2.0f;  // simple blue-noise via rejection

    // ---------- Prefabs / Look ----------
    [Header("Prefabs & Look")]
    public GameObject[] prefabs;

    // NOTE: Slider will update this range live; new method ApplyHeightNow() scales existing trees smoothly.
    public Vector2 uniformScale = new Vector2(0.9f, 1.2f);
    public Vector2 yRotationJitter = new Vector2(0f, 360f);

    // ---------- Transition (anti-blink) ----------
    public enum Transition { None, PopScale }
    [Header("Transitions")]
    public Transition transition = Transition.PopScale;
    [Tooltip("How many trees can change state per second.")]
    public float changesPerSecond = 20f;
    [Tooltip("Seconds for pop-in/out tween.")]
    public float popDuration = 0.18f;
    [Tooltip("Start scale for pop-in (0.3=small sprout).")]
    public float popInStartScale = 0.35f;

    // ---------- Internal state ----------
    readonly List<GameObject> pool = new();   // existing + spawned (stable order)
    readonly List<Vector3> candidates = new();
    System.Random rng;
    float lastDensity = -1f;
    int shown = 0;

    // work queue so we add/remove gradually
    readonly Queue<int> toEnable = new();
    readonly Queue<int> toDisable = new();
    float budget; // how many changes left this frame

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

        // prune nulls from arrays to avoid Inspector binding issues
#if UNITY_EDITOR
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
#endif
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
                if (prefabs == null || prefabs.Length == 0) break; // no fallback stones

                int k = rng.Next(prefabs.Length);
                var prefab = prefabs[Mathf.Clamp(k, 0, prefabs.Length - 1)];
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
    }

    bool IsSceneSeed(GameObject go)
    {
#if UNITY_EDITOR
        return UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(go) != null;
#else
        return false;
#endif
    }

    // ======================================================================
    // NEW: Smooth, in-place retargeting of tree heights when uniformScale changes
    // Call from your UI (e.g., slider) after updating uniformScale.
    // ======================================================================
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
            if (!go || !go.activeSelf) continue; // animate only visible ones
            float s = Random.Range(uniformScale.x, uniformScale.y);
            var tr = go.transform;
            targets.Add((tr, tr.localScale, Vector3.one * s));
        }

        if (targets.Count == 0) yield break;

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
    }
}
