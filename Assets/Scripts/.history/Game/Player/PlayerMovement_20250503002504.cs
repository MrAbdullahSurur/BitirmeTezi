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
    private float _interpolationSpeed = 15f; // Adjust for desired smoothness

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

    // Add a failsafe initialization for client movement
    private IEnumerator ForceClientMovementInitialization()
    {
        // Wait a frame to ensure everything is ready
        yield return null;
        
        if (IsOwner && !IsHost)
        {
            // Verify Rigidbody2D settings for client
            if (_rigidbody != null)
            {
                _rigidbody.simulated = true;
                _rigidbody.isKinematic = false;
                _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
                
                Debug.Log($"Client {OwnerClientId}: Forced Rigidbody2D initialization", this);
                
                // Test movement pulse to verify physics
                _rigidbody.AddForce(Vector2.up * 0.1f, ForceMode2D.Impulse);
            }
            
            // Verify PlayerInput is properly enabled
            if (_playerInput != null)
            {
                _playerInput.enabled = true;
                if (_playerInput.currentActionMap?.name != "PlayerArrows")
                {
                    _playerInput.SwitchCurrentActionMap("PlayerArrows");
                    Debug.Log($"Client {OwnerClientId}: Forced PlayerArrows control scheme", this);
                }
            }
        }
    }

    // Client movement verification - modify existing method
    private IEnumerator VerifyClientMovement()
    {
        yield return new WaitForSeconds(0.5f);
        if (!IsHost && IsOwner)
        {
            // Force client authority
            Debug.Log($"Client {OwnerClientId}: Verifying movement setup...", this);
            
            // Run failsafe initialization
            StartCoroutine(ForceClientMovementInitialization());
            
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
        
        // HOST movement logic (remains in FixedUpdate)
        if (IsHost)
        {
             SetPlayerVelocity();
             RotateInDirectionOfInput();
        }
        // CLIENT movement logic (moved to FixedUpdate)
        else if (!IsHost && _movementInput != Vector2.zero)
        {
            // Apply SmoothDamp to the input for local movement feel
            _smoothedMovementInput = Vector2.SmoothDamp(
                _smoothedMovementInput, 
                _movementInput, 
                ref _movementInputSmoothVelocity, 
                0.1f); // Smoothing time

            // 1. Calculate desired position using smoothed input
            Vector3 moveDelta = new Vector3(_smoothedMovementInput.x, _smoothedMovementInput.y, 0) * _speed * Time.fixedDeltaTime; 
            Vector3 desiredPosition = transform.position + moveDelta;
            
            // 2. Apply position locally for responsiveness
            transform.position = desiredPosition;
            
            // 3. Handle rotation locally using smoothed input
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _smoothedMovementInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime); 
            
            // 4. Request server sync periodically (e.g., every 3 fixed updates)
            // Send the actual calculated position and rotation
            if (Time.frameCount % 3 == 0) 
            {
                RequestTeleportServerRpc(transform.position, transform.rotation);
                // Log only when sending RPC
                //Debug.Log($"Client requesting sync. Pos: {transform.position}, Rot: {transform.rotation.eulerAngles}");
            }
        }
        // If client is not moving, ensure smoothed input goes to zero
        else if (!IsHost && _movementInput == Vector2.zero)
        {
            _smoothedMovementInput = Vector2.SmoothDamp(
                _smoothedMovementInput, 
                Vector2.zero, 
                ref _movementInputSmoothVelocity, 
                0.1f);
        }
        
        // Animation update for both host and client owner
        // Use the RAW input for animation trigger, not smoothed
        SetAnimation(_movementInput != Vector2.zero);
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
        // This method is now ONLY for the HOST
        if (!IsHost || !IsOwner) return;
        
        _smoothedMovementInput = Vector2.SmoothDamp(_smoothedMovementInput, _movementInput, ref _movementInputSmoothVelocity, 0.1f);
        _rigidbody.velocity = _smoothedMovementInput * _speed;
            
        if (_movementInput != Vector2.zero)
        {
             //Debug.Log($"Host velocity set to: {_rigidbody.velocity}");
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
        // This is now ONLY for the HOST
        if (!IsHost || !IsOwner) return;
        
        if(_movementInput != Vector2.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _smoothedMovementInput);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            _rigidbody.MoveRotation(rotation); // Host uses Rigidbody rotation
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

    private void ForceDisableNetworkRigidbody()
    {
        if (!IsOwner || !IsClient) return;
        
        Debug.Log($"Player {OwnerClientId}: Forcefully disabling network components...");
        
        // Disable all network components that might interfere with movement
        var networkComponents = new System.Type[] {
            typeof(Unity.Netcode.Components.NetworkRigidbody2D),
            typeof(Unity.Netcode.Components.NetworkTransform)
        };
        
        foreach (var componentType in networkComponents)
        {
            var component = GetComponent(componentType) as Behaviour;
            if (component != null)
            {
                component.enabled = false;
                Debug.Log($"Player {OwnerClientId}: Disabled {componentType.Name}");
            }
        }
        
        // For the client's player:
        if (IsOwner && !IsHost)
        {
            // Set Rigidbody to kinematic and disable simulation to prevent physics interference
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.simulated = false; // Disable physics simulation entirely
                _rigidbody.velocity = Vector2.zero;
                Debug.Log($"Player {OwnerClientId}: Set client Rigidbody2D to kinematic AND disabled simulation");
            }
        }
    }

    // Add a specific startup method for client
    private void ClientStartup()
    {
        if (!IsOwner || IsHost) return;
        
        Debug.Log($"Client {OwnerClientId}: Running special client startup");
        
        // Force disable all network physics components
        ForceDisableNetworkRigidbody();
        
        // Force enable input
        if (_playerInput != null)
        {
            _playerInput.enabled = true;
            _playerInput.SwitchCurrentActionMap("PlayerArrows");
            Debug.Log($"Client {OwnerClientId}: Forced PlayerInput to enabled state with PlayerArrows map");
        }
        
        // Test RPC connection
        TestRpcServerRpc();
    }

    [ServerRpc]
    private void TestRpcServerRpc()
    {
        Debug.Log($"SERVER received test RPC from player {OwnerClientId}");
        TestRpcResponseClientRpc();
    }
    
    [ClientRpc]
    private void TestRpcResponseClientRpc()
    {
        if (IsOwner && !IsHost)
        {
            Debug.Log($"CLIENT {OwnerClientId} received RPC response - connection working!");
        }
    }

    // Remove client movement logic from Update
    private void Update()
    {
        // Interpolate non-owner player movement
        if (!IsOwner)
        {
             // Smoothly move towards the network target position and rotation
             transform.position = Vector3.Lerp(transform.position, _networkPositionTarget, Time.deltaTime * _interpolationSpeed);
             transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotationTarget, Time.deltaTime * _interpolationSpeed);
        }
        
        // NOTE: Keep input reading (OnMove) as it is, it updates _movementInput
    }

    // Modify ServerRpc to accept rotation as well
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

    // Modify ClientRpc to accept rotation and store targets
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
}
