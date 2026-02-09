using System;

public interface IRoomClearCondition
{
    bool IsComplete { get; }
    event Action<IRoomClearCondition> ConditionStateChanged;
    void Initialize(RoomController room);
    void ResetCondition();
}
