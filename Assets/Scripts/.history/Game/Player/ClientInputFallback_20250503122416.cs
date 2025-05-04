using UnityEngine;
using Unity.Netcode;

/// <summary>
/// A simple fallback for client input that completely bypasses the Input System
/// Use this as a last resort if ClientInputHandler doesn't work
/// </summary>
public class ClientInputFallback : NetworkBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 200f;
    
    private Camera _mainCamera;
    private Animator _animator;
    private bool _isMoving = false;
    
    public override void OnNetworkSpawn()
    {
        // Only activate for the client player, not the host
        if (!IsOwner || IsHost)
        {
            enabled = false;
            return;
        }
        
        _mainCamera = Camera.main;
        _animator = GetComponent<Animator>();
        
        Debug.Log("ClientInputFallback activated as an emergency input handler");
    }
    
    private void Update()
    {
        if (!IsOwner || IsHost) return;
        
        _isMoving = false;
        
        // Handle movement with arrow keys
        Vector3 moveDirection = Vector3.zero;
        
        if (Input.GetKey(KeyCode.UpArrow)) 
        {
            moveDirection.y += 1;
            _isMoving = true;
        }
        if (Input.GetKey(KeyCode.DownArrow)) 
        {
            moveDirection.y -= 1;
            _isMoving = true;
        }
        if (Input.GetKey(KeyCode.LeftArrow)) 
        {
            moveDirection.x -= 1;
            _isMoving = true;
        }
        if (Input.GetKey(KeyCode.RightArrow)) 
        {
            moveDirection.x += 1;
            _isMoving = true;
        }
        
        // Normalize for diagonal movement
        if (moveDirection.magnitude > 1)
            moveDirection.Normalize();
        
        // Apply movement
        transform.position += moveDirection * _moveSpeed * Time.deltaTime;
        
        // Handle rotation
        if (_isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
        
        // Update animation
        if (_animator != null)
        {
            _animator.SetBool("IsMoving", _isMoving);
        }
        
        // Handle firing with Space
        if (Input.GetKeyDown(KeyCode.Space))
        {
            FireBullet();
        }
        
        // Keep position in bounds
        KeepInBounds();
        
        // Send position to server
        if (Time.frameCount % 2 == 0)
        {
            SyncPositionServerRpc(transform.position, transform.rotation);
        }
    }
    
    private void KeepInBounds()
    {
        if (_mainCamera == null) return;
        
        Vector3 pos = transform.position;
        Vector3 viewportPos = _mainCamera.WorldToViewportPoint(pos);
        
        viewportPos.x = Mathf.Clamp(viewportPos.x, 0.05f, 0.95f);
        viewportPos.y = Mathf.Clamp(viewportPos.y, 0.05f, 0.95f);
        
        transform.position = _mainCamera.ViewportToWorldPoint(viewportPos);
    }
    
    private void FireBullet()
    {
        Debug.Log("Client attempting to fire bullet");
        FireBulletServerRpc(transform.position, transform.rotation);
    }
    
    [ServerRpc]
    private void FireBulletServerRpc(Vector3 position, Quaternion rotation)
    {
        Debug.Log("Server received bullet fire request from client");
        
        // Find bullet prefab in resources
        GameObject bulletPrefab = Resources.Load<GameObject>("Bullet");
        if (bulletPrefab == null)
        {
            Debug.LogError("Could not find Bullet prefab in Resources folder");
            return;
        }
        
        // Instantiate and spawn bullet
        GameObject bullet = Instantiate(bulletPrefab, position, rotation);
        NetworkObject networkObj = bullet.GetComponent<NetworkObject>();
        
        if (networkObj != null)
        {
            // Add velocity to bullet
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = rotation * Vector3.up * 10f;
            }
            
            // Spawn on network
            networkObj.Spawn();
        }
        else
        {
            Debug.LogError("Bullet prefab is missing NetworkObject component");
            Destroy(bullet);
        }
    }
    
    [ServerRpc]
    private void SyncPositionServerRpc(Vector3 position, Quaternion rotation)
    {
        // Update on server
        transform.position = position;
        transform.rotation = rotation;
        
        // Broadcast to other clients
        SyncPositionClientRpc(position, rotation);
    }
    
    [ClientRpc]
    private void SyncPositionClientRpc(Vector3 position, Quaternion rotation)
    {
        // Only update for non-owners
        if (!IsOwner)
        {
            transform.position = position;
            transform.rotation = rotation;
        }
    }
} 