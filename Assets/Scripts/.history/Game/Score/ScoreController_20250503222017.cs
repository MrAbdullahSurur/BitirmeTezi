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
            if (_playerScores.Value.scores != null) 
            {
                found = _playerScores.Value.scores.TryGetValue(localClientId, out score);
            }
            else
            {
                 Debug.LogWarning($"ScoreController.Score Getter: _playerScores or _playerScores.Value.scores is null! Local Client ID: {localClientId}");
            }

            Debug.Log($"ScoreController.Score Getter called. Local Client ID: {localClientId}, Score Found: {found}, Score Value: {score}");
            
            return score; // Return found score or default 0
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
        // Log specifically when this runs on Server vs Client
        if (IsServer)
        {
            Debug.Log($"ScoreController (Instance: {NetworkObjectId}) OnNetworkSpawn running on SERVER.");
        }
        if (IsClient)
        {
             Debug.Log($"ScoreController (Instance: {NetworkObjectId}, ClientId: {OwnerClientId}) OnNetworkSpawn running on CLIENT.");
             // Try subscribing specifically on client
             Debug.Log($"ScoreController CLIENT (Instance: {NetworkObjectId}, ClientId: {OwnerClientId}): Attempting to subscribe to _playerScores.OnValueChanged.");
             _playerScores.OnValueChanged += OnPlayerScoresChangedCallback;
             Debug.Log($"ScoreController CLIENT (Instance: {NetworkObjectId}, ClientId: {OwnerClientId}): Subscription call completed for _playerScores.OnValueChanged.");
        }
        else if (IsServer) // If only server (not host), still subscribe
        {
            _playerScores.OnValueChanged += OnPlayerScoresChangedCallback;
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
        
        if (newData.scores.ContainsKey(playerId))
        {
            newData.scores[playerId] += amount;
        }
        else
        {
            newData.scores[playerId] = amount;
        }
        
        _playerScores.Value = newData;
    }
    
    private void OnPlayerScoresChangedCallback(PlayerScoreData previousValue, PlayerScoreData newValue)
    {
        // Log specifically when this callback runs on server/client
        Debug.Log($"ScoreController (Instance: {NetworkObjectId}): OnPlayerScoresChangedCallback triggered on {(IsServer ? "Server" : "Client")}");
        
        // Log the new score data received
        string scoresDebug = "Scores: ";
        if (newValue.scores != null)
        {
            foreach(var kvp in newValue.scores)
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
    // Make the field private and manage access if needed, but keep it simple for now.
    public Dictionary<ulong, int> scores; 
    
    // Remove the property getter to avoid confusion during serialization
    // public Dictionary<ulong, int> Scores => scores ?? (scores = new Dictionary<ulong, int>());
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int count = 0;
        
        if (!serializer.IsReader) // Writing
        {
            // Initialize if null before writing
            if (scores == null) { scores = new Dictionary<ulong, int>(); } 
            count = scores.Count;
        }
        
        // Write/Read count
        serializer.SerializeValue(ref count);
        
        // Initialize or clear the dictionary when reading
        if (serializer.IsReader) 
        {
            scores = new Dictionary<ulong, int>(count); // Initialize with capacity
        }

        // If writing and count > 0 OR if reading and count > 0 
        // (Redundant check for reading, but safe)
        if (!serializer.IsReader && count > 0) // Writing
        {
            // Directly use the keys and values from the field
            ulong[] keys = scores.Keys.ToArray();
            int[] values = scores.Values.ToArray();
            for (int i = 0; i < count; i++)
            {
                serializer.SerializeValue(ref keys[i]);
                serializer.SerializeValue(ref values[i]);
            }
        }
        else if (serializer.IsReader && count > 0) // Reading
        {
             ulong currentKey = 0;
             int currentValue = 0;
             for (int i = 0; i < count; i++)
            {
                serializer.SerializeValue(ref currentKey);
                serializer.SerializeValue(ref currentValue);
                scores.Add(currentKey, currentValue);
            }
        }
    }
}
