using System.Collections.Generic;
using UnityEngine;

public class DuneDensityController : MonoBehaviour
{
    [Header("Spawn points")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("What to spawn at each point")]
    public GameObject dunePrefab;

    [Header("Controls")]
    [Range(0f, 1f)] public float density = 0.6f;
    public int seed = 1234;

    [Header("Placement")]
    public bool snapToGround = true;
    public LayerMask groundMask = ~0; 
    public float raycastUp = 10f;
    public float raycastDown = 50f;
    public float baseYOffset = 0f;

    [Header("Variation")]
    public Vector2 uniformScaleRange = new Vector2(0.9f, 1.15f);
    public Vector2 yRotJitterRange = new Vector2(-10f, 10f);

    readonly List<GameObject> _pool = new();
    int _lastCount = -1;
    int _lastSeed = -1;

    void Awake()
    {
        EnsurePoolSize();
        Apply(true);
    }

    void OnValidate()
    {
        EnsurePoolSize();
    }

    void EnsurePoolSize()
    {
        while (_pool.Count < spawnPoints.Count) _pool.Add(null);

        if (_pool.Count > spawnPoints.Count)
            _pool.RemoveRange(spawnPoints.Count, _pool.Count - spawnPoints.Count);
    }

    public void SetDensity01(float t)
    {
        density = Mathf.Clamp01(t);
        Apply(false);
    }

    // Rebuild selection if density or seed changed
    public void Apply(bool force)
    {
        int target = Mathf.Clamp(Mathf.RoundToInt(density * spawnPoints.Count), 0, spawnPoints.Count);

        if (!force && target == _lastCount && seed == _lastSeed)
            return;

        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i]) _pool[i].SetActive(false);
        }

        var rng = new System.Random(seed);
        List<int> indices = new List<int>(spawnPoints.Count);
        for (int i = 0; i < spawnPoints.Count; i++) indices.Add(i);
        // Fisher–Yates shuffle
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        // Activate first K points
        for (int k = 0; k < target; k++)
        {
            int idx = indices[k];
            SpawnOrUpdate(idx, rng);
        }

        _lastCount = target;
        _lastSeed = seed;
    }

    void SpawnOrUpdate(int idx, System.Random rng)
    {
        if (idx < 0 || idx >= spawnPoints.Count) return;
        Transform p = spawnPoints[idx];
        if (!p) return;

        if (_pool[idx] == null)
        {
            if (!dunePrefab) { Debug.LogWarning("DuneSpawner: dunePrefab is missing."); return; }
            _pool[idx] = Instantiate(dunePrefab, transform); // parented under spawner
        }

        var go = _pool[idx];
        go.SetActive(true);

        // Base transform from spawn point
        Vector3 pos = p.position;
        Quaternion rot = p.rotation;

        if (snapToGround)
        {
            Vector3 rayStart = pos + Vector3.up * raycastUp;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDown + raycastUp, groundMask, QueryTriggerInteraction.Ignore))
            {
                pos = hit.point + Vector3.up * baseYOffset;
            }
        }

        float yaw = YawJitter(rng);
        float scl = RandomRange(rng, uniformScaleRange.x, uniformScaleRange.y);

        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rot.eulerAngles.y + yaw, 0f));
        go.transform.localScale = new Vector3(scl, scl, scl);
    }

    float YawJitter(System.Random rng)
    {
        return Mathf.Lerp(yRotJitterRange.x, yRotJitterRange.y, (float)rng.NextDouble());
    }

    float RandomRange(System.Random rng, float a, float b)
    {
        return a + (float)rng.NextDouble() * (b - a);
    }
}
