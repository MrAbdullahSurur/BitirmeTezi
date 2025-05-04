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
    [SerializeField]
    private ScoreUI _scoreUI; // Reference to the ScoreUI component

    private ScoreController _scoreController;
    
    // Track player deaths and connected players
    private NetworkVariable<int> _deadPlayersCount = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private NetworkVariable<int> _connectedPlayersCount = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
        
    private bool _gameEnded = false;
    
    // Dictionary to track which players have died
    private Dictionary<ulong, bool> _playerDeathStatus = new Dictionary<ulong, bool>();

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
        
        // Initialize connected players count and player death status on server
        if (IsServer)
        {
            // Reset death counters for fresh start
            _deadPlayersCount.Value = 0;
            _playerDeathStatus.Clear();
            
            // Get actual connected clients count
            _connectedPlayersCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
            
            // Initialize death status for all connected players
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                _playerDeathStatus[clientId] = false;
            }
            
            Debug.Log($"Initial connected players count: {_connectedPlayersCount.Value}");
            Debug.Log($"Initialized death status for {_playerDeathStatus.Count} players");
        }
        
        // Register for network manager client connected/disconnected events
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        _deadPlayersCount.OnValueChanged -= OnDeadPlayersCountChanged;
        
        // Unregister network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        
        // Update connected players count
        _connectedPlayersCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
        
        // Initialize new player's death status
        _playerDeathStatus[clientId] = false;
        
        Debug.Log($"Client {clientId} connected. Total connected: {_connectedPlayersCount.Value}");
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        
        // Update connected players count
        _connectedPlayersCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
        
        // Remove player from death tracking
        if (_playerDeathStatus.ContainsKey(clientId))
        {
            _playerDeathStatus.Remove(clientId);
        }
        
        Debug.Log($"Client {clientId} disconnected. Total connected: {_connectedPlayersCount.Value}");
        
        // Check if all remaining players are dead - game might need to end
        CheckAllPlayersDead();
    }

    private void OnDeadPlayersCountChanged(int previous, int current)
    {
        Debug.Log($"Dead players count updated: {previous} -> {current}. IsClient={IsClient}, IsServer={IsServer}, GameEnded={_gameEnded}, Connected Players={_connectedPlayersCount.Value}");
    }
    
    private void CheckAllPlayersDead()
    {
        if (!IsServer || _gameEnded || _connectedPlayersCount.Value <= 0) return;
        
        // Count how many active players are still alive
        int alivePlayersCount = 0;
        int deadPlayersCount = 0;
        
        foreach (var entry in _playerDeathStatus)
        {
            if (entry.Value) // If player is dead
            {
                deadPlayersCount++;
                Debug.Log($"Player {entry.Key} is marked as dead");
            }
            else
            {
                alivePlayersCount++;
                Debug.Log($"Player {entry.Key} is still alive");
            }
        }
        
        Debug.Log($"CheckAllPlayersDead: {alivePlayersCount} alive players, {deadPlayersCount} dead players out of {_connectedPlayersCount.Value} total");
        
        bool allPlayersDead = (alivePlayersCount == 0 && deadPlayersCount > 0);
        
        if (allPlayersDead)
        {
            Debug.Log($"All {deadPlayersCount} players are dead - showing game over screen");
            ShowGameOverClientRpc();
        }
        else
        {
            // Notify all clients about player deaths but keep game running
            UpdatePlayerStatusClientRpc();
        }
    }

    [ClientRpc]
    private void UpdatePlayerStatusClientRpc()
    {
        // This can be used to notify all clients about player deaths without ending the game
        Debug.Log($"Game status updated: {_deadPlayersCount.Value} players dead, game continues");
        
        // Could be expanded to show/update UI elements indicating which players have died
    }

    public void OnPlayerDied(ulong clientId)
    {
        if (!IsServer) 
        {
            Debug.Log("GameManager.OnPlayerDied called on non-server instance - ignoring");
            return;
        }
        
        Debug.Log($"Player {clientId} died!");
        
        // Mark this player as dead in our tracking dictionary
        _playerDeathStatus[clientId] = true;
        
        // Update dead players count for debugging
        _deadPlayersCount.Value++;
        
        Debug.Log($"Player died! Dead players count: {_deadPlayersCount.Value}/{_connectedPlayersCount.Value}, IsServer={IsServer}, IsClient={IsClient}");
        
        // Check if all players are now dead
        CheckAllPlayersDead();
    }
    
    [ClientRpc]
    private void ShowGameOverClientRpc()
    {
        // This will be called on all clients to show the game over screen
        Debug.Log("ShowGameOverClientRpc called. Showing game over screen on all clients.");
        _gameEnded = true; // Only set game ended when actually showing the game over screen
        ShowGameOver();
    }

    private void ShowGameOver()
    {
        // Don't set _gameEnded here - it's set above when ShowGameOverClientRpc is called
        
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
                
                if (_scoreController != null)
                {
                    // Get the current score from the NetworkVariable
                    int finalScore = _scoreController.Score;
                    Debug.Log($"Final score from ScoreController: {finalScore}");
                    
                    // Update game over text with score
                    _gameOverText.text = $"Game Over\nScore: {finalScore}";
                    
                    Debug.Log($"Set GameOverText to: {_gameOverText.text}");
                }
                else
                {
                    Debug.LogWarning("ScoreController not found, showing score 0");
                    _gameOverText.text = "Game Over\nScore: 0";
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