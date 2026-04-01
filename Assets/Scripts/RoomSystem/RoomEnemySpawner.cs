using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomEnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomController room;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform spawnedParent;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnRoomActivated = true;
    [SerializeField, Min(0f)] private float spawnDelay = 0f;
    [SerializeField] private bool autoPlaySpawnPresentation = true;
    [SerializeField] private bool registerToEnemyClearConditions = true;
    [SerializeField] private bool addSpawnedObjectToRoomOptimization = true;

    [Header("Lifecycle")]
    [SerializeField] private bool removeRespawnOnCheckpointFromSpawnedInstance = true;
    [SerializeField] private bool destroySpawnedInstanceOnRoomReset = true;
    [SerializeField] private bool debugLogs = false;

    private GameObject spawnedInstance;
    private Coroutine spawnRoutine;

    public GameObject SpawnedInstance => spawnedInstance;

    private void Awake()
    {
        if (room == null)
            room = GetComponentInParent<RoomController>();

        if (spawnPoint == null)
            spawnPoint = transform;
    }

    private void OnEnable()
    {
        SubscribeToRoom();
    }

    private void OnDisable()
    {
        UnsubscribeFromRoom();
        CancelPendingSpawn();
    }

    public void SpawnNow()
    {
        CancelPendingSpawn();
        SpawnOrReuseInstance();
    }

    public void ResetSpawnedInstance()
    {
        CancelPendingSpawn();
        DestroySpawnedInstance();
    }

    private void SubscribeToRoom()
    {
        if (room == null)
            return;

        room.RoomActivated -= HandleRoomActivated;
        room.RoomActivated += HandleRoomActivated;
        room.RoomReset -= HandleRoomReset;
        room.RoomReset += HandleRoomReset;
    }

    private void UnsubscribeFromRoom()
    {
        if (room == null)
            return;

        room.RoomActivated -= HandleRoomActivated;
        room.RoomReset -= HandleRoomReset;
    }

    private void HandleRoomActivated(RoomController activatedRoom)
    {
        if (!spawnOnRoomActivated || activatedRoom != room)
            return;

        CancelPendingSpawn();

        if (spawnDelay <= 0f)
        {
            SpawnOrReuseInstance();
            return;
        }

        spawnRoutine = StartCoroutine(SpawnAfterDelay());
    }

    private void HandleRoomReset(RoomController resetRoom)
    {
        if (resetRoom != room)
            return;

        CancelPendingSpawn();

        if (!destroySpawnedInstanceOnRoomReset)
            return;

        DestroySpawnedInstance();
    }

    private IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(spawnDelay);
        spawnRoutine = null;
        SpawnOrReuseInstance();
    }

    private void SpawnOrReuseInstance()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("[RoomEnemySpawner] Enemy prefab is not assigned.", this);
            return;
        }

        if (spawnedInstance == null)
        {
            Transform parent = ResolveSpawnParent();
            Transform point = spawnPoint != null ? spawnPoint : transform;
            spawnedInstance = Instantiate(enemyPrefab, point.position, point.rotation, parent);

            if (removeRespawnOnCheckpointFromSpawnedInstance)
                RemoveRespawnOnCheckpointComponents(spawnedInstance);

            if (addSpawnedObjectToRoomOptimization)
                room?.RegisterOptimizedObject(spawnedInstance);

            RegisterSpawnedUnit(spawnedInstance);

            if (debugLogs)
                Debug.Log("[RoomEnemySpawner] Spawned '" + spawnedInstance.name + "' from " + name, this);
        }
        else
        {
            Transform point = spawnPoint != null ? spawnPoint : transform;
            spawnedInstance.transform.SetPositionAndRotation(point.position, point.rotation);
            if (!spawnedInstance.activeSelf)
                spawnedInstance.SetActive(true);
        }

        if (!autoPlaySpawnPresentation)
            return;

        EnemySpawnPresentation spawnPresentation = spawnedInstance.GetComponent<EnemySpawnPresentation>();
        if (spawnPresentation != null)
            spawnPresentation.BeginSpawnSequence();
    }

    private Transform ResolveSpawnParent()
    {
        if (spawnedParent != null)
            return spawnedParent;

        if (room != null)
            return room.transform;

        return null;
    }

    private void RegisterSpawnedUnit(GameObject instance)
    {
        if (!registerToEnemyClearConditions || instance == null || room == null)
            return;

        EnemyClearCondition[] conditions = room.GetComponentsInChildren<EnemyClearCondition>(true);
        if (conditions == null || conditions.Length == 0)
            return;

        RoomTrackedUnit[] units = instance.GetComponentsInChildren<RoomTrackedUnit>(true);
        for (int i = 0; i < units.Length; i++)
        {
            RoomTrackedUnit unit = units[i];
            if (unit == null)
                continue;

            unit.ResetClearedState();

            for (int j = 0; j < conditions.Length; j++)
            {
                if (conditions[j] != null)
                    conditions[j].RegisterUnit(unit);
            }
        }
    }

    private void UnregisterSpawnedUnit(GameObject instance)
    {
        if (!registerToEnemyClearConditions || instance == null || room == null)
            return;

        EnemyClearCondition[] conditions = room.GetComponentsInChildren<EnemyClearCondition>(true);
        if (conditions == null || conditions.Length == 0)
            return;

        RoomTrackedUnit[] units = instance.GetComponentsInChildren<RoomTrackedUnit>(true);
        for (int i = 0; i < units.Length; i++)
        {
            RoomTrackedUnit unit = units[i];
            if (unit == null)
                continue;

            for (int j = 0; j < conditions.Length; j++)
            {
                if (conditions[j] != null)
                    conditions[j].UnregisterUnit(unit);
            }
        }
    }

    private void RemoveRespawnOnCheckpointComponents(GameObject instance)
    {
        RespawnOnCheckpoint[] respawnComponents = instance.GetComponentsInChildren<RespawnOnCheckpoint>(true);
        for (int i = 0; i < respawnComponents.Length; i++)
        {
            RespawnOnCheckpoint respawnComponent = respawnComponents[i];
            if (respawnComponent == null)
                continue;

            Destroy(respawnComponent);
        }
    }

    private void DestroySpawnedInstance()
    {
        if (spawnedInstance == null)
            return;

        UnregisterSpawnedUnit(spawnedInstance);
        room?.UnregisterOptimizedObject(spawnedInstance);

        if (Application.isPlaying)
            Destroy(spawnedInstance);
        else
            DestroyImmediate(spawnedInstance);

        spawnedInstance = null;
    }

    private void CancelPendingSpawn()
    {
        if (spawnRoutine == null)
            return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }
}
