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
        Debug.Log("StartHost çağrıldı.");
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("StartHost başlangıcında NetworkManager.Singleton null!");
            return;
        }

        bool hostStarted = NetworkManager.Singleton.StartHost();
        Debug.Log($"NetworkManager.Singleton.StartHost() sonucu: {hostStarted}");

        if (hostStarted)
        {
            Debug.Log("Host başarıyla başlatıldı. SceneManager kontrol ediliyor...");
            
            // Tekrar Singleton kontrolü yap
            if (NetworkManager.Singleton == null)
            {
                 Debug.LogError("SceneManager kontrolünden HEMEN ÖNCE NetworkManager.Singleton null oldu!");
                 return;
            }
            
            if (NetworkManager.Singleton.SceneManager == null)
            {
                Debug.LogError("NetworkManager.SceneManager referansı null! NetworkManager ayarlarını kontrol edin.");
                return; 
            }
            
            Debug.Log("SceneManager geçerli. Sahne yükleniyor...");
            NetworkManager.Singleton.SceneManager.LoadScene("HalfLifeMultiplayer", LoadSceneMode.Single);
            Debug.Log("LoadScene çağrısı yapıldı.");
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