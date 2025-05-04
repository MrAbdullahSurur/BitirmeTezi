using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerShoot : NetworkBehaviour
{
    [SerializeField]
    private GameObject _bulletPrefab;

    [SerializeField]
    private float _bulletSpeed;

    [SerializeField]
    private Transform _gunOffset;

    [SerializeField]
    private float _timeBetweenShots;

    private bool _fireContinuously;
    private bool _fireSingle;
    private float _lastFireTime;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
        }
        _lastFireTime = -_timeBetweenShots;
    }

    void Update()
    {
        if (_fireContinuously || _fireSingle)
        {
            float timeSinceLastFire = Time.time - _lastFireTime;

            if (timeSinceLastFire >= _timeBetweenShots)
            {
                RequestShootServerRpc(_gunOffset.position, transform.rotation);

                _lastFireTime = Time.time;
                _fireSingle = false;
            }
        }
    }

    [ServerRpc]
    private void RequestShootServerRpc(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (_bulletPrefab == null) return;

        GameObject bulletInstance = Instantiate(_bulletPrefab, spawnPosition, spawnRotation);
        NetworkObject networkObject = bulletInstance.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
             Rigidbody2D rb = bulletInstance.GetComponent<Rigidbody2D>();
             if (rb != null)
             {
                 rb.velocity = spawnRotation * Vector3.up * _bulletSpeed;
             }
             else
             {
                 // Rigidbody yoksa transform ile hareket ettir?
                 // Şimdilik Rigidbody varsayıyoruz.
             }

             networkObject.Spawn(true); 
        }
        else
        {
             Debug.LogError("Mermi prefabında NetworkObject bulunamadı!", bulletInstance);
             Destroy(bulletInstance);
        }
    }

    private void OnFire(InputValue inputValue)
    {
        _fireContinuously = inputValue.isPressed;

        if (inputValue.isPressed)
        {
            _fireSingle = true;
        }
    }
}