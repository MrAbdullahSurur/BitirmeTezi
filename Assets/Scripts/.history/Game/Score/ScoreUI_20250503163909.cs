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
        
        // Display player's own score only
        _currentScore = scoreController.Score;
        _scoreText.text = $"{_scoreLabel}: {_currentScore}";
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
