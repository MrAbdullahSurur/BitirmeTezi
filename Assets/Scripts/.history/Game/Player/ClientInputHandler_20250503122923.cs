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
    private Vector2 _currentMovement;
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
    
    public override void OnNetworkSpawn()
    {
        // Only activate for clients (not for host)
        if (!IsOwner || IsHost)
        {
            enabled = false;
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
        
        // Find gun offset in the PlayerShoot component
        var playerShoot = GetComponent<PlayerShoot>();
        if (playerShoot != null)
        {
            // Find the _gunOffset field using reflection
            var field = playerShoot.GetType().GetField("_gunOffset", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                _gunOffset = field.GetValue(playerShoot) as Transform;
                Debug.Log("Found gun offset reference for client", this);
            }
        }
        
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
        
        Debug.Log("CLIENT INPUT HANDLER READY - USE ARROW KEYS TO MOVE, SPACE TO FIRE");
        
        // Send initial position to ensure sync
        StartCoroutine(InitialSyncCoroutine());
    }
    
    private IEnumerator InitialSyncCoroutine()
    {
        yield return new WaitForSeconds(1f);
        UpdatePositionServerRpc(transform.position, transform.rotation);
        Debug.Log("Client sent initial position sync");
    }
    
    void Update()
    {
        if (!IsOwner || IsHost) return;
        
        // Handle direct keyboard input for movement
        float horizontal = 0f;
        float vertical = 0f;
        
        // Arrow keys for movement
        if (Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        
        _currentMovement = new Vector2(horizontal, vertical);
        if (_currentMovement.magnitude > 1f)
            _currentMovement.Normalize();
        
        // Log movement input for debugging (only when it changes)
        if (_currentMovement != Vector2.zero && !_hasMoved)
        {
            Debug.Log($"CLIENT DIRECT INPUT: {_currentMovement}", this);
            _hasMoved = true;
        }
        else if (_currentMovement == Vector2.zero)
        {
            _hasMoved = false;
        }
        
        // Handle space for firing
        bool isSpacePressed = Input.GetKey(KeyCode.Space);
        
        // Track button state changes
        if (isSpacePressed && !_fireButtonDown)
        {
            _fireButtonDown = true;
            _fireThisFrame = true;
            Debug.Log("CLIENT: Space pressed for firing");
        }
        else if (!isSpacePressed && _fireButtonDown)
        {
            _fireButtonDown = false;
        }
        
        // Handle firing logic
        if (_fireThisFrame || _fireButtonDown)
        {
            float timeSinceLastFire = Time.time - _lastFireTime;
            
            if (timeSinceLastFire >= _timeBetweenShots)
            {
                FireBulletServerRpc();
                _lastFireTime = Time.time;
            }
            
            _fireThisFrame = false;
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
        
        // Apply movement
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
            
            // Only sync if we've moved significantly
            if (Vector3.Distance(_lastPosition, transform.position) > 0.1f || Time.frameCount % 30 == 0)
            {
                // Send position update to server
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
        // Find player shoot component
        var playerShoot = GetComponent<PlayerShoot>();
        if (playerShoot == null) return;
        
        // Find the RequestShootServerRpc method
        var method = playerShoot.GetType().GetMethod("RequestShootServerRpc", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        if (method != null && _gunOffset != null)
        {
            // Invoke the shoot method
            method.Invoke(playerShoot, new object[] { _gunOffset.position, transform.rotation });
            Debug.Log("Client fired bullet through server RPC");
        }
    }
} 