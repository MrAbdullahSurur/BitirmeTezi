using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

// NetworkObject bileşeni gerektirir (genellikle GameManager gibi bir objede bulunur)
[RequireComponent(typeof(NetworkObject))] 
public class ScoreController : NetworkBehaviour // NetworkBehaviour'dan türetildi
{
    // Skoru ağ üzerinden senkronize et
    private NetworkVariable<int> _networkScore = new NetworkVariable<int>(
        0, 
        NetworkVariableReadPermission.Everyone, // Herkes skoru okuyabilir
        NetworkVariableWritePermission.Server // Sadece sunucu skoru değiştirebilir
    );

    // Skor değiştiğinde tetiklenecek olay (client'larda UI güncellemek için)
    // Not: UnityEvent ağ üzerinden otomatik senkronize olmaz.
    // NetworkVariable.OnValueChanged daha güvenilirdir.
    // public UnityEvent OnScoreChanged; 
    
    // Public property to access the score value
    public int Score => _networkScore.Value;

    public override void OnNetworkSpawn()
    {
        // Client'lar skor değiştiğinde UI'ı güncellesin
        if (!IsServer) 
        {
            _networkScore.OnValueChanged += ClientOnScoreChanged;
            // Başlangıç değerini UI'a yansıt
            UpdateScoreUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Event aboneliğini kaldır
        if (!IsServer) 
        {
            _networkScore.OnValueChanged -= ClientOnScoreChanged;
        }
    }
    
    // Skor sadece sunucuda eklenir
    public void AddScore(int amount)
    {
        if (!IsServer) 
        {
            Debug.LogWarning("AddScore called on client! Score can only be added by the server.");
            return; 
        }
        
        _networkScore.Value += amount;
        // Debug.Log($"Score added on server: {amount}. New score: {_networkScore.Value}");
        // OnScoreChanged.Invoke(); // Sunucuda event tetiklemeye gerek yok, NetworkVariable client'ları tetikler
    }
    
    // Client tarafında skor değiştiğinde çağrılır
    private void ClientOnScoreChanged(int previousValue, int newValue)
    {
        // Debug.Log($"Client received score update: {newValue}");
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
        else
        { 
            // Debug.LogWarning("ScoreUI not found in scene.");
        }
    }
}
