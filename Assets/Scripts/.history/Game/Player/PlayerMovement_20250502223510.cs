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
            _camera = Camera.main; 
            if (_camera == null) { Debug.LogError("Sahip oyuncu için Main Camera bulunamadı!"); }
            
            // --- PlayerInput'u ve Doğru Action Map'i Etkinleştir ---
            _playerInput.enabled = true; // PlayerInput bileşenini sadece sahip için etkinleştir
            if (IsHost)
            {
                // Debug.Log("Kontrol şeması: PlayerWASD", this); // Logları temizleyebiliriz
            }
            else 
            {
                 // Debug.Log("Kontrol şeması: PlayerArrows", this); // Logları temizleyebiliriz
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

    private void SetPlayerVelocity()
    {
        _smoothedMovementInput = Vector2.SmoothDamp(_smoothedMovementInput, _movementInput, ref _movementInputSmoothVelocity, 0.1f);
        _rigidbody.velocity = _smoothedMovementInput * _speed;
        
        // --- LOG EKLE ---
        if (IsOwner && _rigidbody.velocity != Vector2.zero)
        {
            Debug.Log($"[{Time.frameCount}] SetPlayerVelocity: Velocity ayarlandı: {_rigidbody.velocity} (Input: {_movementInput}, Speed: {_speed})", this);
        }
        // --- LOG SONU ---

        PreventPlayerGoingOffScreen();
    }

    private void PreventPlayerGoingOffScreen()
    {
        if (_camera == null) return;
        
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
        
        // --- LOG TEMİZLE --- 
        // Debug.Log($"[{Time.frameCount}] OnMove: Input alındı: {_movementInput}", this);
        // --- LOG SONU ---
    }
}
