using System;

public interface IRoomClearUnit
{
    bool IsCleared { get; }
    event Action<IRoomClearUnit> Cleared;
}
