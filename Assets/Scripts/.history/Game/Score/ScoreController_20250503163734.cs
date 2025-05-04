using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using System.Linq;

// NetworkObject bileşeni gerektirir (genellikle GameManager gibi bir objede bulunur)
[RequireComponent(typeof(NetworkObject))] 
public class ScoreController : NetworkBehaviour
{
    // Global score for backward compatibility (to be removed later)
    private NetworkVariable<int> _networkScore = new NetworkVariable<int>(
        0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );
    
    // Dictionary to track scores for each player
    private readonly NetworkVariable<PlayerScoreData> _playerScores = new NetworkVariable<PlayerScoreData>(
        new PlayerScoreData(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    // Public property to access the current player's score
    public int Score 
    {
        get 
        {
            if (_playerScores.Value.scores.TryGetValue(GetLocalClientId(), out int score))
            {
                return score;
            }
            return 0; // Default score if not found
        }
    }
    
    // Get score for a specific player
    public int GetPlayerScore(ulong clientId)
    {
        if (_playerScores.Value.scores.TryGetValue(clientId, out int score))
        {
            return score;
        }
        return 0;
    }
    
    // Get all player scores as a dictionary
    public Dictionary<ulong, int> GetAllPlayerScores()
    {
        return new Dictionary<ulong, int>(_playerScores.Value.scores);
    }
    
    // Get the player ID with the highest score
    public ulong GetWinningPlayerId()
    {
        if (_playerScores.Value.scores.Count == 0)
            return 0;
            
        return _playerScores.Value.scores
            .OrderByDescending(pair => pair.Value)
            .First().Key;
    }
    
    // Check if this player is the winner
    public bool IsLocalPlayerWinner()
    {
        return GetWinningPlayerId() == GetLocalClientId();
    }
    
    // Get the current client ID (handle both server and client cases)
    private ulong GetLocalClientId()
    {
        if (NetworkManager.Singleton == null)
            return 0;
            
        return NetworkManager.Singleton.LocalClientId;
    }

    public override void OnNetworkSpawn()
    {
        // Skor değiştiğinde UI'ı güncellesin (Hem Server/Host hem de Client için)
        _networkScore.OnValueChanged += OnScoreChangedCallback;
        _playerScores.OnValueChanged += OnPlayerScoresChangedCallback;
        
        // Başlangıç değerini UI'a yansıt
        UpdateScoreUI();
    }

    public override void OnNetworkDespawn()
    {
        // Event aboneliğini kaldır
        _networkScore.OnValueChanged -= OnScoreChangedCallback;
        _playerScores.OnValueChanged -= OnPlayerScoresChangedCallback;
    }
    
    // Adds score to a specific player
    public void AddScoreToPlayer(ulong playerId, int amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning("AddScoreToPlayer called on client! Score can only be added by the server.");
            return;
        }
        
        PlayerScoreData newData = _playerScores.Value;
        
        if (newData.scores.ContainsKey(playerId))
        {
            newData.scores[playerId] += amount;
        }
        else
        {
            newData.scores[playerId] = amount;
        }
        
        _playerScores.Value = newData;
        
        // Also update legacy score for backward compatibility
        _networkScore.Value += amount;
    }
    
    // Backward compatibility method - adds score to the server/host
    public void AddScore(int amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning("AddScore called on client! Score can only be added by the server.");
            return;
        }
        
        // Add score to the host player (clientId 0)
        AddScoreToPlayer(0, amount);
    }
    
    // Skor değiştiğinde çağrılır (legacy callback)
    private void OnScoreChangedCallback(int previousValue, int newValue)
    {
        Debug.Log($"ScoreController (Instance: {NetworkObjectId}): Legacy Score changed from {previousValue} to {newValue} on {(IsServer ? "Server" : "Client")}");
        // Backward compatibility - we now use the player-specific callback
    }
    
    private void OnPlayerScoresChangedCallback(PlayerScoreData previousValue, PlayerScoreData newValue)
    {
        Debug.Log($"ScoreController (Instance: {NetworkObjectId}): Player scores changed on {(IsServer ? "Server" : "Client")}");
        UpdateScoreUI();
    }
    
    // UI Güncelleme (ScoreUI scriptini bulup çağırır)
    private void UpdateScoreUI()
    {
        ScoreUI scoreUI = FindObjectOfType<ScoreUI>();
        if (scoreUI != null)
        { 
            scoreUI.UpdateScore(this);
        }
    }
}

// Serializable struct to hold player scores in the NetworkVariable
[System.Serializable]
public struct PlayerScoreData : INetworkSerializable
{
    public Dictionary<ulong, int> scores;
    
    public PlayerScoreData()
    {
        scores = new Dictionary<ulong, int>();
    }
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int count = 0;
        
        if (!serializer.IsReader)
        {
            count = scores.Count;
        }
        
        serializer.SerializeValue(ref count);
        
        if (serializer.IsReader)
        {
            scores = new Dictionary<ulong, int>(count);
        }
        
        if (count > 0)
        {
            ulong[] keys = serializer.IsReader ? new ulong[count] : scores.Keys.ToArray();
            int[] values = serializer.IsReader ? new int[count] : scores.Values.ToArray();
            
            for (int i = 0; i < count; i++)
            {
                serializer.SerializeValue(ref keys[i]);
                serializer.SerializeValue(ref values[i]);
                
                if (serializer.IsReader)
                {
                    scores.Add(keys[i], values[i]);
                }
            }
        }
    }
}
