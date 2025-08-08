using TextBuddySDK;
using TextBuddySDK.Configuration;
using TextBuddySDK.Domain.ValueObjects;
using UnityEngine;
using System;
using TMPro; // Use TextMeshPro

/// <summary>
/// Manages the TextBuddy SDK initialization and provides simple,
/// public methods to be called from UI elements like buttons.
/// </summary>
public class TextBuddyManager : MonoBehaviour
{
    // A "singleton" instance. This makes it easy to access the manager from any other script.
    public static TextBuddyManager Instance { get; private set; }

    // This will hold our SDK client.
    public TextBuddyClient TextBuddy { get; private set; }

    [Header("UI References")]
    [Tooltip("Assign a TextMeshProUGUI element here to display status messages from the SDK.")]
    public TextMeshProUGUI statusText; // Assign this in the Unity Inspector

    [Header("Testing")]
    [Tooltip("The message to send when clicking the 'Send SMS' button.")]
    public string testSmsMessage = "Hello from my game, this is a test message!";

    // We need a place to store the token after the player successfully registers.
    private SMSToken _currentToken;
    private const string TokenPlayerPrefsKey = "TextBuddy_SmsToken";

    #region Unity Lifecycle Methods

    void Awake()
    {
        // Standard singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Makes sure our manager persists between scenes

        // --- This is the core SDK initialization ---

        // 1. Create the configuration object with your specific keys
        var config = new TextBuddyConfig(
            gameApiIdKey: "YOUR_GAME_API_KEY_HERE",
            apiBaseUrl: "https://52ae07c4f404.ngrok-free.app",
            textBuddyPhoneNumber: "+359123456789",
            enableDebugLogging: true, // Set to true for development
            useRealApi: true
        );

        // 2. Create the main SDK client instance
        TextBuddy = new TextBuddyClient(config);

        UpdateStatus("TextBuddy SDK Initialized!");

        // 3. Load any existing token from the previous session
        LoadToken();
    }

    void OnEnable()
    {
        // Unity's event for when the application is opened via a deep link.
        Application.deepLinkActivated += OnDeepLinkActivated;
    }

    void OnDisable()
    {
        // Clean up the event subscription when the object is destroyed.
        Application.deepLinkActivated -= OnDeepLinkActivated;
    }

    #endregion

    #region SDK Method Wrappers

    /// <summary>
    /// This method is called by Unity when the app is opened from a deep link.
    /// </summary>
    private void OnDeepLinkActivated(string url)
    {
        UpdateStatus($"Deep link activated: {url}. Processing...");
        if (TextBuddy == null)
        {
            UpdateStatus("ERROR: Cannot process deep link, SDK not initialized!");
            return;
        }

        TextBuddy.ProcessDeepLink(new Uri(url), (result) =>
        {
            if (result.IsSuccess)
            {
                _currentToken = result.Value;
                SaveToken(_currentToken); // Save the new token
                UpdateStatus($"SUCCESS: Registration complete! Token received and saved.");
            }
            else
            {
                UpdateStatus($"ERROR: Failed to process deep link: {result.Error.Message}");
            }
        });
    }

    /// <summary>
    /// Starts the registration process by opening the user's SMS app.
    /// </summary>
    public void StartRegistrationProcess()
    {
        if (TextBuddy != null)
        {
            UpdateStatus("Opening SMS app for registration...");
            TextBuddy.Register().ContinueWith(x=> Debug.Log("Done"));
        }
        else
        {
            UpdateStatus("ERROR: TextBuddy SDK is not initialized!");
        }
    }

    /// <summary>
    /// Sends an SMS using the currently stored token.
    /// </summary>
    public void SendSmsOnClick()
    {
        if (TextBuddy == null) { UpdateStatus("ERROR: SDK not initialized!"); return; }
        if (_currentToken == null) { UpdateStatus("ERROR: No valid token. Please register first."); return; }

        UpdateStatus($"Sending SMS: '{testSmsMessage}'...");
        TextBuddy.SendSms(_currentToken, testSmsMessage, (result) =>
        {
            if (result.IsSuccess)
            {
                UpdateStatus("SUCCESS: SendSms call was successful!");
            }
            else
            {
                UpdateStatus($"ERROR: SendSms failed: {result.Error.Message}");
            }
        });
    }

    /// <summary>
    /// Unregisters the currently stored token.
    /// </summary>
    public void UnregisterOnClick()
    {
        if (TextBuddy == null) { UpdateStatus("ERROR: SDK not initialized!"); return; }
        if (_currentToken == null) { UpdateStatus("ERROR: No valid token to unregister."); return; }

        UpdateStatus("Attempting to unregister token...");
        TextBuddy.Unregister(_currentToken, (result) =>
        {
            if (result.IsSuccess)
            {
                ClearToken(); // Clear the token locally and from PlayerPrefs
                UpdateStatus("SUCCESS: Unregister call was successful! Token is now invalid and has been cleared.");
            }
            else
            {
                UpdateStatus($"ERROR: Unregister failed: {result.Error.Message}");
            }
        });
    }

    /// <summary>
    /// Checks if the currently stored token is still valid on the backend.
    /// </summary>
    public void CheckTokenStatusOnClick()
    {
        if (TextBuddy == null) { UpdateStatus("ERROR: SDK not initialized!"); return; }
        if (_currentToken == null) { UpdateStatus("ERROR: No valid token to check."); return; }

        UpdateStatus("Checking token status...");
        TextBuddy.IsTokenRegistered(_currentToken, (result) =>
        {
            if (result.IsSuccess)
            {
                UpdateStatus($"SUCCESS: Token is currently {(result.Value ? "VALID" : "INVALID")}.");
            }
            else
            {
                UpdateStatus($"ERROR: Token status check failed: {result.Error.Message}");
            }
        });
    }

    #endregion

    #region Token Persistence

    /// <summary>
    /// Saves the SMSToken to PlayerPrefs as a JSON string.
    /// </summary>
    private void SaveToken(SMSToken token)
    {
        // IMPORTANT: The SMSToken class MUST have the [System.Serializable] attribute for this to work.
        string tokenJson = JsonUtility.ToJson(token);
        PlayerPrefs.SetString(TokenPlayerPrefsKey, tokenJson);
        PlayerPrefs.Save();
        Debug.Log($"[TextBuddyManager] Token saved to PlayerPrefs.");
    }

    /// <summary>
    /// Loads the SMSToken from PlayerPrefs.
    /// </summary>
    private void LoadToken()
    {
        if (PlayerPrefs.HasKey(TokenPlayerPrefsKey))
        {
            string tokenJson = PlayerPrefs.GetString(TokenPlayerPrefsKey);
            _currentToken = JsonUtility.FromJson<SMSToken>(tokenJson);

            if (_currentToken != null && !_currentToken.IsExpired)
            {
                UpdateStatus("Existing valid token loaded from previous session.");
            }
            else
            {
                UpdateStatus("Found an expired token. Please register again.");
                ClearToken();
            }
        }
        else
        {
            UpdateStatus("No saved token found. Please register.");
        }
    }

    /// <summary>
    /// Clears the token from the current session and PlayerPrefs.
    /// </summary>
    private void ClearToken()
    {
        _currentToken = null;
        PlayerPrefs.DeleteKey(TokenPlayerPrefsKey);
        PlayerPrefs.Save();
        Debug.Log($"[TextBuddyManager] Token cleared from PlayerPrefs.");
    }

    #endregion

    #region UI Helper

    /// <summary>
    /// Updates the status text UI element and logs the message to the console.
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[TextBuddyManager] {message}");
    }

    #endregion
}
