using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ScoreUI : MonoBehaviour
{
    private TMP_Text _scoreText;
    private int _currentScore = 0;
    
    [SerializeField]
    private string _scoreLabel = "Puan"; // Default label for score

    private void Awake()
    {
        _scoreText = GetComponent<TMP_Text>();
        if (_scoreText == null)
        {
            Debug.LogError("ScoreUI could not find TMP_Text component", this);
        }
    }

    public void UpdateScore(ScoreController scoreController)
    {
        if (scoreController == null)
        {
            Debug.LogWarning("ScoreUI UpdateScore called with null ScoreController");
            return;
        }
        
        ulong localClientId = scoreController.IsSpawned ? scoreController.OwnerClientId : NetworkManager.Singleton.LocalClientId; // Safely get local client ID
        int scoreFromController = scoreController.Score; // Get score via property

        Debug.Log($"ScoreUI UpdateScore called. Controller ID: {scoreController.NetworkObjectId}, Local Client ID: {localClientId}, Score from Controller Property: {scoreFromController}");

        // Display player's own score only
        _currentScore = scoreFromController; // Use the retrieved score
        if (_scoreText != null)
        {
            _scoreText.text = $"{_scoreLabel}: {_currentScore}";
            Debug.Log($"ScoreUI Updated Text: {_scoreText.text}");
        }
        else
        {
             Debug.LogError("ScoreUI _scoreText is null!");
        }
    }
    
    // Get the current score being displayed
    public int GetCurrentScore()
    {
        return _currentScore;
    }
    
    // Get the displayed score text
    public string GetScoreText()
    {
        return _scoreText != null ? _scoreText.text : $"{_scoreLabel}: 0";
    }
}
