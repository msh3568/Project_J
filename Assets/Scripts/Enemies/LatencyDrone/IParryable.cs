using UnityEngine;

public interface IParryable
{
    void OnParried(Vector2 reflectDirection);
    GameObject GetGameObject(); // To get the GameObject of the parryable entity
}