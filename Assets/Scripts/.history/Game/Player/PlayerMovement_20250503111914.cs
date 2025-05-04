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

    // --- Network Interpolation Data ---
    private Vector3 _networkPositionTarget;
    private Quaternion _networkRotationTarget;
    private float _interpolationSpeed = 12f; 

    // --- Bileşen Referansları ---
    private SpriteRenderer _spriteRenderer;
    private PlayerInput _playerInput; // PlayerInput referansı

    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player {OwnerClientId} spawned: IsHost={IsHost}, IsClient={IsClient}, IsOwner={IsOwner}");
        
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        // Initialize network interpolation targets
        if (!IsOwner)
        {
             _networkPositionTarget = transform.position;
             _networkRotationTarget = transform.rotation;
        }

        // If client, disable the NetworkRigidbody to prevent it from overriding client movement
        if (IsClient && !IsHost && IsOwner)
        {
            ForceDisableNetworkRigidbody();
        }

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
                if (!IsHost)
                {
                    // For client, disable NetworkRigidbody2D to prevent overrides
                    networkRigidbody.enabled = false;
                    Debug.Log($"Player {OwnerClientId}: Disabled NetworkRigidbody2D for client control", this);
                }
                else
                {
                    Debug.Log($"Player {OwnerClientId}: NetworkRigidbody2D found", this);
                }
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
                 
                 // Run special client initialization (after a short delay)
                 Invoke("ClientStartup", 0.5f);
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

        // --- IMPORTANT: Print player object hierarchy for debugging ---
        PrintObjectHierarchy();
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

    private void FixedUpdate()
    {
        if (!IsOwner) 
        {
            // Non-owner interpolation (Keep this as is)
            transform.position = Vector3.Lerp(transform.position, _networkPositionTarget, Time.fixedDeltaTime * _interpolationSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotationTarget, Time.fixedDeltaTime * _interpolationSpeed);
            return; 
        }
        
        // --- Owner Logic Below ---

        // Read input value DIRECTLY every FixedUpdate
        if (_playerInput.enabled)
        {
             _movementInput = _playerInput.actions["Move"].ReadValue<Vector2>();
             // Log raw input value for debugging
             // if (_movementInput != Vector2.zero) 
             // { 
             //      Debug.Log($"Owner {OwnerClientId} reading input: {_movementInput}"); 
             // }
        }
        else
        {
             _movementInput = Vector2.zero;
        }

        // Apply smoothing (for both Host and Client's local feel)
        _smoothedMovementInput = Vector2.SmoothDamp(
            _smoothedMovementInput, 
            _movementInput, 
            ref _movementInputSmoothVelocity, 
            0.1f); 

        // Apply movement using Rigidbody for BOTH Host and Client owners
        SetPlayerVelocity(); 
        RotateInDirectionOfInput();
        
        // Animation update for owner (based on raw input)
        SetAnimation(_movementInput != Vector2.zero);

        // Send sync data if CLIENT and moving (throttled)
        if (!IsHost && _smoothedMovementInput.sqrMagnitude > 0.001f && Time.frameCount % 2 == 0)
        {
            // Send transform data (even though we use NetworkRigidbody2D, this can help consistency)
             RequestTeleportServerRpc(transform.position, transform.rotation);
        }
    }

    private void SetPlayerVelocity()
    {
        // This method now applies velocity for the OWNER (Host or Client)
        if (!IsOwner) return; // Should not happen due to FixedUpdate check, but safe guard
        
        // Directly apply smoothed input to velocity
        _rigidbody.velocity = _smoothedMovementInput * _speed;
            
        // Log velocity if moving
        if (_smoothedMovementInput.sqrMagnitude > 0.001f)
        {
             // Debug.Log($"Owner {OwnerClientId} SetVelocity: {_rigidbody.velocity}");
        }
        
        // Screen boundary check for the owner
        PreventPlayerGoingOffScreen();
    }

    private void RotateInDirectionOfInput()
    {
        // This method now applies rotation for the OWNER (Host or Client)
        if (!IsOwner) return;
        
        // Rotate only if there is significant smoothed input
        if(_smoothedMovementInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _smoothedMovementInput);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            
            // Apply rotation via Rigidbody for smoother physics interaction
            _rigidbody.MoveRotation(rotation); 
        }
    }

    // Modify SetAnimation to accept moving state
    private void SetAnimation(bool isMoving)
    {
        // bool isMoving = _movementInput != Vector2.zero; // Original logic removed
        _animator.SetBool("IsMoving", isMoving);
    }

    [ServerRpc]
    private void SyncPositionToServerServerRpc(Vector3 position)
    {
        // On server, directly update position and tell all clients
        transform.position = position;
        
        // Notify all clients about this position
        SyncPositionToClientsClientRpc(position);
    }

    [ClientRpc]
    private void SyncPositionToClientsClientRpc(Vector3 position)
    {
        // Only update position for non-owner clients to avoid fighting with local control
        if (!IsOwner)
        {
            transform.position = position;
        }
    }

    [ServerRpc]
    private void SyncRotationToServerServerRpc(Quaternion rotation)
    {
        // Update rotation on server
        transform.rotation = rotation;
        
        // Broadcast to all clients
        SyncRotationToClientsClientRpc(rotation);
    }

    [ClientRpc]
    private void SyncRotationToClientsClientRpc(Quaternion rotation)
    {
        // Only update for non-owner objects
        if (!IsOwner)
        {
            transform.rotation = rotation;
        }
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

    // Debugging utility to print entire component list
    private void PrintObjectHierarchy()
    {
        var components = GetComponents<Component>();
        string componentList = $"Player {OwnerClientId} components:";
        
        foreach (var component in components)
        {
            componentList += $"\n - {component.GetType().Name} (Enabled: {(component is Behaviour b ? b.enabled.ToString() : "N/A")})";
        }
        
        Debug.Log(componentList);
    }

    // Keep the Teleport RPCs for now as a backup sync mechanism
    [ServerRpc]
    private void RequestTeleportServerRpc(Vector3 position, Quaternion rotation)
    {
        // On server: teleport player directly
        //Debug.Log($"Server received sync request. Pos: {position}, Rot: {rotation.eulerAngles} for player {OwnerClientId}");
        
        // Validate position/rotation if needed
        
        // Force update transform on server
        transform.position = position;
        transform.rotation = rotation;
        
        // Update all clients with new transform
        TeleportPlayerClientRpc(position, rotation);
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(Vector3 position, Quaternion rotation)
    {
        // Update transform target on non-owner clients
        if (!IsOwner)
        {
            //Debug.Log($"Client {NetworkManager.Singleton.LocalClientId} received sync for {OwnerClientId}. Pos: {position}");
            _networkPositionTarget = position;
            _networkRotationTarget = rotation;
            // Do NOT set transform directly anymore
            // transform.position = position;
            // transform.rotation = rotation;
        }
        // Animator update moved to FixedUpdate for owner
    }
}
