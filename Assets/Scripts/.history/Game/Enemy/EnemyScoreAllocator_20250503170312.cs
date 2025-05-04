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
    private ulong _lastDamageFromPlayerId = 0; // Track which player caused damage

    private void Awake()
    {
        _scoreController = FindObjectOfType<ScoreController>();
        if (_scoreController == null)
        {
            Debug.LogError("EnemyScoreAllocator could not find ScoreController in the scene!", this);
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
        // Track the last player who damaged this enemy
        _lastDamageFromPlayerId = playerId;
        Debug.Log($"Enemy {gameObject.name} damaged by player {playerId}");
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
            // Allocate score to the player who killed this enemy
            Debug.Log($"Enemy {gameObject.name} killed by player {_lastDamageFromPlayerId}, about to add {_killScore} score");
            _scoreController.AddScoreToPlayer(_lastDamageFromPlayerId, _killScore);
            Debug.Log($"Score {_killScore} added to player {_lastDamageFromPlayerId}");
            
            // Skoru hemen kontrol et
            int newScore = _scoreController.GetPlayerScore(_lastDamageFromPlayerId);
            Debug.Log($"Player {_lastDamageFromPlayerId} new score is: {newScore}");
        }
        else
        {
            Debug.LogError($"Cannot allocate score for {gameObject.name}, ScoreController not found!", this);
        }
    }
}
