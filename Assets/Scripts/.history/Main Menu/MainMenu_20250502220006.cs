using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MainMenu : MonoBehaviour
{
    // SceneController referansına artık gerek yok
    // [SerializeField]
    // private SceneController _sceneController;

    public void StartHost()
    {
        // Önce Host'u başlat
        if (NetworkManager.Singleton.StartHost())
        {
            // Ekle: SceneManager null mu kontrol et
            if (NetworkManager.Singleton.SceneManager == null)
            {
                Debug.LogError("NetworkManager.SceneManager referansı null! NetworkManager ayarlarını kontrol edin.");
                return; // Hata varsa devam etme
            }
            
            // Başarılı olursa oyun sahnesini NetworkManager aracılığıyla yükle
            NetworkManager.Singleton.SceneManager.LoadScene("HalfLifeMultiplayer", LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("Host başlatılamadı!");
        }
    }

    public void StartClient()
    {
        // Sadece Client'ı başlat. Sahne yüklemesi otomatik olacak.
        NetworkManager.Singleton.StartClient();
    }

    public void Exit()
    {
        Application.Quit();
    }
}