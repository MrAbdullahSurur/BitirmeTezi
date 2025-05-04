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
    private Button _okButton;

    private ScoreController _scoreController;
    
    // Track player deaths
    private NetworkVariable<int> _deadPlayersCount = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    private bool _gameEnded = false;

    private void Awake()
    {
        _scoreController = FindObjectOfType<ScoreController>();
        
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(false);
            
            if (_okButton != null)
            {
                _okButton.onClick.AddListener(OnOkButtonClicked);
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
        
        if (IsClient && current >= 2 && !_gameEnded)
        {
            ShowGameOver();
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
        _gameEnded = true;
        
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(true);
            
            int finalScore = 0;
            if (_scoreController != null)
            {
                finalScore = _scoreController.Score;
            }
            
            if (_gameOverText != null)
            {
                _gameOverText.text = $"Game Over\nScore = {finalScore}";
            }
        }
    }

    private void OnOkButtonClicked()
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