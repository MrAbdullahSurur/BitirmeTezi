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
        
        // Log for debugging
        Debug.Log($"PlayerShoot {OwnerClientId} spawned: IsHost={IsHost}, IsClient={IsClient}, PlayerInput enabled: {_playerInput.enabled}");
        
        _lastFireTime = -_timeBetweenShots;
        
        // Ensure input is set up properly
        if (!IsHost)
        {
            // Schedule client-specific setup
            StartCoroutine(EnsureClientInput());
        }
    }
    
    private IEnumerator EnsureClientInput()
    {
        // Wait for PlayerMovement to finish its setup
        yield return new WaitForSeconds(1f);
        
        if (!IsOwner) yield break;
        
        Debug.Log($"Client PlayerShoot {OwnerClientId}: Setting up input");
        
        // Make sure our input component is enabled
        if (_playerInput != null && !_playerInput.enabled)
        {
            _playerInput.enabled = true;
            
            // Ensure correct action map
            if (_playerInput.currentActionMap?.name != "PlayerArrows")
            {
                try
                {
                    _playerInput.SwitchCurrentActionMap("PlayerArrows");
                    Debug.Log($"Client PlayerShoot {OwnerClientId}: Switched to PlayerArrows action map");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Client PlayerShoot {OwnerClientId}: Failed to switch action map: {e.Message}");
                }
            }
        }
    }

    void Update()
    {
        if (_fireContinuously || _fireSingle)
        {
            float timeSinceLastFire = Time.time - _lastFireTime;

            if (timeSinceLastFire >= _timeBetweenShots)
            {
                // Log firing event
                Debug.Log($"Player {OwnerClientId} firing: IsHost={IsHost}");
                
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
        // Add debug logging
        Debug.Log($"OnFire triggered on Player {OwnerClientId}! IsHost={IsHost}, ActionMap={_playerInput?.currentActionMap?.name}, Value={inputValue.isPressed}");
        
        if (!IsOwner) return;
        
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