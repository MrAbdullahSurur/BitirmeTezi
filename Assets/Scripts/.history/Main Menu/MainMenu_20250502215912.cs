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