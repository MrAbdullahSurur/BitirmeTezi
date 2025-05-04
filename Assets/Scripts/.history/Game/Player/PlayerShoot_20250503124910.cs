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
        // Disable this script entirely if not owner AND not server (non-owners don't need update loop)
        // Server needs it enabled to handle ServerControlledFire
        if (!IsOwner && !IsServer)
        {
            enabled = false;
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

        // Debug.Log($"Host OnFire triggered: {inputValue.isPressed}");
        _fireContinuously = inputValue.isPressed;
        if (inputValue.isPressed)
        {
            _fireSingle = true;
        }
    }

    // SERVER RPC: Called by Host (for itself) or by ServerControlledFire (for client)
    [ServerRpc]
    private void RequestShootServerRpc(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (!IsServer) return;
        
        // --- Bullet Spawning Logic (Runs on Server) ---
        if (_bulletPrefab == null) {
             Debug.LogError("Bullet Prefab not assigned!", this);
             return;
        }
        if (_gunOffset == null) {
             Debug.LogError("Gun Offset not assigned or found!", this);
             // Attempt to find it again?
             var movement = GetComponent<PlayerMovement>();
             if (movement != null) { /* try finding through movement */ }
             return;
        }

        GameObject bulletInstance = Instantiate(_bulletPrefab, spawnPosition, spawnRotation);
        NetworkObject networkObject = bulletInstance.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
             networkObject.Spawn(true); // Spawn with ownership to the server
             
             // Get Rigidbody and apply velocity ON SERVER
             Rigidbody2D rb = bulletInstance.GetComponent<Rigidbody2D>();
             if (rb != null)
             {
                 rb.velocity = spawnRotation * Vector3.up * _bulletSpeed;
                 // Debug.Log($"Server spawned bullet with velocity: {rb.velocity}");
             }
        }
        else
        {
             Debug.LogError("Bullet prefab missing NetworkObject!", bulletInstance);
             Destroy(bulletInstance);
        }
    }
    
    // METHOD EXECUTED ON SERVER: Called by PlayerMovement ServerRpc to fire for the client
    public void ServerControlledFire()
    {
        if (!IsServer) return; // Should only run on server
        
        // Check fire rate
        float timeSinceLastFire = Time.time - _lastFireTime;
        if (timeSinceLastFire >= _timeBetweenShots)
        {
            // Call the ServerRpc to handle spawning (ensures consistent logic)
            if (_gunOffset != null)
            {
                 RequestShootServerRpc(_gunOffset.position, transform.rotation); // Use current server transform
                 _lastFireTime = Time.time;
                 // Debug.Log($"ServerControlledFire executed for {gameObject.name}");
            }
            else {
                 Debug.LogError($"ServerControlledFire: Gun Offset is null for {gameObject.name}", this);
            }
        }
    }
    
    // Removed EnsureClientInput coroutine - no longer needed
}