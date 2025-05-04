using UnityEngine;
using Unity.Netcode;

/// <summary>
/// NetworkManager'a otomatik olarak ClientInputFixer ekleyen component.
/// Oyunun ana sahnesine ekleyin.
/// </summary>
public class NetworkManagerSetup : MonoBehaviour
{
    private void Start()
    {
        // NetworkManager'ı bul
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            // NetworkManager'da ClientInputFixer var mı kontrol et
            if (networkManager.GetComponent<ClientInputFixer>() == null)
            {
                // Eğer yoksa ekle
                networkManager.gameObject.AddComponent<ClientInputFixer>();
                Debug.Log("ClientInputFixer added to NetworkManager");
            }
            
            // ClientFixUtility'yi de ekle
            if (networkManager.GetComponent<ClientFixUtility>() == null)
            {
                networkManager.gameObject.AddComponent<ClientFixUtility>();
                Debug.Log("ClientFixUtility added to NetworkManager");
            }
        }
        else
        {
            Debug.LogWarning("NetworkManager not found in scene!");
        }
    }
} 