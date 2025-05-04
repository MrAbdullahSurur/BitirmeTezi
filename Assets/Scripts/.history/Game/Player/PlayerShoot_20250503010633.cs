using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))]
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
    private PlayerInput _playerInput;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
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

             networkObject.Spawn(false); 
        }
        else
        {
             Debug.LogError("Mermi prefabında NetworkObject bulunamadı!", bulletInstance);
             Destroy(bulletInstance);
        }
    }

    private void OnFire(InputValue inputValue)
    {
        InputAction fireAction = _playerInput.actions["Fire"];
        bool triggeredByMouse = false;
        if (fireAction != null)
        {
             foreach (var control in fireAction.controls)
             {
                 if (control.device is Mouse && control.IsPressed() && inputValue.isPressed)
                 {
                     triggeredByMouse = true;
                     break;
                 }
             }
        }

        _fireContinuously = inputValue.isPressed;
        if (inputValue.isPressed)
        {
            _fireSingle = true;
        }
    }
}