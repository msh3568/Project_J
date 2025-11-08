
using UnityEngine;
using System.Collections;

public class EnemyRespawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float respawnDelay = 4f;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void Start()
    {
        SpawnEnemy();
    }

    public void Respawn()
    {
        StartCoroutine(RespawnCoroutine());
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab != null)
        {
            GameObject newEnemy = Instantiate(enemyPrefab, spawnPosition, spawnRotation);
            Enemy enemyComponent = newEnemy.GetComponent<Enemy>();
            if (enemyComponent != null)
            {
                enemyComponent.respawner = this;
            }
        }
    }
}
