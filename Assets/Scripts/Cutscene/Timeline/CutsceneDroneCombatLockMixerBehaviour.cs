using UnityEngine;
using UnityEngine.Playables;

public class CutsceneDroneCombatLockMixerBehaviour : PlayableBehaviour
{
    private const string EnemyRoomName = "Room_Trigger_A_Enemy";
    private const string TargetDroneName = "LatencyDroneStrong2";

    private CutsceneDroneCombatLockBehaviour activeLock;
    private LatencyDroneWeak fallbackDrone;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        LatencyDroneWeak drone = playerData as LatencyDroneWeak;
        if (drone == null)
            drone = ResolveFallbackDrone();

        if (drone == null)
        {
            UnlockActiveLock();
            return;
        }

        CutsceneDroneCombatLockBehaviour lockBehaviour = null;
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            if (playable.GetInputWeight(i) <= 0f)
                continue;

            ScriptPlayable<CutsceneDroneCombatLockBehaviour> inputPlayable =
                (ScriptPlayable<CutsceneDroneCombatLockBehaviour>)playable.GetInput(i);
            lockBehaviour = inputPlayable.GetBehaviour();
            break;
        }

        if (lockBehaviour == null)
        {
            UnlockActiveLock();
            return;
        }

        if (activeLock != null && activeLock != lockBehaviour)
            activeLock.Unlock();

        activeLock = lockBehaviour;
        activeLock.Lock(drone);
    }

    public override void OnGraphStop(Playable playable)
    {
        UnlockActiveLock();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        UnlockActiveLock();
    }

    private void UnlockActiveLock()
    {
        if (activeLock != null)
            activeLock.Unlock();

        activeLock = null;
    }

    private LatencyDroneWeak ResolveFallbackDrone()
    {
        if (fallbackDrone != null)
            return fallbackDrone;

        LatencyDroneWeak[] drones = Object.FindObjectsByType<LatencyDroneWeak>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < drones.Length; i++)
        {
            LatencyDroneWeak drone = drones[i];
            if (drone == null || NormalizeName(drone.name) != TargetDroneName)
                continue;

            if (!IsUnderNamedParent(drone.transform, EnemyRoomName))
                continue;

            fallbackDrone = drone;
            return fallbackDrone;
        }

        return null;
    }

    private static bool IsUnderNamedParent(Transform transform, string parentName)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == parentName)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", string.Empty);
    }
}
