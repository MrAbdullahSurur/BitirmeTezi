using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// This component handles inputs directly for the client player
// Use this when having issues with PlayerInput component
public class ClientInputHandler : NetworkBehaviour
{
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
        // Only activate for clients (not for host)
        if (!IsOwner || IsHost)
        {
            enabled = false;
            // If host, still need to find gun offset for potential client control
            if (IsHost)
            {
                FindGunOffset();
            }
            return;
        }
        
        Debug.Log("ClientInputHandler activated for client player", this);
        
        // Disable any PlayerInput component to avoid conflicts
        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = false;
            Debug.Log("Disabled PlayerInput component for client", this);
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
    
    void Update()
    {
        // Logic now runs only for the client owner
        if (!IsOwner || IsHost) return;
        
        // Read movement and fire state from Network Variables
        _currentMovement = _hostSentMoveInput.Value;
        bool isFireHeldDown = _hostSentFireInput.Value;
        
        // Log movement input for debugging (only when it changes)
        if (_currentMovement != Vector2.zero && !_hasMoved)
        {
            Debug.Log($"CLIENT Received Host Input: Move={_currentMovement}", this);
            _hasMoved = true;
        }
        else if (_currentMovement == Vector2.zero)
        {
            _hasMoved = false;
        }
        
        // --- Firing Logic ---
        // Track button state changes based on Network Variable
        if (isFireHeldDown && !_fireButtonDown) // Button just pressed
        {
            _fireButtonDown = true;
            _fireThisFrame = true; // Trigger single shot on press
            Debug.Log("CLIENT: Received Host Fire Input (Pressed)");
        }
        else if (!isFireHeldDown && _fireButtonDown) // Button just released
        {
            _fireButtonDown = false;
        }
        
        // Handle firing logic (continuous fire if held, single shot if just pressed)
        if (_fireThisFrame || _fireButtonDown) 
        {
            float timeSinceLastFire = Time.time - _lastFireTime;
            
            if (timeSinceLastFire >= _timeBetweenShots)
            {
                FireBulletServerRpc(); // Client still tells server to fire
                _lastFireTime = Time.time;
            }
            
            _fireThisFrame = false; // Reset single-shot trigger
        }
        
        // Update animation state
        if (_animator != null)
        {
            _animator.SetBool("IsMoving", _currentMovement.magnitude > 0.1f);
        }
    }
    
    void FixedUpdate()
    {
        if (!IsOwner || IsHost) return;
        
        // Apply movement based on _currentMovement (read from NetworkVariable in Update)
        if (_currentMovement.magnitude > 0.01f)
        {
            // Calculate new position
            Vector3 moveDelta = new Vector3(_currentMovement.x, _currentMovement.y, 0) * _speed * Time.fixedDeltaTime;
            Vector3 newPosition = transform.position + moveDelta;
            
            // Check screen boundaries
            if (_camera != null)
            {
                Vector3 viewportPos = _camera.WorldToViewportPoint(newPosition);
                viewportPos.x = Mathf.Clamp(viewportPos.x, 0.05f, 0.95f);
                viewportPos.y = Mathf.Clamp(viewportPos.y, 0.05f, 0.95f);
                newPosition = _camera.ViewportToWorldPoint(viewportPos);
            }
            
            // Apply position
            transform.position = newPosition;
            
            // Handle rotation - face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _currentMovement);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            
            // Client sends its position to the server for sync
            if (Vector3.Distance(_lastPosition, transform.position) > 0.05f || Time.frameCount % 30 == 0)
            {
                UpdatePositionServerRpc(transform.position, transform.rotation);
                _lastPosition = transform.position;
            }
        }
        // Even if not moving, send updates periodically to ensure sync
        else if (Time.frameCount % 60 == 0)
        {
            UpdatePositionServerRpc(transform.position, transform.rotation);
        }
    }
    
    [ServerRpc]
    private void UpdatePositionServerRpc(Vector3 position, Quaternion rotation)
    {
        // Update position and rotation on server (authority)
        transform.position = position;
        transform.rotation = rotation;
        
        // Broadcast to all clients
        UpdatePositionClientRpc(position, rotation);
    }
    
    [ClientRpc]
    private void UpdatePositionClientRpc(Vector3 position, Quaternion rotation)
    {
        // Only update for non-owners
        if (!IsOwner)
        {
            transform.position = position;
            transform.rotation = rotation;
        }
    }
    
    [ServerRpc]
    private void FireBulletServerRpc()
    {
        var playerShoot = GetComponent<PlayerShoot>();
        if (playerShoot == null) return;
        
        var method = playerShoot.GetType().GetMethod("RequestShootServerRpc", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        if (method != null && _gunOffset != null)
        {
            method.Invoke(playerShoot, new object[] { _gunOffset.position, transform.rotation });
            // Debug.Log("Server executed FireBulletServerRpc requested by client");
        }
        else {
            Debug.LogError("FireBulletServerRpc: Could not find RequestShootServerRpc method or gun offset", this);
        }
    }
    
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