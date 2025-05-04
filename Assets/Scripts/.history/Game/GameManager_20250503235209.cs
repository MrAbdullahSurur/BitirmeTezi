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
    private TextMeshProUGUI _gameOverText;
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
        // Get the ScoreController component attached to this GameManager object
        _scoreController = GetComponent<ScoreController>();
        if (_scoreController == null)
        {
            Debug.LogError("GameManager could not find ScoreController component on itself!", this);
        }

        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(false);
            
            if (_exitButton != null)
            {
                _exitButton.onClick.AddListener(OnExitButtonClicked);
            }
        }

        // Log the state of _gameOverText immediately after potential assignment from Inspector
        if (_gameOverText == null)
        {
            Debug.LogError($"GameManager Awake ({NetworkManager.Singleton?.LocalClientId ?? ulong.MaxValue}): _gameOverText is NULL immediately after Awake assignments! Check Inspector reference.", this);
        }
        else
        {
             Debug.Log($"GameManager Awake ({NetworkManager.Singleton?.LocalClientId ?? ulong.MaxValue}): _gameOverText reference seems VALID in Awake. Object: {_gameOverText.gameObject.name}", _gameOverText.gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        // Log when this runs on Server vs Client
        if (IsServer)
        {
            Debug.Log("GameManager OnNetworkSpawn running on SERVER.");
        }
        if (IsClient)
        {
             Debug.Log("GameManager OnNetworkSpawn running on CLIENT.");
        }

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

        // Play background music directly if this instance is the server
        if (IsServer)
        {
            Debug.Log("GameManager OnNetworkSpawn: Instance is Server. Attempting to play background music.");
            // Check if the SoundEffectManager instance exists and the sound is defined
            if (SoundEffectManager.Instance != null && SoundEffectManager.Instance.HasSoundEffect("BackgroundMusic"))
            {
                // Use PlayBackgroundSound for looping
                SoundEffectManager.Instance.PlayBackgroundSound("BackgroundMusic"); 
            }
            else
            {
                 if (SoundEffectManager.Instance == null)
                 {
                     Debug.LogError("GameManager (OnNetworkSpawn): SoundEffectManager.Instance is null when trying to play music!");
                 }
                 else
                 {
                    Debug.LogWarning("GameManager (OnNetworkSpawn): SoundEffect 'BackgroundMusic' not found in the SoundEffectManager list!");
                 }
            }
        }
        else
        {
            Debug.Log("GameManager OnNetworkSpawn: Instance is Client. Skipping background music.");
        }
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
        // Find UI elements dynamically when needed
        if (_gameOverPanel == null) 
        {
            _gameOverPanel = GameObject.FindGameObjectWithTag("GameOverPanel"); // Paneli tag ile bulmayı dene
            if (_gameOverPanel == null) 
            {
                 Debug.LogError($"GameManager ShowGameOver ({NetworkManager.Singleton?.LocalClientId ?? ulong.MaxValue}): Could not find GameOverPanel GameObject! Ensure it exists and has the correct tag.");
                 return; // Panel yoksa devam etme
            }
            else
            {
                 _gameOverPanel.SetActive(true); // Paneli bulduysak aktif edelim
            }
        }
        else if (!_gameOverPanel.activeSelf)
        {
            _gameOverPanel.SetActive(true); // Önceden referans varsa ama kapalıysa aktif edelim
        }

        // Text referansını bul/kontrol et
        if (_gameOverText == null && _gameOverPanel != null) 
        {   
            _gameOverText = _gameOverPanel.GetComponentInChildren<TMP_Text>(true); 
            if (_gameOverText == null) 
            {
                Debug.LogError($"GameManager ShowGameOver ({NetworkManager.Singleton?.LocalClientId ?? ulong.MaxValue}): Could not find TMP_Text component within GameOverPanel!", _gameOverPanel);
            }
        }
        
        // Log the state of the reference right when the method is called
        if (_gameOverText == null)
        {
             Debug.LogError($"GameManager ShowGameOver ({NetworkManager.Singleton?.LocalClientId ?? ulong.MaxValue}): _gameOverText is NULL when ShowGameOver is called! Cannot display game over message.", this);
             return; // Text yoksa devam etme
        }
        else
        {
             Debug.Log($"GameManager ShowGameOver ({NetworkManager.Singleton?.LocalClientId ?? ulong.MaxValue}): _gameOverText reference is VALID when ShowGameOver is called. Object: {_gameOverText.gameObject.name}", _gameOverText.gameObject);
        }
        
        // _gameEnded, ShowGameOverClientRpc içinde set ediliyor, burada tekrar set etmeye gerek yok.
        
        // Ensure the panel is active before trying to set text
        if (!_gameOverPanel.activeSelf) { _gameOverPanel.SetActive(true); }
        
        // Get ScoreController reference if missing (safe check)
        if (_scoreController == null)
        {
            _scoreController = GetComponent<ScoreController>(); // GameManager üzerinde olduğunu varsayıyoruz
        }
        
        // Determine player count
        int playerCount = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;
        
        if (_scoreController != null && playerCount > 1)
        {
            // Multiple players - show win/lose message based on score
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            bool isWinner = _scoreController.IsLocalPlayerWinner();
            int yourScore = _scoreController.GetPlayerScore(localClientId); // Use GetPlayerScore
            
            // Get opponent's score 
            int opponentScore = 0;
            var allScores = _scoreController.GetAllPlayerScores(); 
            
            foreach (var entry in allScores)
            {
                if (entry.Key != localClientId)
                {
                    opponentScore = entry.Value;
                    break; // Assuming only one opponent for now
                }
            }
            
            // Check for draw before declaring winner/loser
            if (yourScore == opponentScore)
            {
                // Use \n for new lines - Assign to _gameOverText
                _gameOverText.text = $"Berabere!\nPuan: {yourScore}"; 
            }
            else if (isWinner)
            {
                 // Use \n for new lines - Assign to _gameOverText
                _gameOverText.text = $"Kazandınız!\nSenin Puanın: {yourScore}\nRakibin Puanı: {opponentScore}";
            }
            else
            {
                 // Use \n for new lines - Assign to _gameOverText
                _gameOverText.text = $"Kaybettiniz!\nSenin Puanın: {yourScore}\nRakibin Puanı: {opponentScore}";
            }
            
            Debug.Log($"Game Over - Local player {(isWinner ? "won" : "lost")} with score {yourScore} vs {opponentScore}");
        }
        else if (_scoreController != null && playerCount <= 1) // Handle 0 or 1 player (e.g., host alone)
        {
            // Single player mode or host alone - just show Game Over and score
            int finalScore = _scoreController.Score; // Use Score property for local score
             // Use \n for new lines - Assign to _gameOverText
            _gameOverText.text = $"Oyun Bitti\nPuan: {finalScore}"; 
            
            Debug.Log($"Game Over - Single player/Host alone with score {finalScore}");
        }
        else
        {
            // Fallback if no score controller or something went wrong
            // Assign to _gameOverText
            _gameOverText.text = "Oyun Bitti";
            Debug.LogWarning("ScoreController not found or player count invalid at game over, showing generic message");
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