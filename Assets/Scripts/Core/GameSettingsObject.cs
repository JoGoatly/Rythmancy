using UnityEditor.ShaderGraph.Legacy;
using UnityEngine;

[CreateAssetMenu(fileName = "GameSettingsObject", menuName = "Scriptable Objects/GameSettingsObject")]
public class GameSettingsObject : ScriptableObject
{
    [Header("Audio")]
    
    public float MasterVolume = 1.0f;

    public float MusicVolume = 1.0f;
    public float SfxVolume = 1.0f;

    [Header("Graphics")]

    public Resolution res = new();
    public bool fullscreen = false;


}
