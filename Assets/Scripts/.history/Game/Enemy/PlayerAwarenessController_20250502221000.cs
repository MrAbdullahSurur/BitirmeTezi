using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAwarenessController : MonoBehaviour
{
    public bool AwareOfPlayer { get; private set; }

    public Vector2 DirectionToPlayer { get; private set; }


    [SerializeField]
    private float _playerAwarenessDistance;

    private Transform _player;

    private void Awake()
    {
        _player = FindObjectOfType<PlayerMovement>().transform;
    }


    // Update is called once per frame
    void Update()
    {
        // Oyuncu referansı geçerli değilse veya yok edilmişse tekrar bulmayı dene
        if (_player == null)
        {
            PlayerMovement playerMovement = FindObjectOfType<PlayerMovement>();
            if (playerMovement != null)
            {
                _player = playerMovement.transform;
            }
            else
            {
                // Sahnede oyuncu yoksa veya bulunamıyorsa, farkındalık mantığını atla
                AwareOfPlayer = false; 
                return; 
            }
        }
        
        // _player hala null olabilir (çok nadir bir yarış durumu)
        // veya bir önceki frame'de bulunan nesne bu frame'de yok edilmiş olabilir.
        // Bu yüzden tekrar kontrol edelim.
        if (_player == null) 
        { 
            AwareOfPlayer = false;
            return;
        }

        // Şimdi _player referansının (muhtemelen) geçerli olduğunu varsayabiliriz
        Vector2 enemyToPlayerVector = _player.position - transform.position;
        DirectionToPlayer = enemyToPlayerVector.normalized;

        if (enemyToPlayerVector.magnitude <= _playerAwarenessDistance)
        {
            AwareOfPlayer = true;
        }
        else
        {
            AwareOfPlayer= false;
        }
    }
}
