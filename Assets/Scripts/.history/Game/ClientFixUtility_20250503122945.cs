using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ClientFixUtility : MonoBehaviour
{
    [Tooltip("Key to press to fix client input issues")]
    [SerializeField] private KeyCode _fixKey = KeyCode.F12;
    
    [Tooltip("Enable debug overlay")]
    [SerializeField] private bool _showDebugOverlay = true;
    
    private bool _fixApplied = false;
    private string _statusMessage = "Press F12 to fix client input issues";
    
    void Update()
    {
        // Check for fix key press
        if (Input.GetKeyDown(_fixKey))
        {
            ApplyClientFix();
        }
    }
    
    private void ApplyClientFix()
    {
        _statusMessage = "Applying client fix...";
        Debug.Log("ClientFixUtility: Applying client fix");
        
        // Find all player objects in the scene
        var players = FindObjectsOfType<PlayerMovement>();
        int fixCount = 0;
        
        foreach (var player in players)
        {
            // Only fix client-owned players that aren't the host
            if (player.IsOwner && !player.IsHost)
            {
                Debug.Log($"Found client player to fix: {player.gameObject.name}");
                
                // 1. Disable PlayerInput component
                var playerInput = player.gameObject.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (playerInput != null)
                {
                    playerInput.enabled = false;
                    Debug.Log("Disabled PlayerInput component");
                }
                
                // 2. Disable NetworkRigidbody2D and NetworkTransform
                var networkRb = player.gameObject.GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
                if (networkRb != null)
                {
                    networkRb.enabled = false;
                    Debug.Log("Disabled NetworkRigidbody2D");
                }
                
                var networkTransform = player.gameObject.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                if (networkTransform != null)
                {
                    networkTransform.enabled = false;
                    Debug.Log("Disabled NetworkTransform");
                }
                
                // 3. Add/Enable ClientInputHandler
                ClientInputHandler inputHandler = player.gameObject.GetComponent<ClientInputHandler>();
                if (inputHandler == null)
                {
                    inputHandler = player.gameObject.AddComponent<ClientInputHandler>();
                    Debug.Log("Added ClientInputHandler");
                }
                else if (!inputHandler.enabled)
                {
                    inputHandler.enabled = true;
                    Debug.Log("Enabled existing ClientInputHandler");
                }
                
                fixCount++;
            }
        }
        
        _statusMessage = fixCount > 0 
            ? $"Fix applied to {fixCount} client player(s). Use arrow keys to move, space to fire." 
            : "No client players found to fix";
            
        _fixApplied = fixCount > 0;
        
        // Schedule status message to clear after some time
        Invoke("ClearStatusMessage", 5f);
    }
    
    private void ClearStatusMessage()
    {
        _statusMessage = _fixApplied 
            ? "Client fix active. Use arrow keys to move, space to fire." 
            : "Press F12 to fix client input issues";
    }
    
    private void OnGUI()
    {
        if (!_showDebugOverlay) return;
        
        // Draw status box in bottom-left corner
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.MiddleLeft;
        style.padding = new RectOffset(10, 10, 5, 5);
        
        // Draw background
        GUI.Box(new Rect(10, Screen.height - 40, 400, 30), "", style);
        
        // Draw text
        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.alignment = TextAnchor.MiddleLeft;
        textStyle.fontStyle = FontStyle.Bold;
        
        GUI.Label(new Rect(15, Screen.height - 38, 390, 25), _statusMessage, textStyle);
    }
} 