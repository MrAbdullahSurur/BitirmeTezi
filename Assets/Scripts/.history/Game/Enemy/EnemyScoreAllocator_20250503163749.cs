using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemyScoreAllocator : MonoBehaviour
{
    [SerializeField]
    private int _killScore;

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
        Debug.Log($"Enemy damaged by player {playerId}");
    }

    // Called when the enemy dies
    private void OnEnemyDied()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }
        
        if (_scoreController != null)
        {
            // Allocate score to the player who killed this enemy
            _scoreController.AddScoreToPlayer(_lastDamageFromPlayerId, _killScore);
            Debug.Log($"Enemy killed by player {_lastDamageFromPlayerId}, adding {_killScore} score.");
        }
        else
        {
            Debug.LogError("Cannot allocate score, ScoreController not found!", this);
        }
    }
}
