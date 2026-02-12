// Assets/Scripts/Mapping/MappingMath.cs
using UnityEngine;

namespace MappingTool
{
    public enum CurvePreset
    {
        Linear,
        EaseInOut,
        EaseOut
    }

    public static class MappingMath
    {
        public static float Remap01(float x, float inMin, float inMax)
        {
            if (Mathf.Approximately(inMin, inMax)) return 0f;
            return Mathf.Clamp01((x - inMin) / (inMax - inMin));
        }

        public static float ApplyPreset(float t, CurvePreset preset)
        {
            t = Mathf.Clamp01(t);

            switch (preset)
            {
                case CurvePreset.EaseInOut:
                    // Smoothstep
                    return t * t * (3f - 2f * t);

                case CurvePreset.EaseOut:
                    // Quadratic ease out
                    return 1f - (1f - t) * (1f - t);

                default:
                    return t;
            }
        }

        public static float Lerp(float outMin, float outMax, float t)
        {
            return Mathf.Lerp(outMin, outMax, Mathf.Clamp01(t));
        }

        /// <summary>
        /// One-pole low-pass filter.
        /// smoothing in [0..1] where higher = smoother/slower (e.g., 0.8-0.98).
        /// </summary>
        public static float LowPass(float prev, float current, float smoothing)
        {
            // smoothing: 0 = no smoothing (snappy), 0.95 = very smooth
            smoothing = Mathf.Clamp01(smoothing);
            float alpha = 1f - smoothing;              // alpha: 1 = snappy, small = smooth
            return Mathf.Lerp(prev, current, alpha);   // move a small step toward current
        }


        public static float ApplyDeadzone(float x, float center, float deadzone)
        {
            if (deadzone <= 0f) return x;
            return (Mathf.Abs(x - center) <= deadzone) ? center : x;
        }
    }
}
