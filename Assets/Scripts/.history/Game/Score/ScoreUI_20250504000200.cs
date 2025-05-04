using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance { get; private set; }

    private TMP_Text _scoreText;
    private int _currentScore = 0;
    
    [SerializeField]
    private string _scoreLabel = "Puan"; // Default label for score

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple ScoreUI instances found! Destroying duplicate.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Optional: DontDestroyOnLoad(gameObject); // Add if ScoreUI needs to persist scenes independently

        _scoreText = GetComponent<TMP_Text>();
        if (_scoreText == null)
        {
            Debug.LogError("ScoreUI could not find TMP_Text component", this);
        }
        else
        {
            // Initialize text
             _scoreText.text = $"{_scoreLabel}: 0";
        }
    }

    public void UpdateScore(ScoreController scoreController)
    {
        if (scoreController == null)
        {
            Debug.LogWarning("ScoreUI UpdateScore called with null ScoreController");
            return;
        }
        
        // Get the local client's ID
        ulong localClientId = NetworkManager.Singleton.LocalClientId; 
        
        // Get the score specifically for the local client from the controller
        int scoreForLocalPlayer = scoreController.GetPlayerScore(localClientId); 

        Debug.Log($"ScoreUI UpdateScore called. Controller ID: {scoreController.NetworkObjectId}, Local Client ID: {localClientId}, Score for Local Player: {scoreForLocalPlayer}");

        // Display the local player's score
        _currentScore = scoreForLocalPlayer; 

        // Log the score value *before* setting the text
        Debug.Log($"ScoreUI: Attempting to display score: {_currentScore}");

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
