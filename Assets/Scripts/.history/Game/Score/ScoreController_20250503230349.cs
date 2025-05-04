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
    // Use NetworkList to store score entries for each player
    private NetworkList<PlayerScoreEntry> _playerScores;

    // Awake is called before OnNetworkSpawn
    private void Awake()
    {
        // Initialize the NetworkList here
        _playerScores = new NetworkList<PlayerScoreEntry>(
            new List<PlayerScoreEntry>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server // Only server can modify the list
        );
    }
    
    // Public property to access the current player's score
    public int Score 
    {
        get 
        {
            return GetPlayerScore(NetworkManager.Singleton.LocalClientId);
        }
    }
    
    // Get score for a specific player
    public int GetPlayerScore(ulong clientId)
    {
        foreach (var entry in _playerScores)
        {
            if (entry.ClientId == clientId)
            {
                return entry.Score;
            }
        }
        return 0; // Return 0 if player not found
    }
    
    // Get all player scores as a dictionary (for easier use elsewhere, e.g., UI)
    public Dictionary<ulong, int> GetAllPlayerScores()
    {
        Dictionary<ulong, int> scoresDict = new Dictionary<ulong, int>();
        foreach (var entry in _playerScores)
        {
            scoresDict[entry.ClientId] = entry.Score;
        }
        return scoresDict;
    }
    
    // Get the player ID with the highest score
    public ulong GetWinningPlayerId()
    {
        if (_playerScores.Count == 0)
            return ulong.MaxValue; // Indicate no winner or invalid state

        ulong winningId = _playerScores[0].ClientId;
        int highestScore = _playerScores[0].Score;

        for (int i = 1; i < _playerScores.Count; i++)
        {
            if (_playerScores[i].Score > highestScore)
            {
                highestScore = _playerScores[i].Score;
                winningId = _playerScores[i].ClientId;
            }
        }
        return winningId;
    }
    
    // Check if this player is the winner
    public bool IsLocalPlayerWinner()
    {
        ulong winningId = GetWinningPlayerId();
        return winningId != ulong.MaxValue && winningId == NetworkManager.Singleton.LocalClientId;
    }
    
    public override void OnNetworkSpawn()
    {
        // Subscribe to NetworkList changes
        _playerScores.OnListChanged += OnPlayerScoresChangedCallback;

        // Log subscription status specifically on clients
        if (IsClient)
        {
             Debug.Log($"ScoreController (Instance: {NetworkObjectId}, ClientId: {NetworkManager.Singleton.LocalClientId}) OnNetworkSpawn running on CLIENT.");
             Debug.Log($"ScoreController CLIENT (Instance: {NetworkObjectId}, ClientId: {NetworkManager.Singleton.LocalClientId}): Subscribed to _playerScores.OnListChanged.");
        }
        if (IsServer)
        {
             Debug.Log($"ScoreController (Instance: {NetworkObjectId}) OnNetworkSpawn running on SERVER.");
        }

        // Initial UI update on spawn
        UpdateScoreUI();
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from NetworkList changes
        if (_playerScores != null) // Check if initialized
        {
            _playerScores.OnListChanged -= OnPlayerScoresChangedCallback;
        }
    }
    
    // Adds score to a specific player (Server only)
    public void AddScoreToPlayer(ulong playerId, int amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning("AddScoreToPlayer called on client! Score can only be added by the server.");
            return;
        }

        // Find the player's entry in the list
        int playerIndex = -1;
        for (int i = 0; i < _playerScores.Count; i++)
        {
            if (_playerScores[i].ClientId == playerId)
            {
                playerIndex = i;
                break;
            }
        }

        if (playerIndex != -1)
        {
            // Player found, update their score
            PlayerScoreEntry updatedEntry = _playerScores[playerIndex];
            updatedEntry.Score += amount;
            _playerScores[playerIndex] = updatedEntry; // Assign back to trigger sync
             Debug.Log($"ScoreController: Updated score for player {playerId} to {_playerScores[playerIndex].Score}");
        }
        else
        {
            // Player not found, add a new entry
            _playerScores.Add(new PlayerScoreEntry { ClientId = playerId, Score = amount });
            Debug.Log($"ScoreController: Added new score entry for player {playerId} with score {amount}");
        }
    }
    
    // Callback for when the NetworkList changes
    private void OnPlayerScoresChangedCallback(NetworkListEvent<PlayerScoreEntry> changeEvent)
    {
        // Log specifically when this callback runs on server/client
        Debug.Log($"ScoreController (Instance: {NetworkObjectId}): OnPlayerScoresChangedCallback triggered on {(IsServer ? "Server" : "Client")}. EventType: {changeEvent.Type}");

        // Log the current list content for debugging
        string scoresDebug = "Current Scores: ";
        foreach (var entry in _playerScores)
        {
            scoresDebug += $"[{entry.ClientId}:{entry.Score}] ";
        }
        Debug.Log($"ScoreController (Instance: {NetworkObjectId}): {scoresDebug}");

        // Always update the UI when the list changes
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

// Serializable struct to hold individual player score entries
// Ensure this struct is defined OUTSIDE the ScoreController class
[System.Serializable]
public struct PlayerScoreEntry : INetworkSerializable, System.IEquatable<PlayerScoreEntry>
{
    public ulong ClientId;
    public int Score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Score);
    }

    public bool Equals(PlayerScoreEntry other)
    {
        return ClientId == other.ClientId && Score == other.Score;
    }
}
