using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Web;

public class DeepLinkManager : MonoBehaviour
{
    public static DeepLinkManager Instance { get; private set; }

    [Tooltip("Custom link name to match the deep link URL prefix.")]
    public string linkName = "rewardslink";  // Example: "testlink" in unitydl://testlink?scene=Main

    [Tooltip("List of valid scene names that can be loaded through deep links.")]
    public List<string> validScenes = new List<string>();

    [Tooltip("Additional parameters extracted from the deep link URL.")]
    public Dictionary<string, string> parameters = new Dictionary<string, string>();

    public string DeeplinkURL { get; private set; } = "[none]";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                OnDeepLinkActivated(Application.absoluteURL);
            }
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called when a deep link is activated.
    /// Processes the URL, loads the specified scene if valid, and extracts additional parameters.
    /// </summary>
    /// <param name="url">The URL received from the deep link.</param>
    private void OnDeepLinkActivated(string url)
    {
        DeeplinkURL = url;  // Store the deep link URL
        parameters.Clear();  // Clear any previously stored parameters

        if (url.Contains(linkName))  // Check if the URL contains the expected link name
        {
            string sceneName = GetSceneNameFromUrl(url);  // Extract the scene name from the URL
            if (IsValidScene(sceneName))  // Check if the scene name is valid
            {
                SceneManager.LoadScene(sceneName);  // Load the scene if valid
            }
            else
            {
                Debug.LogWarning($"Invalid scene name in deep link: {sceneName}");  // Log a warning if the scene name is invalid
            }
            ExtractParametersFromUrl(url);  // Extract additional parameters from the URL
        }
        else
        {
            Debug.LogWarning($"URL does not contain the expected link name: {linkName}");  // Log a warning if the URL does not contain the expected link name
        }
    }

    /// <summary>
    /// Parses the URL to extract the scene name.
    /// </summary>
    /// <param name="url">The URL received from the deep link.</param>
    /// <returns>The scene name extracted from the URL's query parameters.</returns>
    /// <example>
    /// Example URL: unitydl://testlink?scene=Main
    /// The method will return "Main".
    /// </example>
    private string GetSceneNameFromUrl(string url)
    {
        var uri = new System.Uri(url);  // Create a new Uri object from the URL
        var query = HttpUtility.ParseQueryString(uri.Query);  // Parse the query string part of the URL
        return query.Get("scene");  // Return the value of the "scene" parameter
    }

    /// <summary>
    /// Extracts additional parameters from the URL and stores them in the parameters dictionary.
    /// </summary>
    /// <param name="url">The URL received from the deep link.</param>
    /// <example>
    /// Example URL: unitydl://testlink?scene=Main&user=123&token=abc
    /// The method will store the following in the parameters dictionary:
    /// { "user": "123", "token": "abc" }
    /// </example>
    private void ExtractParametersFromUrl(string url)
    {
        var uri = new System.Uri(url);  // Create a new Uri object from the URL
        var query = HttpUtility.ParseQueryString(uri.Query);  // Parse the query string part of the URL
        foreach (var key in query.AllKeys)  // Iterate over all keys in the query string
        {
            if (key != "scene")  // Ignore the "scene" parameter as it is handled separately
            {
                parameters[key] = query.Get(key);  // Add the parameter to the dictionary
            }
        }
    }

    /// <summary>
    /// Checks if the given scene name is in the list of valid scenes.
    /// </summary>
    /// <param name="sceneName">The name of the scene to check.</param>
    /// <returns>True if the scene name is valid; otherwise, false.</returns>
    private bool IsValidScene(string sceneName)
    {
        return validScenes.Contains(sceneName);
    }
}