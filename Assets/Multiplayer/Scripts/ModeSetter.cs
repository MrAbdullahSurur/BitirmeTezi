using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ModeSetter : MonoBehaviour
{
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }
}
