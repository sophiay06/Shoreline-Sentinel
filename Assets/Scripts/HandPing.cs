using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class HandPing : MonoBehaviour
{
    void Update()
    {
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        if (loader == null) return;

        var hands = loader.GetLoadedSubsystem<XRHandSubsystem>();
        if (hands == null) return;

        Debug.Log($"Hands tracked?  L:{hands.leftHand.isTracked}  R:{hands.rightHand.isTracked}");
    }
}
