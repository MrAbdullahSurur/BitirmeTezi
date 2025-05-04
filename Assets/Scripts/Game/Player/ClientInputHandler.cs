using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// This component handles inputs directly for the client player
// Use this when having issues with PlayerInput component
public class ClientInputHandler : NetworkBehaviour
{
    // This component is now mostly inactive for client players when host controls them.
    // It might be used if the client window itself gets focus, but primary control
    // comes from the server via PlayerMovement RPCs.

    [Header("Movement Settings")]
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _rotationSpeed = 200f;
    
    // Direct input handling fields
    private Vector2 _currentMovement; // This will now be driven by NetworkVariable
    private bool _fireThisFrame = false;
    private bool _fireButtonDown = false;
    private float _lastFireTime = 0f;
    [SerializeField] private float _timeBetweenShots = 0.2f;
    
    private Camera _camera;
    private Animator _animator;
    private Transform _gunOffset;
    private Rigidbody2D _rb;
    
    // For smooth movement
    private Vector3 _lastPosition;
    private bool _hasMoved = false;
    
    // Network Variables for host-driven input
    private NetworkVariable<Vector2> _hostSentMoveInput = new NetworkVariable<Vector2>(
        Vector2.zero, 
        NetworkVariableReadPermission.Owner, // Only owner (client) needs to read
        NetworkVariableWritePermission.Server // Only server (host) can write
    );
    private NetworkVariable<bool> _hostSentFireInput = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Owner, // Only owner (client) needs to read
        NetworkVariableWritePermission.Server // Only server (host) can write
    );
    
    public override void OnNetworkSpawn()
    {
        // Deactivate this script for client owners, as control comes from the server
        if (IsOwner && !IsHost)
        {
            enabled = false;
            Debug.Log("ClientInputHandler disabled for client owner. Control handled by server.");
        }
        // Keep it enabled for host or non-owners if needed for other logic (unlikely now)
        else if (IsHost)
        {
            // Host doesn't use this for input
            enabled = false;
        }
        
        // Initialize references
        _camera = Camera.main;
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _lastPosition = transform.position;
        
        FindGunOffset();
        
        // Disable NetworkRigidbody2D to allow direct control
        var networkRb = GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
        if (networkRb != null)
        {
            networkRb.enabled = false;
            Debug.Log("Disabled NetworkRigidbody2D for client control", this);
        }
        
        // Disable NetworkTransform if it exists
        var networkTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (networkTransform != null)
        {
            networkTransform.enabled = false;
            Debug.Log("Disabled NetworkTransform for client control", this);
        }
        
        Debug.Log("CLIENT INPUT HANDLER READY - Host will control via NetworkVariables");
    }
    
    private void FindGunOffset()
    {
        // Find gun offset in the PlayerShoot component
        var playerShoot = GetComponent<PlayerShoot>();
        if (playerShoot != null)
        {
            var field = playerShoot.GetType().GetField("_gunOffset", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                _gunOffset = field.GetValue(playerShoot) as Transform;
                if (_gunOffset != null) Debug.Log("Found gun offset reference");
            }
        }
    }
    
    // Removed all Update, FixedUpdate, and input-related methods
    // as they are no longer used for client control when host is focused.
    // The server directly moves the client player.
    
    // --- Public method for Server/Host to update NetworkVariables ---
    [ServerRpc(RequireOwnership = false)] // Allow server to call this on the client's object
    public void UpdateInputFromServerServerRpc(Vector2 moveInput, bool fireInput)
    {
        // This code runs ON THE SERVER (Host)
        // Update the network variables, which will sync to the client owner
        _hostSentMoveInput.Value = moveInput;
        _hostSentFireInput.Value = fireInput;
        // Debug.Log($"Server updated input for client {OwnerClientId}: Move={moveInput}, Fire={fireInput}");
    }
} 