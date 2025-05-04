using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAwarenessController : MonoBehaviour
{
    public bool AwareOfPlayer { get; private set; }

    public Vector2 DirectionToPlayer { get; private set; }


    [SerializeField]
    private float _playerAwarenessDistance;

    void Update()
    {
        // Aktif oyuncu listesini al
        List<PlayerMovement> players = PlayerMovement.ActivePlayers;
        
        Transform closestPlayer = null;
        float minDistanceSqr = _playerAwarenessDistance * _playerAwarenessDistance; // Karesi alınmış mesafe ile karşılaştır
        bool foundPlayerInRange = false;

        if (players.Count == 0)
        {
            // Sahnede oyuncu yoksa
            AwareOfPlayer = false;
            return;
        }

        // En yakın oyuncuyu bul
        foreach (PlayerMovement player in players)
        {
            if (player == null) continue; // Liste temizlenirken nadiren null olabilir
            
            // Check if player is dead - ignore dead players
            HealthController playerHealth = player.GetComponent<HealthController>();
            if (playerHealth != null && playerHealth.RemainingHealthPercentage <= 0)
            {
                // Skip dead players
                continue;
            }
            
            Vector2 directionToPlayer = player.transform.position - transform.position;
            float distanceSqr = directionToPlayer.sqrMagnitude; // Uzaklığın karesi (daha hızlı)

            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closestPlayer = player.transform;
                foundPlayerInRange = true; // Menzil içinde en az bir oyuncu bulundu
            }
        }

        // En yakın oyuncuya göre durumu güncelle
        if (foundPlayerInRange && closestPlayer != null)
        {
            AwareOfPlayer = true;
            DirectionToPlayer = (closestPlayer.position - transform.position).normalized;
        }
        else
        {
            AwareOfPlayer = false;
            // İsteğe bağlı: Yöne varsayılan bir değer ata
            // DirectionToPlayer = Vector2.zero;
        }
    }
}
