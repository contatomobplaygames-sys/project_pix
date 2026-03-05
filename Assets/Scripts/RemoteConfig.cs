using System;
using System.Threading.Tasks;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using TMPro;
using UnityEditor;
using UnityEngine;

[Serializable]
public class ConfigValue
{
    public string name;
    public float version;
    public int level;
}
public class RemoteConfig : MonoBehaviour
{
    public TextMeshProUGUI title, version;
    public ConfigValue configValue;
    void Awake()
    {
        FetchDataAsync();
    }
    public Task FetchDataAsync()
    {
        Debug.Log("Fetching data...");
        Task fetchTask = FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero);
        return fetchTask.ContinueWithOnMainThread(FetchComplete);
    }
    private void FetchComplete(Task fetchTask)
    {
        if (!fetchTask.IsCompleted)
        {
            Debug.LogError("Retrieval hasn't finished.");
            return;
        }

        var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        var info = remoteConfig.Info;
        if (info.LastFetchStatus != LastFetchStatus.Success)
        {
            Debug.LogError($"{nameof(FetchComplete)} was unsuccessful\n{nameof(info.LastFetchStatus)}: {info.LastFetchStatus}");
            return;
        }

        // Fetch successful. Parameter values must be activated to use.
        remoteConfig.ActivateAsync()
          .ContinueWithOnMainThread(
            task =>
            {
                Debug.Log($"Remote data loaded and ready for use. Last fetch time {info.FetchTime}.");
            });
        string data = remoteConfig.GetValue("All_Data").StringValue;
        configValue = JsonUtility.FromJson<ConfigValue>(data);
        title.text = configValue.name;
        Debug.Log(Application.version);
        Debug.Log(configValue.version);
        if (configValue.version.ToString() == Application.version)
        {
            version.text = "Latest Version";
        }
        else
        {
            version.text = "Update Available";
        }
        // foreach (var item in remoteConfig.AllValues)
        // {
        //     Debug.Log("Key" + item.Key);
        //     Debug.Log("Value" + item.Value.StringValue);
        // }
    }
}
