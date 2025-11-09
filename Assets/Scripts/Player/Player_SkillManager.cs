using UnityEngine;

public class Player_SkillManager : MonoBehaviour
{
    public Skill_Baldo baldo {  get; private set; }

    private void Awake()
    {
        baldo = GetComponentInChildren<Skill_Baldo>();
    }

}
