
using UnityEngine;
using System.Collections;

public class EnemyRespawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float respawnDelay = 4f;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Enemy currentEnemy;

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

    public void ResetImmediate()
    {
        StopAllCoroutines();
        if (currentEnemy != null)
        {
            Destroy(currentEnemy.gameObject);
        }
        SpawnEnemy();
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
            currentEnemy = newEnemy.GetComponent<Enemy>();
        }
    }
}
