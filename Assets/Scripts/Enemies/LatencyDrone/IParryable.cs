using UnityEngine;

public interface IParryable
{
    // Called when the projectile is parried.
    // reflectDir: The direction the projectile should reflect towards (e.g., towards the enemy).
    void OnParried(Vector2 reflectDir);

    // Optional: Property to check if the projectile is currently parryable
    bool IsParryable { get; }
}
