using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemyScoreAllocator : MonoBehaviour
{
    [SerializeField]
    private int _killScore = 10;  // Varsayılan değer eklendi

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
            Debug.Log($"EnemyScoreAllocator: Successfully connected to HealthController OnDied event");
        }
        else
        {
            Debug.LogError("EnemyScoreAllocator could not find HealthController on the same GameObject!", this);
        }
    }
    
    // Called by bullet or other damage sources when hitting this enemy
    public void RegisterPlayerDamage(ulong playerId)
    {
        // Track the last player who damaged this enemy
        _lastDamageFromPlayerId = playerId;
        Debug.Log($"Enemy damaged by player {playerId}, setting as last damage source");
    }

    // Called when the enemy dies
    private void OnEnemyDied()
    {
        Debug.Log($"OnEnemyDied called for enemy: {gameObject.name}, last damage from player: {_lastDamageFromPlayerId}");
        
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("OnEnemyDied: Not running on server, skipping score allocation");
            return;
        }
        
        if (_scoreController != null)
        {
            // Default to host (0) if no player ID recorded
            if (_lastDamageFromPlayerId == 0)
            {
                Debug.Log($"No player ID recorded for damage, defaulting to host (0)");
            }
            
            // Allocate score to the player who killed this enemy
            _scoreController.AddScoreToPlayer(_lastDamageFromPlayerId, _killScore);
            Debug.Log($"Enemy killed by player {_lastDamageFromPlayerId}, adding {_killScore} score via ScoreController");
        }
        else
        {
            Debug.LogError("Cannot allocate score, ScoreController not found!", this);
            // Try to find the ScoreController again as a fallback
            _scoreController = FindObjectOfType<ScoreController>();
            if (_scoreController != null)
            {
                _scoreController.AddScoreToPlayer(_lastDamageFromPlayerId, _killScore);
                Debug.Log($"Found ScoreController and allocated {_killScore} points to player {_lastDamageFromPlayerId}");
            }
        }
    }
}
