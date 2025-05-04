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
    
    [Header("References")]
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private PlayerShoot _playerShoot;
    
    // Direct input handling fields
    private Vector2 _currentMovement;
    private bool _fireThisFrame = false;
    private bool _fireButtonDown = false;
    private float _lastFireTime = 0f;
    [SerializeField] private float _timeBetweenShots = 0.2f;
    
    private Rigidbody2D _rigidbody;
    private Camera _camera;
    private bool _isActive = false;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        
        // Ensure references are set
        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }
        
        if (_playerShoot == null)
        {
            _playerShoot = GetComponent<PlayerShoot>();
        }
    }
    
    public override void OnNetworkSpawn()
    {
        // Only activate for clients (not for host)
        if (!IsOwner || IsHost)
        {
            enabled = false;
            return;
        }
        
        Debug.Log("ClientInputHandler activated for client player", this);
        _isActive = true;
        
        // Get camera reference
        _camera = Camera.main;
        if (_camera == null)
        {
            Debug.LogError("Main camera not found!", this);
        }
        
        // Initialize references
        _lastFireTime = -_timeBetweenShots; // Allow immediate first shot
    }
    
    private void Update()
    {
        if (!_isActive) return;
        
        // Handle arrow key input directly
        HandleMovementInput();
        
        // Handle space key for firing
        HandleFireInput();
    }
    
    private void FixedUpdate()
    {
        if (!_isActive) return;
        
        // Apply movement directly
        ApplyMovement();
    }
    
    private void HandleMovementInput()
    {
        // Get arrow key input
        float horizontal = 0f;
        float vertical = 0f;
        
        if (Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        
        // Normalize the vector if moving diagonally
        _currentMovement = new Vector2(horizontal, vertical);
        if (_currentMovement.magnitude > 1f)
        {
            _currentMovement.Normalize();
        }
        
        // Log significant movements
        if (_currentMovement != Vector2.zero)
        {
            Debug.Log($"Client input: {_currentMovement}", this);
        }
    }
    
    private void HandleFireInput()
    {
        // Check for Space key for firing
        bool spacePressed = Input.GetKey(KeyCode.Space);
        
        // Track button down (first press)
        if (spacePressed && !_fireButtonDown)
        {
            _fireButtonDown = true;
            _fireThisFrame = true;
        }
        else if (!spacePressed && _fireButtonDown)
        {
            _fireButtonDown = false;
        }
        
        // Handle firing logic
        if (_fireButtonDown || _fireThisFrame)
        {
            float timeSinceLastFire = Time.time - _lastFireTime;
            
            if (timeSinceLastFire >= _timeBetweenShots)
            {
                // Directly send RPC through PlayerShoot
                if (_playerShoot != null)
                {
                    // Look for the RequestShootServerRpc method using reflection
                    var shootMethod = _playerShoot.GetType().GetMethod("RequestShootServerRpc", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (shootMethod != null)
                    {
                        // Get gun offset position from PlayerShoot
                        var gunOffsetField = _playerShoot.GetType().GetField("_gunOffset", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        Transform gunOffset = null;
                        if (gunOffsetField != null)
                        {
                            gunOffset = gunOffsetField.GetValue(_playerShoot) as Transform;
                        }
                        
                        // If we have all we need, invoke the shoot method
                        if (gunOffset != null)
                        {
                            shootMethod.Invoke(_playerShoot, new object[] { gunOffset.position, transform.rotation });
                            Debug.Log("Client fired bullet manually!", this);
                        }
                        else
                        {
                            Debug.LogError("Couldn't find gun offset reference!", this);
                        }
                    }
                    else
                    {
                        Debug.LogError("Couldn't find RequestShootServerRpc method!", this);
                    }
                }
                
                _lastFireTime = Time.time;
            }
            
            _fireThisFrame = false;
        }
    }
    
    private void ApplyMovement()
    {
        if (_currentMovement.sqrMagnitude < 0.01f) return;
        
        // Apply movement directly 
        Vector3 moveDelta = new Vector3(_currentMovement.x, _currentMovement.y, 0) * _speed * Time.fixedDeltaTime;
        Vector3 desiredPosition = transform.position + moveDelta;
        
        // Check screen boundaries
        if (_camera != null)
        {
            Vector2 screenPosition = _camera.WorldToScreenPoint(desiredPosition);
            bool outsideHorizontal = screenPosition.x < 50 || screenPosition.x > _camera.pixelWidth - 50;
            bool outsideVertical = screenPosition.y < 50 || screenPosition.y > _camera.pixelHeight - 50;
            
            if (outsideHorizontal)
            {
                desiredPosition.x = transform.position.x;
            }
            
            if (outsideVertical)
            {
                desiredPosition.y = transform.position.y;
            }
        }
        
        // Apply position
        transform.position = desiredPosition;
        
        // Handle rotation
        if (_currentMovement != Vector2.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _currentMovement);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
        }
        
        // Notify server of new position
        if (_playerMovement != null)
        {
            // Try to find and invoke the RequestTeleportServerRpc method
            var teleportMethod = _playerMovement.GetType().GetMethod("RequestTeleportServerRpc", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
            if (teleportMethod != null)
            {
                if (Time.frameCount % 2 == 0) // Only sync every 2 frames
                {
                    teleportMethod.Invoke(_playerMovement, new object[] { transform.position, transform.rotation });
                }
            }
        }
    }
} 