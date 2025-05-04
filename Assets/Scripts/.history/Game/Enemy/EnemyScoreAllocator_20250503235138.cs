using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemyScoreAllocator : MonoBehaviour
{
    [SerializeField]
    private int _killScore = 10; // Varsayılan değer ekledim

    private ScoreController _scoreController;
    private HealthController _healthController;
    private List<ulong> _damageSourcePlayerIds = new List<ulong>(); // Track all players who damaged this enemy

    private void Awake()
    {
        // Find the single ScoreController instance in the scene
        _scoreController = FindObjectOfType<ScoreController>();
        if (_scoreController == null)
        {
            Debug.LogError("EnemyScoreAllocator could not find the single ScoreController instance in the scene!", this);
        }
        
        _healthController = GetComponent<HealthController>();
        if (_healthController != null)
        {
            // Listen for enemy death
            _healthController.OnDied.AddListener(OnEnemyDied);
            Debug.Log($"EnemyScoreAllocator on {gameObject.name} subscribed to HealthController.OnDied event");
        }
        else
        {
            Debug.LogError("EnemyScoreAllocator could not find HealthController on the same GameObject!", this);
        }
    }
    
    private void OnDestroy()
    {
        // Abonelikten çık
        if (_healthController != null)
        {
            _healthController.OnDied.RemoveListener(OnEnemyDied);
        }
    }
    
    // Called by bullet or other damage sources when hitting this enemy
    public void RegisterPlayerDamage(ulong playerId)
    {
        // Track the last player who damaged this enemy by adding to list
        // Avoid adding duplicates consecutively if needed, but for now just add
        _damageSourcePlayerIds.Add(playerId);
        Debug.Log($"Enemy {gameObject.name} damaged by player {playerId}. Damage list count: {_damageSourcePlayerIds.Count}");
    }

    // Called when the enemy dies
    private void OnEnemyDied()
    {
        Debug.Log($"OnEnemyDied called for {gameObject.name}. IsServer: {(NetworkManager.Singleton != null ? NetworkManager.Singleton.IsServer : false)}");
        
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"OnEnemyDied skipped for {gameObject.name} - not on server");
            return;
        }
        
        if (_scoreController != null)
    {
            // Allocate score to the player who dealt the killing blow (last in the list)
            if (_damageSourcePlayerIds.Count > 0)
            {
                ulong killingPlayerId = _damageSourcePlayerIds[_damageSourcePlayerIds.Count - 1]; // Get the last player ID from the list
                Debug.Log($"EnemyScoreAllocator: Attempting to add score {_killScore} to player with ID: {killingPlayerId} (last damage source)"); 
                _scoreController.AddScoreToPlayer(killingPlayerId, _killScore);
            
                // Skoru hemen kontrol et (opsiyonel)
                // int newScore = _scoreController.GetPlayerScore(killingPlayerId);
                // Debug.Log($"Player {killingPlayerId} new score check: {newScore}");
            }
            else
            {
                 Debug.LogWarning($"Enemy {gameObject.name} died but no player damage was registered. No score allocated.", this);
            }
        }
        else
        {
            Debug.LogError($"Cannot allocate score for {gameObject.name}, ScoreController not found!", this);
        }
    }
}
