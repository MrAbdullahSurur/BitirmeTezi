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
        
        // Find and cache ScoreController reference
        _scoreController = FindObjectOfType<ScoreController>();
        if (_scoreController == null)
        {
            Debug.LogError("GameManager: ScoreController not found in scene!");
        }
        else
        {
            Debug.Log($"GameManager: Found ScoreController with current score: {_scoreController.Score}");
        }
        
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
        
        // Game ending logic:
        // If 2 players present, only end when BOTH are dead (count == 2)
        // If 1 player, end when that player dies (count == 1)
        
        if (!_gameEnded && _connectedPlayersCount.Value > 0)
        {
            bool allPlayersDead = (current == _connectedPlayersCount.Value);
            
            Debug.Log($"Dead players: {current}, Total connected: {_connectedPlayersCount.Value}, All dead? {allPlayersDead}");
            
            if (allPlayersDead)
            {
                Debug.Log($"All players are dead ({current}/{_connectedPlayersCount.Value}) - showing game over screen");
                ShowGameOver();
            }
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
        
        // Game ending is handled in OnDeadPlayersCountChanged
    }

    private void ShowGameOver()
    {
        _gameEnded = true;
        
        if (_gameOverPanel != null)
        {
            Debug.Log($"Activating GameOverPanel. Currently active: {_gameOverPanel.activeSelf}");
            _gameOverPanel.SetActive(true);
            
            if (_gameOverText != null)
            {
                // Ensure we have a reference to the ScoreController
                if (_scoreController == null)
                {
                    _scoreController = FindObjectOfType<ScoreController>();
                }
                
                // Find ScoreUI component to get the exact same score format
                ScoreUI scoreUI = FindObjectOfType<ScoreUI>();
                
                if (_scoreController != null)
                {
                    // Get the current score from the NetworkVariable
                    int finalScore = _scoreController.Score;
                    Debug.Log($"Final score from ScoreController: {finalScore}");
                    
                    // Use ScoreUI format if available, otherwise use default format
                    if (scoreUI != null)
                    {
                        // Get a temporary TextMeshProUGUI to see what format ScoreUI uses
                        TMPro.TMP_Text tempText = new GameObject().AddComponent<TMPro.TMP_Text>();
                        scoreUI.UpdateScore(_scoreController);
                        
                        // Apply same format to game over text (replacing "Score:" with "Game Over\nScore =")
                        string scoreText = $"Game Over\n{_scoreController.Score}";
                        _gameOverText.text = scoreText;
                        
                        // Clean up temporary object
                        Destroy(tempText.gameObject);
                    }
                    else
                    {
                        // Default format if ScoreUI not found
                        _gameOverText.text = $"Game Over\nScore = {finalScore}";
                    }
                    
                    Debug.Log($"Set GameOverText to: {_gameOverText.text}");
                }
                else
                {
                    Debug.LogWarning("ScoreController not found, showing score 0");
                    _gameOverText.text = "Game Over\nScore = 0";
                }
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
        
        // Quit the application instead of loading main menu
        Debug.Log("Quitting application");
        Application.Quit();
        
        // In Unity Editor, this will not actually quit, so also stop play mode for testing
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
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