using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemyScoreAllocator : MonoBehaviour
{
    [SerializeField]
    private int _killScore;

    private ScoreController _scoreController;

    private void Awake()
    {
        _scoreController = FindObjectOfType<ScoreController>();
        if (_scoreController == null)
        {
            Debug.LogError("EnemyScoreAllocator could not find ScoreController in the scene!", this);
        }
    }

    public void AllocateScore()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }
        
        if (_scoreController != null)
        {
            _scoreController.AddScore(_killScore);
            Debug.Log($"Enemy killed, adding {_killScore} score on server.");
        }
        else
        {
            Debug.LogError("Cannot allocate score, ScoreController not found!", this);
        }
    }
}
