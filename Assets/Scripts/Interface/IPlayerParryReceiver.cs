using UnityEngine;

public interface IPlayerParryReceiver
{
    bool TryGetParryLaunchDirection(out Vector2 direction);
}
