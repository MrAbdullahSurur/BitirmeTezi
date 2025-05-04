using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    [SerializeField]
    private float _timeToWaitBeforeExit;

    [SerializeField]
    private SceneController _sceneController;

    [Header("Game Over UI")]
    [SerializeField]
    private GameObject _gameOverPanel;
    [SerializeField]
    private TMP_Text _gameOverText;
    [SerializeField]
    private Button _exitButton;

    private ScoreController _scoreController;
    
    // Track player deaths
    private NetworkVariable<int> _deadPlayersCount = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private bool _gameEnded = false;

    private void Awake()
    {
        _scoreController = FindObjectOfType<ScoreController>();
        
        // Try to find GameOverPanel if not assigned
        if (_gameOverPanel == null)
        {
            _gameOverPanel = GameObject.FindWithTag("GameOverPanel");
            if (_gameOverPanel == null)
            {
                Debug.LogWarning("GameOverPanel not assigned and could not be found by tag! Game over UI won't work.");
            }
            else
            {
                Debug.Log("Found GameOverPanel by tag");
            }
        }
        
        if (_gameOverPanel != null)
        {
            Debug.Log("GameOverPanel found, setting to inactive initially");
            _gameOverPanel.SetActive(false);
            
            if (_exitButton == null)
            {
                // Try to find exit button in the panel
                _exitButton = _gameOverPanel.GetComponentInChildren<Button>();
                if (_exitButton != null)
                {
                    Debug.Log("Found ExitButton in GameOverPanel");
                }
            }
            
            if (_exitButton != null)
            {
                _exitButton.onClick.AddListener(OnExitButtonClicked);
                Debug.Log("Exit button click listener added");
            }
            else
            {
                Debug.LogWarning("ExitButton not found in GameOverPanel!");
            }
            
            if (_gameOverText == null)
            {
                _gameOverText = _gameOverPanel.GetComponentInChildren<TMP_Text>();
                if (_gameOverText != null)
                {
                    Debug.Log("Found GameOverText in GameOverPanel");
                }
                else
                {
                    Debug.LogWarning("GameOverText not found in GameOverPanel!");
                }
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to player death count changes
        _deadPlayersCount.OnValueChanged += OnDeadPlayersCountChanged;
    }

    public override void OnNetworkDespawn()
    {
        _deadPlayersCount.OnValueChanged -= OnDeadPlayersCountChanged;
    }

    private void OnDeadPlayersCountChanged(int previous, int current)
    {
        Debug.Log($"Dead players count updated: {previous} -> {current}");
        
        // Check on both server and client for debugging
        if (current >= 2 && !_gameEnded)
        {
            Debug.Log("Two players dead, showing game over!");
            ShowGameOver();
        }
        else
        {
            Debug.Log($"Not showing game over yet. Players dead: {current}, game ended: {_gameEnded}");
        }
    }

    public void OnPlayerDied()
    {
        if (!IsServer) return;
        
        _deadPlayersCount.Value++;
        Debug.Log($"Player died! Dead players count: {_deadPlayersCount.Value}");
        
        if (_deadPlayersCount.Value >= 2)
        {
            // All players are dead, initiate game over
            Debug.Log("All players dead, ending game");
            if (!_gameEnded)
            {
                _gameEnded = true;
                // Delayed return to main menu only if we're not showing the game over screen
                if (_gameOverPanel == null)
                {
                    Invoke(nameof(EndGame), _timeToWaitBeforeExit);
                }
            }
        }
    }

    private void ShowGameOver()
    {
        Debug.Log("ShowGameOver called!");
        _gameEnded = true;
        
        if (_gameOverPanel != null)
        {
            Debug.Log("Activating GameOverPanel");
            // Make sure it's activated on the main thread
            if (IsClient)
            {
                // Force it to appear on screen
                Canvas canvas = _gameOverPanel.GetComponentInParent<Canvas>();
                if (canvas != null && !canvas.enabled)
                {
                    canvas.enabled = true;
                    Debug.Log("Canvas enabled");
                }
                
                _gameOverPanel.SetActive(true);
                
                int finalScore = 0;
                if (_scoreController != null)
                {
                    finalScore = _scoreController.Score;
                }
                
                if (_gameOverText != null)
                {
                    _gameOverText.text = $"Game Over\nScore = {finalScore}";
                    Debug.Log($"Set GameOverText to: Game Over Score = {finalScore}");
                }
                else
                {
                    Debug.LogError("GameOverText is null when trying to show score!");
                }
                
                // Make sure the panel appears in front
                if (_gameOverPanel.transform is RectTransform rectTransform)
                {
                    rectTransform.SetAsLastSibling();
                    Debug.Log("Set GameOverPanel as last sibling for visibility");
                }
            }
            else
            {
                Debug.Log("Not on client, skipping UI update");
            }
        }
        else
        {
            Debug.LogError("GameOverPanel is null when trying to show game over!");
        }
    }

    private void OnExitButtonClicked()
    {
        _gameOverPanel.SetActive(false);
        EndGame();
    }

    private void EndGame()
    {
        // In a networked game, only the server should load scenes
        if (IsServer)
        {
            _sceneController.LoadScene("Main Menu");
        }
    }
}