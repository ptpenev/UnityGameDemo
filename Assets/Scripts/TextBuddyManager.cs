using System;
using System.Collections.Generic;
using TextBuddySDK;
using TextBuddySDK.Configuration;
using TextBuddySDK.Domain.ValueObjects;
using TMPro; // Use TextMeshPro
using UnityEngine;
using UnityEngine.UI;

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

    [Header("TextBuddy Configuration")]
    [Tooltip("Your unique game API key provided by TextBuddy.")]
    [SerializeField] private string gameApiIdKey = "your-game-api-key-here";

    [Tooltip("The base URL for the TextBuddy API (include https://).")]
    [SerializeField] private string apiBaseUrl = "https://7712d594b052.ngrok-free.app";

    [Tooltip("The phone number that TextBuddy uses to send SMS messages.")]
    [SerializeField] private string textBuddyPhoneNumber = "+12065551234";

    [Tooltip("Enable debug logging for development purposes.")]
    [SerializeField] private bool enableDebugLogging = true;

    [Tooltip("Set to true to use the real API, false to use mock/test mode.")]
    [SerializeField] private bool useRealApi = true;

    [Header("UI References")]
    [Tooltip("Assign a TextMeshProUGUI element here to display status messages from the SDK.")]
    public TextMeshProUGUI statusText; // Assign this in the Unity Inspector

    [Tooltip("Assign the ScrollRect component to enable auto-scrolling to the bottom.")]
    public ScrollRect logScrollRect; // Optional: for auto-scrolling functionality

    [Header("Logging Settings")]
    [Tooltip("Maximum number of log entries to keep in the scrollable log.")]
    [SerializeField] private int maxLogEntries = 100;

    [Tooltip("Enable timestamps for each log entry.")]
    [SerializeField] private bool showTimestamps = true;

    [Tooltip("Auto-scroll to the bottom when new logs are added.")]
    [SerializeField] private bool autoScrollToBottom = true;
    [Tooltip("The message to send when clicking the 'Send SMS' button.")]
    public string testSmsMessage = "Hello from my game, this is a test message!";

    // We need a place to store the token after the player successfully registers.
    private SMSToken _currentToken;
    private const string TokenPlayerPrefsKey = "TextBuddy_SmsToken";
    private List<string> _logEntries = new List<string>();

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

        // Validate configuration before initializing
        if (!ValidateConfiguration())
        {
            UpdateStatus("ERROR: Invalid TextBuddy configuration. Please check the settings in the Inspector.");
            return;
        }

        // 1. Create the configuration object using the serialized values
        var config = new TextBuddyConfig(
            gameApiIdKey: gameApiIdKey,
            apiBaseUrl: apiBaseUrl,
            textBuddyPhoneNumber: textBuddyPhoneNumber,
            enableDebugLogging: enableDebugLogging,
            useRealApi: useRealApi
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

    #region Configuration Validation

    /// <summary>
    /// Validates the TextBuddy configuration to ensure all required fields are set properly.
    /// </summary>
    private bool ValidateConfiguration()
    {
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(gameApiIdKey))
        {
            Debug.LogError("[TextBuddyManager] Game API Key is not set or still has the default value. Please set it in the Inspector.");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            Debug.LogError("[TextBuddyManager] API Base URL is not set. Please set it in the Inspector.");
            isValid = false;
        }
        else if (!apiBaseUrl.StartsWith("http://") && !apiBaseUrl.StartsWith("https://"))
        {
            Debug.LogError("[TextBuddyManager] API Base URL must start with 'http://' or 'https://'. Current value: " + apiBaseUrl);
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(textBuddyPhoneNumber))
        {
            Debug.LogError("[TextBuddyManager] TextBuddy Phone Number is not set. Please set it in the Inspector.");
            isValid = false;
        }
        else if (!textBuddyPhoneNumber.StartsWith("+"))
        {
            Debug.LogError("[TextBuddyManager] TextBuddy Phone Number should start with '+' and include country code. Current value: " + textBuddyPhoneNumber);
            isValid = false;
        }

        return isValid;
    }

    #endregion

    #region Public Configuration Access (Runtime Only)

    /// <summary>
    /// Updates the API base URL at runtime. Requires SDK reinitialization to take effect.
    /// </summary>
    public void UpdateApiBaseUrl(string newUrl)
    {
        if (!string.IsNullOrWhiteSpace(newUrl))
        {
            apiBaseUrl = newUrl;
            UpdateStatus($"API Base URL updated to: {newUrl}. Restart required for changes to take effect.");
        }
    }

    /// <summary>
    /// Gets the current configuration values for debugging purposes.
    /// </summary>
    public string GetCurrentConfiguration()
    {
        return $"Game API Key: {(string.IsNullOrWhiteSpace(gameApiIdKey) ? "Not Set" : "Set")}\n" +
               $"API Base URL: {apiBaseUrl}\n" +
               $"Phone Number: {textBuddyPhoneNumber}\n" +
               $"Debug Logging: {enableDebugLogging}\n" +
               $"Use Real API: {useRealApi}";
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
            TextBuddy.Register().ContinueWith(x => Debug.Log("Registration process initiated."));
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
                UpdateStatus($"SUCCESS: {result.Value} - Token is currently {(result.Value ? "VALID" : "INVALID")}.");
            }
            else
            {
                UpdateStatus($"ERROR: Token status check failed: {result.Error.Message}");
            }
        });
    }

    /// <summary>
    /// Displays the current configuration in the status text for debugging.
    /// </summary>
    public void ShowCurrentConfiguration()
    {
        UpdateStatus($"Current Configuration:\n{GetCurrentConfiguration()}");
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
        AddLogEntry(message);
        Debug.Log($"[TextBuddyManager] {message}");
    }

    /// <summary>
    /// Adds a new log entry to the scrollable log display.
    /// </summary>
    private void AddLogEntry(string message)
    {
        if (statusText == null) return;

        // Create timestamp if enabled
        string timestamp = showTimestamps ? $"[{DateTime.Now:HH:mm:ss}] " : "";
        string logEntry = $"{timestamp}{message}";

        // Add to our log entries list
        _logEntries.Add(logEntry);

        // Remove old entries if we exceed the maximum
        while (_logEntries.Count > maxLogEntries)
        {
            _logEntries.RemoveAt(0);
        }

        // Update the display
        statusText.text = string.Join("\n", _logEntries);

        // Auto-scroll to bottom if enabled
        if (autoScrollToBottom && logScrollRect != null)
        {
            StartCoroutine(ScrollToBottomNextFrame());
        }
    }

    /// <summary>
    /// Scrolls to the bottom of the log on the next frame (required for proper scrolling).
    /// </summary>
    private System.Collections.IEnumerator ScrollToBottomNextFrame()
    {
        yield return new WaitForEndOfFrame();
        if (logScrollRect != null)
        {
            logScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    /// <summary>
    /// Clears all log entries from the display.
    /// </summary>
    public void ClearLogs()
    {
        _logEntries.Clear();
        if (statusText != null)
        {
            statusText.text = "";
        }
        AddLogEntry("Logs cleared.");
    }

    /// <summary>
    /// Manually scrolls to the bottom of the log.
    /// </summary>
    public void ScrollToBottom()
    {
        if (logScrollRect != null)
        {
            logScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    /// <summary>
    /// Manually scrolls to the top of the log.
    /// </summary>
    public void ScrollToTop()
    {
        if (logScrollRect != null)
        {
            logScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    #endregion
}