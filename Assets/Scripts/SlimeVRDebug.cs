using UnityEngine;

public class SlimeVRDebug : MonoBehaviour
{
    public Transform leftAnkle;
    public Transform rightAnkle;
    public Transform rightThigh;

    void Update()
    {
        Debug.Log($"RIGHT ANKLE instanceID {rightAnkle.GetInstanceID()}");
        if (leftAnkle != null)
            Debug.Log($"LEFT ANKLE world: {leftAnkle.position}  local: {leftAnkle.localPosition}");

        if (rightAnkle != null)
            Debug.Log($"RIGHT ANKLE world: {rightAnkle.position}  local: {rightAnkle.localPosition}");

        if (rightThigh != null)
            Debug.Log($"RIGHT THIGH world: {rightThigh.position}  local: {rightThigh.localPosition}");
    }
}
