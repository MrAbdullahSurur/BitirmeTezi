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
            ulong localClientId = GetLocalClientId(); // Get local client ID
            int score = 0;
            bool found = false;
            
            // Check if the Scores dictionary inside the struct is initialized
            if (_playerScores.Value.Scores != null) 
            {
                found = _playerScores.Value.Scores.TryGetValue(localClientId, out score);
            }
            else
            {
                 Debug.LogWarning($"ScoreController.Score Getter: _playerScores or _playerScores.Value.Scores is null! Local Client ID: {localClientId}");
            }

            Debug.Log($"ScoreController.Score Getter called. Local Client ID: {localClientId}, Score Found: {found}, Score Value: {score}");
            
            return score; // Return found score or default 0
        }
    }
    
    // Get score for a specific player
    public int GetPlayerScore(ulong clientId)
    {
        if (_playerScores.Value.Scores.TryGetValue(clientId, out int score))
        {
            return score;
        }
        return 0;
    }
    
    // Get all player scores as a dictionary
    public Dictionary<ulong, int> GetAllPlayerScores()
    {
        return new Dictionary<ulong, int>(_playerScores.Value.Scores);
    }
    
    // Get the player ID with the highest score
    public ulong GetWinningPlayerId()
    {
        if (_playerScores.Value.Scores.Count == 0)
            return 0;
            
        return _playerScores.Value.Scores
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
        _playerScores.OnValueChanged += OnPlayerScoresChangedCallback;
        
        // Log subscription status specifically on clients
        if (!IsServer)
        {
            Debug.Log($"ScoreController CLIENT (Instance: {NetworkObjectId}, ClientId: {OwnerClientId}): Subscribed to _playerScores.OnValueChanged.");
        }
        
        // Başlangıç değerini UI'a yansıt
        UpdateScoreUI();
    }

    public override void OnNetworkDespawn()
    {
        // Event aboneliğini kaldır
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
        
        if (newData.Scores.ContainsKey(playerId))
        {
            newData.Scores[playerId] += amount;
        }
        else
        {
            newData.Scores[playerId] = amount;
        }
        
        _playerScores.Value = newData;
    }
    
    private void OnPlayerScoresChangedCallback(PlayerScoreData previousValue, PlayerScoreData newValue)
    {
        // Log specifically when this callback runs on server/client
        Debug.Log($"ScoreController (Instance: {NetworkObjectId}): OnPlayerScoresChangedCallback triggered on {(IsServer ? "Server" : "Client")}");
        
        // Log the new score data received
        string scoresDebug = "Scores: ";
        if (newValue.Scores != null)
        {
            foreach(var kvp in newValue.Scores)
            {
                scoresDebug += $"[{kvp.Key}:{kvp.Value}] ";
            }
        }
        else { scoresDebug += "NULL"; }
        Debug.Log($"ScoreController (Instance: {NetworkObjectId}): Received new scores - {scoresDebug}");

        UpdateScoreUI();
    }
    
    // UI Güncelleme (ScoreUI scriptini bulup çağırır)
    private void UpdateScoreUI()
    {
        Debug.Log($"ScoreController (Instance: {NetworkObjectId}): UpdateScoreUI called on {(IsServer ? "Server" : "Client")}");
        
        // Use ScoreUI Singleton instance
        if (ScoreUI.Instance != null)
        { 
            Debug.Log($"ScoreController (Instance: {NetworkObjectId}): Found ScoreUI singleton instance. Calling UpdateScore...");
            ScoreUI.Instance.UpdateScore(this);
        }
        else
        {
            Debug.LogError($"ScoreController (Instance: {NetworkObjectId}): ScoreUI.Instance is NULL on {(IsServer ? "Server" : "Client")}");
        }
    }
}

// Serializable struct to hold player scores in the NetworkVariable
[System.Serializable]
public struct PlayerScoreData : INetworkSerializable
{
    public Dictionary<ulong, int> scores;
    
    // Initialize dictionary in property instead of constructor for C# 9.0 compatibility
    public Dictionary<ulong, int> Scores => scores ?? (scores = new Dictionary<ulong, int>());
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int count = 0;
        
        if (!serializer.IsReader)
        {
            // Make sure scores is initialized before reading count
            count = Scores.Count;
        }
        
        serializer.SerializeValue(ref count);
        
        if (serializer.IsReader)
        {
            scores = new Dictionary<ulong, int>(count);
        }
        
        if (count > 0)
        {
            ulong[] keys = serializer.IsReader ? new ulong[count] : Scores.Keys.ToArray();
            int[] values = serializer.IsReader ? new int[count] : Scores.Values.ToArray();
            
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
