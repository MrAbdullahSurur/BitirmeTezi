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
            
            // Düşmanın EnemyScoreAllocator bileşenini kontrol et
            EnemyScoreAllocator scoreAllocator = enemyInstance.GetComponent<EnemyScoreAllocator>();
            if (scoreAllocator == null)
            {
                Debug.LogWarning("Düşman prefabında EnemyScoreAllocator bileşeni yok. Puan takibi çalışmayabilir!", enemyInstance);
                // Eksikse ekleyelim
                scoreAllocator = enemyInstance.AddComponent<EnemyScoreAllocator>();
                Debug.Log("EnemyScoreAllocator bileşeni düşmana eklendi.");
            }
            
            // Düşmanın HealthController bileşenini kontrol et
            HealthController healthController = enemyInstance.GetComponent<HealthController>();
            if (healthController == null)
            {
                Debug.LogWarning("Düşman prefabında HealthController bileşeni yok!", enemyInstance);
            }
            else
            {
                Debug.Log($"Düşman oluşturuldu, HealthController mevcut. Current Health: {healthController.RemainingHealthPercentage}");
            }
            
            NetworkObject networkObject = enemyInstance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn(true);
                Debug.Log($"Düşman ID:{networkObject.NetworkObjectId} ağda spawn edildi.");
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
