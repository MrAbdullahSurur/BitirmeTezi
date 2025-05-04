using UnityEngine;
using Unity.Netcode;

public class InputDebuggerSetup : MonoBehaviour
{
    [SerializeField] private GameObject _inputDebuggerPrefab;
    private bool _hasSpawned = false;
    
    private void Awake()
    {
        // Create a simple prefab on scene load
        if (_inputDebuggerPrefab == null)
        {
            GameObject debuggerObj = new GameObject("InputDebugger");
            debuggerObj.AddComponent<InputDebugger>();
            _inputDebuggerPrefab = debuggerObj;
            DontDestroyOnLoad(debuggerObj);
        }
        else
        {
            Instantiate(_inputDebuggerPrefab);
        }
    }
    
    private void Update()
    {
        // Adding a keyboard shortcut to manually add the debugger
        if (Input.GetKeyDown(KeyCode.F1) && !_hasSpawned)
        {
            _hasSpawned = true;
            GameObject debuggerObj = new GameObject("InputDebugger_Manual");
            debuggerObj.AddComponent<InputDebugger>();
            Debug.Log("Input debugger manually added to scene");
        }
    }
} 