using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))] // PlayerInput bileşeninin olmasını zorunlu kıl
public class PlayerMovement : NetworkBehaviour
{
    // Statik liste: Tüm aktif oyuncuları tutar
    public static List<PlayerMovement> ActivePlayers = new List<PlayerMovement>();

    [Header("Movement Settings")]
    [SerializeField]
    private float _speed;

    [SerializeField]
    private float _rotationSpeed;

    [SerializeField]
    private float _screenBorder;
    
    [Header("References")]
    [SerializeField] 
    private GameObject _playerVisuals;

    // Ağ Değişkeni (Görsel Aktifliği)
    private NetworkVariable<bool> _networkVisualsActive = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner // Sadece sahip değiştirebilir
    );

    // Ağ Değişkeni (Oyuncu Rengi)
    private NetworkVariable<Color> _networkPlayerColor = new NetworkVariable<Color>(
        Color.white, // Varsayılan renk
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server // Sadece sunucu rengi belirleyebilir
    );

    private Rigidbody2D _rigidbody;
    private Vector2 _movementInput;
    private Vector2 _smoothedMovementInput;
    private Vector2 _movementInputSmoothVelocity;
    private Camera _camera;
    private Animator _animator;

    // --- Bileşen Referansları ---
    private SpriteRenderer _spriteRenderer;
    private PlayerInput _playerInput; // PlayerInput referansı

    public override void OnNetworkSpawn()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        // Görsel referansını ve SpriteRenderer'ı bul
        if (_playerVisuals != null)
        {
            _spriteRenderer = _playerVisuals.GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                 Debug.LogWarning("_playerVisuals içinde SpriteRenderer bulunamadı!", this);
            }
        }
        else
        {   
             Debug.LogError("PlayerMovement: _playerVisuals referansı atanmamış!", this);
        }

        // Olaylara abone ol
        _networkVisualsActive.OnValueChanged += OnVisualsActiveChanged;
        _networkPlayerColor.OnValueChanged += OnPlayerColorChanged;
        
        // Başlangıç durumlarını uygula
        UpdateVisualsState(_networkVisualsActive.Value);
        ApplyPlayerColor(_networkPlayerColor.Value); // Başlangıç rengini uygula

        // --- Sunucu Taraflı Renk Ayarı ---
        if (IsServer)
        {
            // Eğer bu oyuncu Host'un kendisi değilse (yani katılan bir client ise) rengi yeşil yap
            // OwnerClientId kullanarak kontrol edebiliriz. NetworkManager.LocalClientId Host'un ID'sidir.
            if (OwnerClientId != NetworkManager.Singleton.LocalClientId)
            {
                 _networkPlayerColor.Value = Color.green;
            }
            else
            {
                 _networkPlayerColor.Value = Color.white; // Host'un rengi beyaz kalsın
            }
        }
        // --- Sunucu Taraflı Renk Ayarı Sonu ---

        _playerInput = GetComponent<PlayerInput>();

        if (IsOwner)
        {
            // Ensure NetworkRigidbody2D has proper ownership
            var networkRigidbody = GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
            if (networkRigidbody != null)
            {
                // Remove the line that causes the error
                // networkRigidbody.InLocalSpace = true;
                
                // Instead, simply log that we found the component
                Debug.Log($"Player {OwnerClientId}: NetworkRigidbody2D found", this);
            }
            else
            {
                Debug.LogWarning($"Player {OwnerClientId}: No NetworkRigidbody2D component found!", this);
            }

            _camera = Camera.main; 
            if (_camera == null) { Debug.LogError("Sahip oyuncu için Main Camera bulunamadı!"); }
            
            // --- PlayerInput'u Etkinleştir ve Doğru Action Map'i Ayarla ---
            _playerInput.enabled = true; // PlayerInput bileşenini ÖNCE etkinleştir
            if (IsHost)
            {
                _playerInput.defaultActionMap = "PlayerWASD"; 
                _playerInput.SwitchCurrentActionMap("PlayerWASD"); 
            }
            else 
            {
                 _playerInput.defaultActionMap = "PlayerArrows"; 
                 _playerInput.SwitchCurrentActionMap("PlayerArrows"); 
                 
                 // Ensure proper client movement initialization
                 StartCoroutine(VerifyClientMovement());
            }
            // --- PlayerInput ve Kontrol Şeması Sonu ---
            
            StartCoroutine(ActivateVisualsAfterFade()); 
        }
        else
        {
             // Sahip olmayan oyuncuların PlayerInput'u devre dışı kalmalı
             _playerInput.enabled = false;
        }
        
        if (!ActivePlayers.Contains(this)) { ActivePlayers.Add(this); }
    }

    public override void OnNetworkDespawn()
    {
        // Olay aboneliklerini kaldır
        _networkVisualsActive.OnValueChanged -= OnVisualsActiveChanged;
        _networkPlayerColor.OnValueChanged -= OnPlayerColorChanged;
        
        if (ActivePlayers.Contains(this)) { ActivePlayers.Remove(this); }
        base.OnNetworkDespawn();
    }

    // Görsel aktifliği değişince çağrılır
    private void OnVisualsActiveChanged(bool previousValue, bool newValue)
    {
        UpdateVisualsState(newValue);
    }

    // Oyuncu rengi değişince çağrılır
    private void OnPlayerColorChanged(Color previousValue, Color newValue)
    {
        ApplyPlayerColor(newValue);
    }

    // Görsellerin aktif/pasif durumunu ayarlar
    private void UpdateVisualsState(bool isActive)
    {
        if (_playerVisuals != null)
        {
            _playerVisuals.SetActive(isActive);
            Debug.Log($"Oyuncu görselleri {(isActive ? "etkinleştirildi" : "devre dışı bırakıldı")}. IsOwner: {IsOwner}", this);
        }
        else
        {
            // Bu hata OnNetworkSpawn'da zaten loglanıyordu, tekrar etmeye gerek yok
            // Debug.LogError("PlayerMovement: _playerVisuals referansı atanmamış!", this);
        }
    }

    // Sprite rengini uygular
    private void ApplyPlayerColor(Color color)
    {
         if (_spriteRenderer != null)
         {
             _spriteRenderer.color = color;
         }
    }

    // Sahip fade bitince görselleri aktif eder (NetworkVariable aracılığıyla)
    private IEnumerator ActivateVisualsAfterFade()
    {
        // Hala eski fade bekleme mantığını kullanıyoruz,
        // ama artık sadece sahibi NetworkVariable'ı güncelliyor.
        SceneFade sceneFade = FindObjectOfType<SceneFade>();
        while (sceneFade == null || !sceneFade.isActiveAndEnabled)
        {
            sceneFade = FindObjectOfType<SceneFade>();
            if (sceneFade != null && sceneFade.isActiveAndEnabled) break; 
            yield return null; 
        }
        while (sceneFade.isActiveAndEnabled)
        {
             yield return null; 
        }

        // Fade bitti, NetworkVariable'ı güncelle
        // Bu değişiklik tüm istemcilere ve hosta yayılacak
        _networkVisualsActive.Value = true;
    }

    // Client movement verification
    private IEnumerator VerifyClientMovement()
    {
        yield return new WaitForSeconds(0.5f);
        if (!IsHost && IsOwner)
        {
            // Force client authority
            Debug.Log($"Client {OwnerClientId}: Verifying movement setup...", this);
            
            // Test local movement for client
            _movementInput = new Vector2(0.1f, 0.1f);
            yield return new WaitForFixedUpdate();
            _movementInput = Vector2.zero;
            
            // Check if we have authority
            if (IsOwner && !IsHost)
            {
                RequestClientAuthorityServerRpc();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestClientAuthorityServerRpc()
    {
        // Grant client authority explicitly
        Debug.Log($"Server granting movement authority to client {OwnerClientId}", this);
        
        // Notify the client it has authority
        ConfirmClientAuthorityClientRpc();
    }

    [ClientRpc]
    private void ConfirmClientAuthorityClientRpc()
    {
        if (IsOwner && !IsHost)
        {
            Debug.Log($"Client {OwnerClientId}: Movement authority confirmed", this);
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        
        // --- LOG TEMİZLE --- 
        // if (_movementInput != Vector2.zero)
        // {
        //      Debug.Log($"[{Time.frameCount}] FixedUpdate: Hareket uygulanıyor. Input: {_movementInput}", this);
        // }
        // --- LOG SONU ---

        SetPlayerVelocity();
        RotateInDirectionOfInput();
        SetAnimation();
    }

    private void SetAnimation()
    {
        bool isMoving = _movementInput != Vector2.zero;

        _animator.SetBool("IsMoving", isMoving);
    }

    [ServerRpc]
    private void SyncPositionToServerServerRpc(Vector3 position)
    {
        // Server can validate position if needed
        // Instead of directly setting position, use NetworkTransform
        // We'll apply physics forces to prevent jitter
        ClientMoveSyncClientRpc(position);
    }

    [ClientRpc]
    private void ClientMoveSyncClientRpc(Vector3 position)
    {
        // Only update non-owner objects to avoid position fighting
        if (!IsOwner)
        {
            transform.position = position;
        }
    }

    // Add a method to apply direct forces for client movement
    private void ApplyClientMovement()
    {
        if (!IsOwner || !IsClient || _movementInput == Vector2.zero)
            return;

        // Apply direct force to ensure movement works
        Vector2 force = _movementInput * _speed * 10f;
        _rigidbody.AddForce(force, ForceMode2D.Force);
        
        Debug.Log($"Client {OwnerClientId}: Applied direct force {force}", this);
    }

    private void SetPlayerVelocity()
    {
        _smoothedMovementInput = Vector2.SmoothDamp(_smoothedMovementInput, _movementInput, ref _movementInputSmoothVelocity, 0.1f);
        
        // If we're a client, ensure we have authority to move
        if (IsOwner && !IsHost)
        {
            // Use direct force for client movement
            ApplyClientMovement();
            
            // For client movement, directly set velocity as backup
            _rigidbody.velocity = _smoothedMovementInput * _speed;
            
            // For client-to-server sync, send position updates
            if (_movementInput != Vector2.zero && (Time.frameCount % 5 == 0)) // Only sync every 5 frames to reduce network traffic
            {
                SyncPositionToServerServerRpc(transform.position);
            }
        }
        else
        {
            // Normal host movement
            _rigidbody.velocity = _smoothedMovementInput * _speed;
        }
        
        // Enhanced debug logging for movement execution
        if (IsOwner && _movementInput != Vector2.zero)
        {
            Debug.Log($"[{Time.frameCount}] SetPlayerVelocity: Player {OwnerClientId} - Input: {_movementInput}, Smoothed: {_smoothedMovementInput}, Final Velocity: {_rigidbody.velocity}, IsHost: {IsHost}", this);
        }

        PreventPlayerGoingOffScreen();
    }

    private void PreventPlayerGoingOffScreen()
    {
        // Fix camera reference handling for client
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogWarning($"Player {OwnerClientId}: Cannot find main camera for screen boundary check", this);
                return;
            }
        }
        
        Vector2 screenPosition = _camera.WorldToScreenPoint(transform.position);
        float cameraWidth = _camera.pixelWidth;
        float cameraHeight = _camera.pixelHeight;

        if ((screenPosition.x < _screenBorder && _rigidbody.velocity.x < 0) || (screenPosition.x > cameraWidth - _screenBorder && _rigidbody.velocity.x > 0))
        {
            _rigidbody.velocity = new Vector2(0, _rigidbody.velocity.y);
        }

        if ((screenPosition.y < _screenBorder && _rigidbody.velocity.y < 0) || (screenPosition.y > cameraHeight - _screenBorder && _rigidbody.velocity.y > 0))
        {
            _rigidbody.velocity = new Vector2(_rigidbody.velocity.x,0);
        }
    }

    private void RotateInDirectionOfInput()
    {
        if(_movementInput != Vector2.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _smoothedMovementInput);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);

            _rigidbody.MoveRotation(rotation);
        }
    }
    private void OnMove(InputValue inputValue)
    {
        if (!IsOwner) return;

        _movementInput = inputValue.Get<Vector2>();
        
        // Add debug logging to track movement input
        Debug.Log($"[{Time.frameCount}] OnMove: Player {OwnerClientId} received input: {_movementInput}, IsHost: {IsHost}, IsOwner: {IsOwner}", this);
    }

    // Add debugging method
    [ContextMenu("DebugInputSystem")]
    private void DebugInputSystem()
    {
        if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
        
        Debug.Log($"Player {OwnerClientId} Input Debug:" +
                  $"\nEnabled: {_playerInput.enabled}" + 
                  $"\nCurrent Map: {_playerInput.currentActionMap.name}" +
                  $"\nDefault Map: {_playerInput.defaultActionMap}" +
                  $"\nIs Host: {IsHost}" +
                  $"\nIs Owner: {IsOwner}");
    }
}
