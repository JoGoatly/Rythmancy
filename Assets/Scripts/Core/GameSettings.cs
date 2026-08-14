using UnityEngine;

public class GameSettings : MonoBehaviour
{
    static GameSettings instance;

    GameSettingsObject gameSettings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitiateGameSettings()
    {
        GameObject obj = new GameObject("GameManager");
        instance = obj.AddComponent<GameSettings>();

        DontDestroyOnLoad(obj);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;
        LoadSettings();
    }

    // Static references
    static public void SaveInstanceSettings()
    {
        instance.SaveSettings();
    }
    static public void LoadInstanceSettings()
    {
        instance.LoadSettings();
    }

    // Instance save/load
    public void SaveSettings()
    {
        string settings = JsonUtility.ToJson(gameSettings);
        PlayerPrefs.SetString("Settings", settings);
    }

    public void LoadSettings()
    {
        if (!PlayerPrefs.HasKey("Settings"))
            return;
        gameSettings = JsonUtility.FromJson<GameSettingsObject> (PlayerPrefs.GetString("Settings"));
    }
}
