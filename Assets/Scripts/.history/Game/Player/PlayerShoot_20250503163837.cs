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

    // These are used by the HOST PlayerInput
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
        _lastFireTime = -_timeBetweenShots;
        
        // Disable PlayerInput for non-host owners
        if (IsOwner && !IsHost && _playerInput != null)
        {
             _playerInput.enabled = false;
        }
        // Disable Update loop if not owner AND not server (server needs ServerControlledFire)
        if (!IsOwner && !IsServer)
        {
            // We keep the script enabled on server for ServerControlledFire, but disable Update
            // Note: A better approach might be to separate host input from server logic
        }
    }

    // Update is only used by the HOST for its own firing input
    void Update()
    {
        if (!IsOwner || !IsHost) return; // Only host uses PlayerInput for firing
        
        if (_fireContinuously || _fireSingle)
        {
            float timeSinceLastFire = Time.time - _lastFireTime;

            if (timeSinceLastFire >= _timeBetweenShots)
            {
                // Host fires directly via ServerRpc
                RequestShootServerRpc(_gunOffset.position, transform.rotation);
                _lastFireTime = Time.time;
                _fireSingle = false;
            }
        }
    }
    
    // Called by HOST PlayerInput component
    private void OnFire(InputValue inputValue)
    {
        if (!IsOwner || !IsHost) return;
        _fireContinuously = inputValue.isPressed;
        if (inputValue.isPressed) _fireSingle = true;
    }

    // SERVER RPC: Called by Host (for itself)
    [ServerRpc]
    private void RequestShootServerRpc(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Ensure this is only executed by the owner (Host) on the server
        if (!IsOwner) 
        {
             Debug.LogError("RequestShootServerRpc called by non-owner!");
             return;
        }
        if (!IsServer) return; 
        
        // Execute the shared spawn logic - host's bullets are owned by client ID 0
        SpawnBullet(spawnPosition, spawnRotation, OwnerClientId);
    }
    
    // METHOD EXECUTED ON SERVER: Called by PlayerMovement ServerRpc to fire for the client
    public void ServerControlledFire()
    {
        if (!IsServer) return; // Should only run on server
        
        // Check fire rate
        float timeSinceLastFire = Time.time - _lastFireTime;
        if (timeSinceLastFire >= _timeBetweenShots)
        {
            // Directly spawn the bullet on the server
            if (_gunOffset != null)
            {
                 // OwnerClientId will be the client ID that owns this player
                 SpawnBullet(_gunOffset.position, transform.rotation, OwnerClientId);
                 _lastFireTime = Time.time;
                 // Debug.Log($"ServerControlledFire executed for {gameObject.name}");
            }
            else {
                 Debug.LogError($"ServerControlledFire: Gun Offset is null for {gameObject.name}", this);
            }
        }
    }

    // Shared bullet spawning logic (Runs only on Server)
    private void SpawnBullet(Vector3 spawnPosition, Quaternion spawnRotation, ulong ownerClientId)
    {
        if (!IsServer) return;

        if (_bulletPrefab == null) {
             Debug.LogError("Bullet Prefab not assigned!", this);
             return;
        }

        GameObject bulletInstance = Instantiate(_bulletPrefab, spawnPosition, spawnRotation);
        NetworkObject networkObject = bulletInstance.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
             networkObject.Spawn(true); // Spawn with ownership to the server
             
             // Set the player who owns this bullet
             Bullet bulletComponent = bulletInstance.GetComponent<Bullet>();
             if (bulletComponent != null)
             {
                 bulletComponent.SetOwner(ownerClientId);
             }
             
             Rigidbody2D rb = bulletInstance.GetComponent<Rigidbody2D>();
             if (rb != null)
             {
                 rb.velocity = spawnRotation * Vector3.up * _bulletSpeed;
             }
        }
        else
        {
             Debug.LogError("Bullet prefab missing NetworkObject!", bulletInstance);
             Destroy(bulletInstance);
        }
    }
}