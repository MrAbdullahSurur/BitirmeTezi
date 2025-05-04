using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Image için eklendi

[RequireComponent(typeof(Image))] // Foreground Image olmalı
public class HealthBarUI : MonoBehaviour
{
    // Static referans (Singleton pattern benzeri)
    public static HealthBarUI Instance { get; private set; }
    
    [SerializeField]
    private Image _healthBarForegroundImage;
    
    // Referans alınacak HealthController
    // Bu genellikle Player prefab'ına eklenir ve Player spawn olduğunda ayarlanır.
    // Veya HealthController Start/Awake içinde bu UI'ı bulup referansı ayarlayabilir.
    private HealthController _trackedHealthController;

    private void Awake()
    {
        // Singleton kontrolü - Sahnedeki tek HealthBarUI bu olmalı
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple HealthBarUI instances found. Destroying duplicate.", gameObject);
            Destroy(gameObject);
            return;
        }
        Instance = this; // Static referansı ayarla
        Debug.Log("HealthBarUI.Instance set.", gameObject); // Log eklendi
        
        // Eğer Inspector'da atanmamışsa kendini bulmaya çalış
        if (_healthBarForegroundImage == null)
        {
            _healthBarForegroundImage = GetComponent<Image>();
        }
        // Başlangıçta barı gizle veya varsayılan değere ayarla
        UpdateFillAmount(1f); 
    }

    // Bu metod dışarıdan çağrılmalı (örn. Player spawn olduğunda)
    public void SetTrackedHealthController(HealthController healthController)
    {
        // Eğer daha önce bir controller takip ediliyorsa, olay aboneliğini kaldır
        if (_trackedHealthController != null)
        { 
            _trackedHealthController.OnHealthChanged.RemoveListener(HandleHealthChanged);
        }
        
        _trackedHealthController = healthController;
        
        if (_trackedHealthController != null)
        {
            // Log eklendi - Hangi HealthController'ı takip ettiğimizi görelim
            Debug.Log($"HealthBarUI now tracking HealthController on GameObject: {_trackedHealthController.gameObject.name} (Owner: {_trackedHealthController.OwnerClientId})", gameObject); 
            _trackedHealthController.OnHealthChanged.AddListener(HandleHealthChanged);
            // Mevcut can durumunu hemen UI'a yansıt
            HandleHealthChanged(); 
        }
        else
        {
             Debug.LogWarning("HealthBarUI received a null HealthController to track.", gameObject); // Log eklendi
             UpdateFillAmount(0f);
        }
    }

    private void HandleHealthChanged()
    {
        if (_trackedHealthController != null)
        {
             float fillAmount = _trackedHealthController.RemainingHealthPercentage;
             // Log eklendi - Güncelleme çağrıldığında ve hesaplanan değeri görelim
             Debug.Log($"HealthBarUI HandleHealthChanged called for {_trackedHealthController.gameObject.name}. New Fill Amount: {fillAmount}", gameObject);
             UpdateFillAmount(fillAmount);
        }
    }
    
    private void UpdateFillAmount(float fillAmount)
    {
         if (_healthBarForegroundImage != null)
         {
              _healthBarForegroundImage.fillAmount = fillAmount;
         }
    }
    
    // Component devre dışı kaldığında veya yok olduğunda aboneliği kaldır
    private void OnDisable()
    {
         if (_trackedHealthController != null)
         {
             _trackedHealthController.OnHealthChanged.RemoveListener(HandleHealthChanged);
         }
    }
    private void OnDestroy()
    {
         if (_trackedHealthController != null)
         { 
             _trackedHealthController.OnHealthChanged.RemoveListener(HandleHealthChanged);
         }
         // Static referansı temizle
         if (Instance == this) Instance = null; 
    }
}
