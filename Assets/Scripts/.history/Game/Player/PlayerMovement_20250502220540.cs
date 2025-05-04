using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
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

    private Rigidbody2D _rigidbody;
    private Vector2 _movementInput;
    private Vector2 _smoothedMovementInput;
    private Vector2 _movementInputSmoothVelocity;
    private Camera _camera;
    private Animator _animator;
    private bool _visualsActivated = false;

    public override void OnNetworkSpawn()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        if (_playerVisuals != null)
        {
           _playerVisuals.SetActive(false);
        }
        else
        {
           Debug.LogError("PlayerMovement: _playerVisuals referansı atanmamış!", this);
        }
        
        _visualsActivated = false;

        if (IsOwner)
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("Sahip oyuncu için Main Camera bulunamadı!");
            }
            StartCoroutine(CheckFadeCompletion());
        }
        else
        {
            StartCoroutine(CheckFadeCompletion());
        }
    }

    private IEnumerator CheckFadeCompletion()
    {
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

        if (_playerVisuals != null && !_visualsActivated)
        {
            _playerVisuals.SetActive(true);
            _visualsActivated = true;
            Debug.Log("Oyuncu görselleri etkinleştirildi.", this);
        }
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
