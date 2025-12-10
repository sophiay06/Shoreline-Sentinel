using UnityEngine;

public class SlimeVRDebug : MonoBehaviour
{
    public Transform leftFoot;
    public Transform rightFoot;
    public Transform waist;

    void Update()
    {
        Debug.Log($"LF {leftFoot.position}  RF {rightFoot.position}  W {waist.position}");
    }
}
