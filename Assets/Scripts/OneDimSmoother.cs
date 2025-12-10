using UnityEngine;

[System.Serializable]
public class OneDimSmoother
{
    [Range(0f, 1f)] public float value01; // smoothed output
    [Range(0f, 0.5f)] public float deadZone = 0.04f; // ignore tiny wiggles
    [Range(0f, 0.2f)] public float hysteresis = 0.03f; // stickiness band
    [Range(0.01f, 1.0f)] public float emaTau = 0.18f; // smoothing time (sec)
    [Range(0.05f, 3.0f)] public float maxDeltaPerSec = 0.6f; // rate cap

    float _latch; // internal target with hysteresis

    public float Step(float raw01, float dt)
    {
        raw01 = Mathf.Clamp01(raw01);
        if (dt <= 0f) return value01;

        // Dead-zone / hysteresis around the latch
        float diff = raw01 - _latch;
        float mag  = Mathf.Abs(diff);

        float enterThresh = Mathf.Max(0f, deadZone);
        float leaveThresh = Mathf.Max(0f, deadZone - hysteresis);

        if (mag > enterThresh)
        {
            // outside band → move latch to new value
            _latch = raw01;
        }
        else if (mag < leaveThresh)
        {
            // deeply inside → keep latch
        }
        // between enter/leave → “sticky” zone

        // EMA toward latch
        float alpha = Mathf.Clamp01(dt / Mathf.Max(emaTau, 0.0001f));
        float ema   = Mathf.Lerp(value01, _latch, alpha);

        // Rate limit
        float maxStep = maxDeltaPerSec * dt;
        value01 = Mathf.MoveTowards(value01, ema, maxStep);
        return value01;
    }

    public void Snap(float raw01)
    {
        _latch = value01 = Mathf.Clamp01(raw01);
    }
}
