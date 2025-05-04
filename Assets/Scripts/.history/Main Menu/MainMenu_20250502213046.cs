using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MainMenu : MonoBehaviour
{
    [SerializeField]
    private SceneController _sceneController;

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        _sceneController.LoadScene("HalfLifeMultiplayer");
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }

    public void Exit()
    {
        Application.Quit();
    }
}