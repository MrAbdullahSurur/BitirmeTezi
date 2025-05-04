using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

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

    // Ağ Değişkeni: Görsellerin aktif olup olmadığını senkronize eder
    private NetworkVariable<bool> _networkVisualsActive = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner // Sadece sahip değiştirebilir
    );

    private Rigidbody2D _rigidbody;
    private Vector2 _movementInput;
    private Vector2 _smoothedMovementInput;
    private Vector2 _movementInputSmoothVelocity;
    private Camera _camera;
    private Animator _animator;

    public override void OnNetworkSpawn()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        _networkVisualsActive.OnValueChanged += OnVisualsActiveChanged;
        UpdateVisualsState(_networkVisualsActive.Value);

        // --- Renk Değişikliği Başlangıcı ---
        if (IsClient && !IsHost) // Sadece Host olmayan Client'lar için
        {
            if (_playerVisuals != null)
            {
                SpriteRenderer spriteRenderer = _playerVisuals.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.green;
                    Debug.Log("Client oyuncu rengi yeşil yapıldı.", this);
                }
                else
                {
                    Debug.LogWarning("_playerVisuals içinde SpriteRenderer bulunamadı!", this);
                }
            }
            // _playerVisuals null ise hata zaten UpdateVisualsState içinde loglanabilir veya OnNetworkSpawn başında loglanmıştı.
        }
        // --- Renk Değişikliği Sonu ---
        
        if (IsOwner)
        {
            _camera = Camera.main; 
            if (_camera == null) { Debug.LogError("Sahip oyuncu için Main Camera bulunamadı!"); }
            
            StartCoroutine(ActivateVisualsAfterFade()); 
        }
        // Eski else bloğu (non-owner için StartCoroutine) kaldırıldı çünkü NetworkVariable kullanıyoruz
        // else
        // {
        //     // Sahip olmayan oyuncular için de fade kontrolü yapalım ki onlar da doğru zamanda görünsün
        //      StartCoroutine(CheckFadeCompletion()); 
        // }
        
        if (!ActivePlayers.Contains(this)) { ActivePlayers.Add(this); }
    }

    public override void OnNetworkDespawn()
    {
        // Olay aboneliğini kaldır
        _networkVisualsActive.OnValueChanged -= OnVisualsActiveChanged;
        
        // Aktif oyuncular listesinden çıkar
        if (ActivePlayers.Contains(this))
        {
            ActivePlayers.Remove(this);
        }
        base.OnNetworkDespawn(); // Temel sınıfın metodunu çağırmayı unutma
    }

    // Bu metod NetworkVariable değiştiğinde çağrılır
    private void OnVisualsActiveChanged(bool previousValue, bool newValue)
    {
        UpdateVisualsState(newValue);
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

    // Sadece Sahip tarafından çalıştırılır: Fade bitince NetworkVariable'ı günceller
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
    }
}
