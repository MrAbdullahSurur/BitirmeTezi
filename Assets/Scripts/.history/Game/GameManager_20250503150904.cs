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
    
    // Track player deaths and connected players
    private NetworkVariable<int> _deadPlayersCount = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private NetworkVariable<int> _connectedPlayersCount = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
        
    private bool _gameEnded = false;

    private void Awake()
    {
        _scoreController = FindObjectOfType<ScoreController>();
        
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(false);
            
            if (_exitButton != null)
            {
                _exitButton.onClick.AddListener(OnExitButtonClicked);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to player death count changes
        _deadPlayersCount.OnValueChanged += OnDeadPlayersCountChanged;
        
        // Log initial state
        Debug.Log($"GameManager NetworkSpawn: IsServer={IsServer}, IsClient={IsClient}, DeadPlayers={_deadPlayersCount.Value}");
        
        // Log state of GameOverPanel
        if (_gameOverPanel == null)
        {
            Debug.LogError("GameManager: _gameOverPanel is not assigned in Inspector! Game over UI will not work.");
        }
        else
        {
            Debug.Log($"GameManager: GameOverPanel reference is valid. Currently active: {_gameOverPanel.activeSelf}");
        }
        
        // Initialize connected players count on server
        if (IsServer)
        {
            _connectedPlayersCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
            Debug.Log($"Initial connected players count: {_connectedPlayersCount.Value}");
        }
    }

    public override void OnNetworkDespawn()
    {
        _deadPlayersCount.OnValueChanged -= OnDeadPlayersCountChanged;
    }

    private void OnDeadPlayersCountChanged(int previous, int current)
    {
        Debug.Log($"Dead players count updated: {previous} -> {current}. IsClient={IsClient}, IsServer={IsServer}, GameEnded={_gameEnded}, Connected Players={_connectedPlayersCount.Value}");
        
        // Check if all players are dead (works for both single and multiplayer)
        if (!_gameEnded && current > 0 && current >= _connectedPlayersCount.Value)
        {
            Debug.Log($"All players are dead ({current}/{_connectedPlayersCount.Value}) - showing game over screen");
            ShowGameOver();
        }
    }

    public void OnPlayerDied()
    {
        if (!IsServer) 
        {
            Debug.Log("GameManager.OnPlayerDied called on non-server instance - ignoring");
            return;
        }
        
        _deadPlayersCount.Value++;
        Debug.Log($"Player died! Dead players count: {_deadPlayersCount.Value}/{_connectedPlayersCount.Value}, IsServer={IsServer}, IsClient={IsClient}");
        
        // Check if all players are dead (works for both single and multiplayer)
        if (_deadPlayersCount.Value >= _connectedPlayersCount.Value)
        {
            Debug.Log($"All players dead ({_deadPlayersCount.Value}/{_connectedPlayersCount.Value}), ending game");
            if (!_gameEnded)
            {
                _gameEnded = true;
                // Delayed return to main menu only if we're not showing the game over screen
                if (_gameOverPanel == null)
                {
                    Debug.LogWarning("GameOverPanel is null, returning to main menu after delay");
                    Invoke(nameof(EndGame), _timeToWaitBeforeExit);
                }
                else
                {
                    Debug.Log("Showing game over UI");
                    ShowGameOver();
                }
            }
        }
    }

    private void ShowGameOver()
    {
        _gameEnded = true;
        
        if (_gameOverPanel != null)
        {
            Debug.Log($"Activating GameOverPanel. Currently active: {_gameOverPanel.activeSelf}");
            _gameOverPanel.SetActive(true);
            
            int finalScore = 0;
            if (_scoreController != null)
            {
                finalScore = _scoreController.Score;
                Debug.Log($"Final score: {finalScore}");
            }
            else
            {
                Debug.LogWarning("ScoreController not found, showing score 0");
            }
            
            if (_gameOverText != null)
            {
                _gameOverText.text = $"Game Over\nScore = {finalScore}";
                Debug.Log($"Set GameOverText to: {_gameOverText.text}");
            }
            else
            {
                Debug.LogError("GameOverText component reference is missing!");
            }
        }
        else
        {
            Debug.LogError("Cannot show game over - _gameOverPanel is null!");
        }
    }

    private void OnExitButtonClicked()
    {
        Debug.Log("Exit button clicked!");
        _gameOverPanel.SetActive(false);
        EndGame();
    }

    private void EndGame()
    {
        // In a networked game, only the server should load scenes
        Debug.Log($"EndGame called. IsServer={IsServer}");
        if (IsServer)
        {
            Debug.Log("Loading Main Menu scene");
            _sceneController.LoadScene("Main Menu");
        }
    }
}