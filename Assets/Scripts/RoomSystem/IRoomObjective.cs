using System;

public interface IRoomObjective
{
    bool IsDestroyed { get; }
    event Action<IRoomObjective> Destroyed;
}
