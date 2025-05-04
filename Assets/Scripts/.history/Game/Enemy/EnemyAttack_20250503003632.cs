using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [SerializeField]
    private float _damageAmount;

    private void OnCollisionStay2D(Collision2D collision)
    {
        // This logic should ideally only run on the server 
        // since damage is server authoritative.
        // Checking if collision object has a NetworkObject might be needed.
        // For now, assume this check is sufficient for gameplay logic.

        if (collision.gameObject.GetComponent<PlayerMovement>())
        {
            var healthController = collision.gameObject.GetComponent<HealthController>();
            // Call RequestTakeDamage instead of TakeDamage
            if (healthController != null) 
            {
                 healthController.RequestTakeDamage(_damageAmount);
            }
        }
    }
}
