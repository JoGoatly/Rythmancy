using UnityEngine;
using UnityEngine.UI;
using RhythmWitchClone.Audio;

namespace RhythmWitchClone.UI.Settings
{
    /// <summary>
    /// Inhalt des "Audio"-Kapitels: je ein Slider für Musik- und SFX-Lautstärke.
    /// Beide Slider sollten im Inspector auf einen Wertebereich von 0 bis 1 eingestellt sein.
    /// </summary>
    public class AudioSettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        private void OnEnable()
        {
            if (AudioManager.Instance == null) return;

            // Listener kurz entfernen, damit das Setzen des Anfangswerts kein Event auslöst.
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);

            musicSlider.value = AudioManager.Instance.GetMusicVolume();
            sfxSlider.value = AudioManager.Instance.GetSfxVolume();

            musicSlider.onValueChanged.AddListener(OnMusicChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }

        private void OnMusicChanged(float value)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }

        private void OnSfxChanged(float value)
        {
            AudioManager.Instance.SetSfxVolume(value);
        }
    }
}
