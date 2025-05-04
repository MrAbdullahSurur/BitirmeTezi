using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreUI : MonoBehaviour
{
    private TMP_Text _scoreText;
    private int _currentScore = 0;

    private void Awake()
    {
        _scoreText = GetComponent<TMP_Text>();
    }

    public void UpdateScore(ScoreController scoreController)
    {
        _currentScore = scoreController.Score;
        _scoreText.text = $"Score: {_currentScore}";
    }
    
    // Get the current score being displayed
    public int GetCurrentScore()
    {
        return _currentScore;
    }
    
    // Get the displayed score text
    public string GetScoreText()
    {
        return _scoreText != null ? _scoreText.text : "Score: 0";
    }
}
