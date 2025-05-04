using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Add this component to your NetworkManager to automatically set up client input handlers
/// when client players connect. This solves the "Cannot find matching control scheme" error.
/// </summary>
public class ClientInputFixer : MonoBehaviour
{
    [Tooltip("Show on-screen instructions for client input")]
    [SerializeField] private bool _showInstructions = true;
    
    [Tooltip("Add emergency fix utility")]
    [SerializeField] private bool _addEmergencyFixUtility = true;
    
    private bool _clientConnected = false;
    private float _messageTimer = 0f;
    private const float MESSAGE_DURATION = 5f;
    
    private void Start()
    {
        // Get NetworkManager if available
        var networkManager = GetComponent<NetworkManager>();
        if (networkManager != null)
        {
            // Subscribe to events
            networkManager.OnClientConnectedCallback += OnClientConnected;
            
            Debug.Log("ClientInputFixer initialized on NetworkManager");
        }
        
        // Add emergency fix utility
        if (_addEmergencyFixUtility)
        {
            if (!FindObjectOfType<ClientFixUtility>())
            {
                gameObject.AddComponent<ClientFixUtility>();
                Debug.Log("Added emergency ClientFixUtility (press F12 to activate)");
            }
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        
        // Only do this for non-host clients
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            _clientConnected = true;
            _messageTimer = MESSAGE_DURATION;
            
            // Set up a delay to fix client players once they're spawned
            Invoke("FindAndFixClientPlayers", 2f);
        }
    }
    
    private void FindAndFixClientPlayers()
    {
        // Find all player objects in the scene
        var players = FindObjectsOfType<PlayerMovement>();
        int fixCount = 0;
        
        foreach (var player in players)
        {
            // Only fix client-owned players that aren't the host
            if (player.IsOwner && !player.IsHost)
            {
                Debug.Log($"Auto-fixing client player: {player.gameObject.name}");
                
                // Remove any existing ClientInputHandler
                var existingHandler = player.gameObject.GetComponent<ClientInputHandler>();
                if (existingHandler != null)
                {
                    Destroy(existingHandler);
                }
                
                // Add a fresh InputHandler
                var inputHandler = player.gameObject.AddComponent<ClientInputHandler>();
                if (inputHandler != null)
                {
                    Debug.Log("Auto-added ClientInputHandler to client player");
                    fixCount++;
                }
            }
        }
        
        Debug.Log($"Auto-fixed {fixCount} client player(s)");
        
        if (fixCount > 0)
        {
            _messageTimer = MESSAGE_DURATION;
        }
    }
    
    private void Update()
    {
        // Count down message timer
        if (_messageTimer > 0)
        {
            _messageTimer -= Time.deltaTime;
        }
    }
    
    private void OnGUI()
    {
        if (!_showInstructions || !_clientConnected || _messageTimer <= 0) return;
        
        // Draw instructions at the bottom center of the screen
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.alignment = TextAnchor.MiddleCenter;
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        boxStyle.normal.background = MakeBackgroundTexture(380, 50, new Color(0, 0, 0, 0.7f));
        
        Rect boxRect = new Rect(
            Screen.width/2 - 190,
            Screen.height - 60, 
            380, 
            50);
        
        GUI.Box(boxRect, "", boxStyle);
        
        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.normal.textColor = Color.white;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.fontSize = 14;
        
        GUI.Label(boxRect, "CLIENT CONTROLS:\nUse Arrow Keys to Move, Space to Fire", textStyle);
    }
    
    // Helper to create a texture for GUI background
    private Texture2D MakeBackgroundTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        
        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
} 