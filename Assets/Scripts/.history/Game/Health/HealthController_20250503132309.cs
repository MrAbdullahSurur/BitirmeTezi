using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class HealthController : NetworkBehaviour
{
    [SerializeField]
    private NetworkVariable<float> _networkCurrentHealth = new NetworkVariable<float>(
        default, 
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [SerializeField]
    private float _maximumHealth;

    public float RemainingHealthPercentage
    {
        get
        {
            if (_maximumHealth <= 0) return 0;
            return _networkCurrentHealth.Value / _maximumHealth;
        }
    }

    public bool IsInvincible { get; set; }

    public UnityEvent OnDied;

    public UnityEvent OnDamaged;

    public UnityEvent OnHealthChanged;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _networkCurrentHealth.Value = _maximumHealth;
        }
        _networkCurrentHealth.OnValueChanged += HealthChanged;
        HealthChanged(_networkCurrentHealth.Value, _networkCurrentHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        _networkCurrentHealth.OnValueChanged -= HealthChanged;
    }

    private void HealthChanged(float previousValue, float newValue)
    {
        Debug.Log($"HealthController on {gameObject.name} (Owner: {OwnerClientId}) HealthChanged: {previousValue} -> {newValue}. Invoking OnHealthChanged event.", gameObject);
        OnHealthChanged.Invoke();
        if (newValue < previousValue && newValue > 0)
        {
            OnDamaged.Invoke();
        }
        if (newValue <= 0 && previousValue > 0)
        {
            OnDied.Invoke();
        }
    }

    public void RequestTakeDamage(float damageAmount)
    {
        if (!enabled || !gameObject.activeSelf) return;
        TakeDamageServerRpc(damageAmount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(float damageAmount)
    {
        if (_networkCurrentHealth.Value <= 0 || IsInvincible)
        {
            return;
        }

        _networkCurrentHealth.Value -= damageAmount;

        if (_networkCurrentHealth.Value < 0)
        {
            _networkCurrentHealth.Value = 0;
        }
    }

    public void AddHealth(float amountToAdd)
    {
        if (!enabled || !gameObject.activeSelf) return;
        AddHealthServerRpc(amountToAdd);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddHealthServerRpc(float amountToAdd)
    {
        if (_networkCurrentHealth.Value >= _maximumHealth)
        {
            return;
        }
        _networkCurrentHealth.Value += amountToAdd;
        if (_networkCurrentHealth.Value > _maximumHealth)
        {    
            _networkCurrentHealth.Value = _maximumHealth;
        }
    }
}