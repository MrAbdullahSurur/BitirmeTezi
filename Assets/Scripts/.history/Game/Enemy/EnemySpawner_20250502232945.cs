using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField]
    private GameObject _enemyPrefab;

    [SerializeField]
    private float _minimumSpawnTime;

    [SerializeField]
    private float _maximumSpawnTime;

    private float _timeUntilSpawn;

    void Start()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        SetTimeUntilSpawn();
    }

    void Update()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        _timeUntilSpawn -= Time.deltaTime;

        if (_timeUntilSpawn <= 0)
        {
            GameObject enemyInstance = Instantiate(_enemyPrefab, transform.position, Quaternion.identity);
            
            NetworkObject networkObject = enemyInstance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn(true);
            }
            else
            {
                Debug.LogError("Spawn edilen Enemy prefabında NetworkObject bileşeni bulunamadı!", enemyInstance);
                Destroy(enemyInstance);
            }
            
            SetTimeUntilSpawn();
        }
    }

    private void SetTimeUntilSpawn()
    {
        _timeUntilSpawn = Random.Range(_minimumSpawnTime, _maximumSpawnTime);
    }
}
