using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;
using System.Text;

public class InputDebugger : NetworkBehaviour
{
    [SerializeField] private bool _logActiveDevices = true;
    [SerializeField] private bool _logInputActions = true;
    
    // Cache for action maps in use
    private List<string> _actionMapsInUse = new List<string>();
    
    // Start is called before the first frame update
    void Start()
    {
        LogDeviceInfo();
        
        // Print debug info every 5 seconds
        InvokeRepeating("LogPeriodicDebugInfo", 5f, 5f);
        
        // Subscribe to all device added/removed events
        InputSystem.onDeviceChange += OnDeviceChange;
    }
    
    void OnDestroy()
    {
        // Unsubscribe when destroyed
        InputSystem.onDeviceChange -= OnDeviceChange;
    }
    
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        Debug.Log($"[InputDebugger] Device change: {device.name} - {change}");
        LogDeviceInfo();
    }
    
    private void LogDeviceInfo()
    {
        if (!_logActiveDevices) return;
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== INPUT SYSTEM DEVICES ===");
        
        foreach (var device in InputSystem.devices)
        {
            sb.AppendLine($"Device: {device.name}, Type: {device.GetType().Name}, Layout: {device.layout}");
            sb.AppendLine($"  - Added: {device.added}, Path: {device.path}");
            sb.AppendLine($"  - Description: {device.description}");
            sb.AppendLine($"  - Enabled: {device.enabled}, Native: {device.native}");
        }
        
        Debug.Log(sb.ToString());
    }
    
    private void LogPeriodicDebugInfo()
    {
        if (!_logInputActions) return;
        
        // Look for active PlayerInput components
        LogPlayerInputComponents();
    }
    
    private void LogPlayerInputComponents()
    {
        var playerInputs = FindObjectsOfType<PlayerInput>();
        if (playerInputs.Length == 0)
        {
            Debug.Log("[InputDebugger] No PlayerInput components found.");
            return;
        }
        
        _actionMapsInUse.Clear();
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== PLAYER INPUT COMPONENTS ===");
        
        foreach (var pi in playerInputs)
        {
            sb.AppendLine($"PlayerInput on {pi.gameObject.name}:");
            sb.AppendLine($"  - Enabled: {pi.enabled}");
            sb.AppendLine($"  - Default Action Map: {pi.defaultActionMap}");
            sb.AppendLine($"  - Current Action Map: {pi.currentActionMap?.name ?? "NULL"}");
            
            if (pi.actions != null)
            {
                sb.AppendLine($"  - Actions Asset Name: {pi.actions.name}");
                
                sb.AppendLine("  - Available Action Maps:");
                foreach (var map in pi.actions.actionMaps)
                {
                    sb.AppendLine($"    * {map.name} (Enabled: {map.enabled})");
                    
                    // Add to list of action maps in use
                    if (map.enabled && !_actionMapsInUse.Contains(map.name))
                    {
                        _actionMapsInUse.Add(map.name);
                    }
                }
                
                // Only log actions for the current action map
                if (pi.currentActionMap != null)
                {
                    sb.AppendLine($"  - Actions in current map ({pi.currentActionMap.name}):");
                    foreach (var action in pi.currentActionMap.actions)
                    {
                        sb.AppendLine($"    * {action.name} (Enabled: {action.enabled})");
                        sb.AppendLine($"      Bindings:");
                        
                        foreach (var binding in action.bindings)
                        {
                            sb.AppendLine($"      - {binding.path} (Group: {binding.groups})");
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine("  - No actions asset assigned!");
            }
            
            // Check control scheme
            sb.AppendLine($"  - Control Scheme: {pi.currentControlScheme ?? "NONE"}");
            
            // Check PlayerInput's assigned devices
            sb.AppendLine("  - Devices:");
            if (pi.devices != null && pi.devices.Count > 0)
            {
                foreach (var device in pi.devices)
                {
                    sb.AppendLine($"    * {device.name} ({device.GetType().Name})");
                }
            }
            else
            {
                sb.AppendLine("    * No explicitly assigned devices");
            }
        }
        
        // Log action map conflicts
        if (_actionMapsInUse.Count > 0)
        {
            sb.AppendLine("\n=== ACTION MAPS IN USE ===");
            foreach (var map in _actionMapsInUse)
            {
                sb.AppendLine($"  * {map}");
            }
        }
        
        Debug.Log(sb.ToString());
    }
    
    // Add this to any GameObject in the scene to display real-time input debug info
    public void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 300, 120), "Input Debug");
        
        GUILayout.BeginArea(new Rect(20, 40, 280, 80));
        
        // Display currently active action maps
        GUILayout.Label("Action Maps In Use:");
        foreach (var map in _actionMapsInUse)
        {
            GUILayout.Label($"• {map}");
        }
        
        // Add button to force re-scan devices
        if (GUILayout.Button("Refresh Input Devices"))
        {
            InputSystem.Update();
            LogDeviceInfo();
        }
        
        GUILayout.EndArea();
    }
} 