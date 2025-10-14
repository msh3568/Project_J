using UnityEngine;

public class Skill_Base : MonoBehaviour
{
    [Header("General details")]
    [SerializeField] private float cooldown;
    private float lastTimeUsed;
    

    protected virtual void awake()
    {
        lastTimeUsed = lastTimeUsed - cooldown;
    }

    public bool CanUseSkill()
    {
        if (OnCooldown())
        {
            return false;
        }

        return true;
    }


    public float GetCooldownTimer()
    {
        return Mathf.Max(0, lastTimeUsed + cooldown - Time.time);
    }

    private bool OnCooldown() => Time.time < lastTimeUsed + cooldown;
    public void SetSkillOnCooldown() => lastTimeUsed = Time.time;
    public void ResetCooldownBy(float cooldownReudction) => lastTimeUsed = lastTimeUsed + cooldownReudction;
    public void ResetCooldown() => lastTimeUsed = Time.time;

}
