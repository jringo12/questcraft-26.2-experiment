using System.Collections;
using UnityEngine;
using UnityEngine.XR;

/// H1 FIX: on first launch, move the world-locked MainMenu in front of the
/// player's head so it is actually findable. The authored menu position sits
/// ~5.6 m from spawn, 3-38 deg above eye level, inside the QCWorld diorama
/// volume, so on first launch it is easy to miss or see occluded.
/// After the first successful recenter the PlayerPrefs flag is set and the
/// menu keeps its normal world-locked behaviour on every later launch.
/// If no valid tracked head pose appears, the menu is left at its authored
/// position and the flag is NOT set, so the recenter retries next launch.
public class MenuRecenter : MonoBehaviour
{
    private const string RecenteredKey = "QCMainMenuRecentered";
    private const float RecenterDistance = 4.5f;
    private const float HeightAboveHead = 0.5f;
    private const float SettleDelay = 1f;
    private const float PosePollInterval = 0.5f;
    private const int MaxPosePolls = 10;

    private void Start()
    {
        if (PlayerPrefs.GetInt(RecenteredKey, 0) == 1)
            return;

        StartCoroutine(RecenterWhenTrackingReady());
    }

    private IEnumerator RecenterWhenTrackingReady()
    {
        yield return new WaitForSeconds(SettleDelay);

        for (int i = 0; i < MaxPosePolls; i++)
        {
            if (TryGetHeadPose(out Vector3 headPos, out Quaternion headRot))
            {
                PlaceMenuInFrontOf(headPos, headRot);
                PlayerPrefs.SetInt(RecenteredKey, 1);
                PlayerPrefs.Save();
                yield break;
            }
            yield return new WaitForSeconds(PosePollInterval);
        }

        Debug.LogWarning("QCXR: MenuRecenter skipped, no valid head pose. Will retry next launch.");
    }

    /// Manual recenter action: callable from UI or other scripts. Always
    /// recenters (ignores the first-launch flag) when a valid pose exists.
    public void RecenterMenu()
    {
        if (TryGetHeadPose(out Vector3 headPos, out Quaternion headRot))
        {
            PlaceMenuInFrontOf(headPos, headRot);
            PlayerPrefs.SetInt(RecenteredKey, 1);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogWarning("QCXR: MenuRecenter failed, no valid head pose.");
        }
    }

    private void PlaceMenuInFrontOf(Vector3 headPos, Quaternion headRot)
    {
        Vector3 forward = headRot * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 menuPos = headPos + forward * RecenterDistance;
        menuPos.y = headPos.y + HeightAboveHead;

        transform.position = menuPos;
        // LookAt with a same-height target keeps the menu upright (no pitch);
        // the canvas front (+Z) then faces the player.
        transform.LookAt(new Vector3(headPos.x, menuPos.y, headPos.z));
        Debug.Log("QCXR: MainMenu recentered to " + menuPos + " facing the player.");
    }

    private static bool TryGetHeadPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        // Primary source: the XR head device, gated on real position tracking.
        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (head.isValid &&
            head.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState trackingState) &&
            (trackingState & InputTrackingState.Position) != 0 &&
            head.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
            head.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
        {
            return true;
        }

        // Fallback: the Main Camera (this scene drives it from the same XR
        // head pose via TrackedPoseDriver). Only accept it if tracking has
        // actually moved it: its authored local position is (0,0,0), so a
        // non-zero local position means a real head pose is feeding it.
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Transform t = mainCamera.transform;
            if (t.localPosition.sqrMagnitude > 0.01f)
            {
                position = t.position;
                rotation = t.rotation;
                return true;
            }
        }

        return false;
    }
}
