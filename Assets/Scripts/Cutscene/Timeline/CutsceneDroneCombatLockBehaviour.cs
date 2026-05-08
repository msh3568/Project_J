using UnityEngine;
using UnityEngine.Playables;

public class CutsceneDroneCombatLockBehaviour : PlayableBehaviour
{
    private LatencyDroneWeak lockedDrone;
    private Rigidbody2D lockedRigidbody;
    private LineRenderer[] lockedTelegraphLines;
    private bool isLocked;

    public void Lock(LatencyDroneWeak drone)
    {
        if (!Application.isPlaying || drone == null)
            return;

        if (isLocked && lockedDrone == drone)
        {
            drone.SetCutsceneCombatSuppressed(true);
            if (lockedRigidbody != null)
            {
                lockedRigidbody.linearVelocity = Vector2.zero;
                lockedRigidbody.angularVelocity = 0f;
            }
            return;
        }

        Unlock();

        lockedDrone = drone;
        lockedRigidbody = drone.GetComponent<Rigidbody2D>();
        if (lockedRigidbody != null)
        {
            lockedRigidbody.linearVelocity = Vector2.zero;
            lockedRigidbody.angularVelocity = 0f;
        }

        lockedTelegraphLines = drone.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lockedTelegraphLines.Length; i++)
        {
            LineRenderer line = lockedTelegraphLines[i];
            if (line == null)
                continue;

            line.enabled = false;
        }

        drone.SetCutsceneCombatSuppressed(true);
        isLocked = true;
    }

    public void Unlock()
    {
        if (!isLocked)
            return;

        if (lockedDrone != null)
            lockedDrone.SetCutsceneCombatSuppressed(false);

        if (lockedRigidbody != null)
        {
            lockedRigidbody.linearVelocity = Vector2.zero;
            lockedRigidbody.angularVelocity = 0f;
        }

        if (lockedTelegraphLines != null)
        {
            for (int i = 0; i < lockedTelegraphLines.Length; i++)
            {
                if (lockedTelegraphLines[i] != null)
                    lockedTelegraphLines[i].enabled = false;
            }
        }

        lockedDrone = null;
        lockedRigidbody = null;
        lockedTelegraphLines = null;
        isLocked = false;
    }
}
