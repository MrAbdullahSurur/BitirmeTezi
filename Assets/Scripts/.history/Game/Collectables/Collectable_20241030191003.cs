using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Collectable : MonoBehaviour
{
    private collectablesbehaviour _collectableBehaviour;

    private void Awake()
    {
        _collectableBehaviour = GetComponent<collectablesbehaviour>();
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        var player = collision.GetComponent<PlayerMovement>();
        
        if(player != null)
        {
            _collectableBehaviour.OnCollected(player.gameObject);
            Destroy(gameObject);
        }
    }
}
