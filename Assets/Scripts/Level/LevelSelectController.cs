using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace RhythmWitchClone.Levels
{
    /// <summary>
    /// UI-Navigation für die Level-Auswahl: Kapitel-Übersicht -> Level-Übersicht eines Kapitels.
    /// Exakt gleiches Prinzip wie SettingsController: "Zurück" geht immer genau einen Schritt zurück,
    /// aus der Kapitelübersicht heraus löst es ein UnityEvent aus (verlässt die Level-Auswahl komplett -
    /// z.B. um das "E"-Interaktions-UI von SafeZoneController wieder zu zeigen).
    ///
    /// Kapitel/Level werden nur anklickbar angezeigt, wenn sie laut LevelManager freigeschaltet sind.
    ///
    /// Setup: Lebt typischerweise als Panel in der Safe-Zone-Szene, wird von SafeZoneController
    /// per Taste "E" ein-/ausgeblendet.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        [Header("Kapitel-Übersicht")]
        [SerializeField] private GameObject chapterListPanel;
        [SerializeField] private Transform chapterButtonParent;
        [SerializeField] private Button chapterButtonPrefab;

        [Header("Level-Übersicht")]
        [SerializeField] private GameObject levelListPanel;
        [SerializeField] private Transform levelButtonParent;
        [SerializeField] private Button levelButtonPrefab;

        [Header("UI-Referenzen")]
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private Button backButton;

        [Header("Navigation")]
        [Tooltip("Wird ausgelöst, wenn 'Zurück' aus der Kapitelübersicht gedrückt wird - verlässt die Level-Auswahl komplett.")]
        [SerializeField] private UnityEvent onExitLevelSelect;

        // null = Kapitelübersicht wird angezeigt, sonst Index des offenen Kapitels
        private int? _currentChapterIndex;

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
        }

        private void OnEnable()
        {
            ShowChapterList();
        }

        private void ShowChapterList()
        {
            _currentChapterIndex = null;
            chapterListPanel.SetActive(true);
            levelListPanel.SetActive(false);
            if (headerLabel != null) headerLabel.text = "Kapitel";

            BuildChapterButtons();
        }

        private void BuildChapterButtons()
        {
            foreach (Transform child in chapterButtonParent)
            {
                Destroy(child.gameObject);
            }

            if (LevelManager.Instance == null) return;

            var chapters = LevelManager.Instance.Chapters;
            for (int i = 0; i < chapters.Count; i++)
            {
                int chapterIndex = i; // lokale Kopie, wichtig für die Closure
                bool unlocked = LevelManager.Instance.IsChapterUnlocked(chapterIndex);

                Button buttonInstance = Instantiate(chapterButtonPrefab, chapterButtonParent);
                TMP_Text label = buttonInstance.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = unlocked ? chapters[i].chapterName : $"{chapters[i].chapterName} (gesperrt)";
                }

                buttonInstance.interactable = unlocked;
                if (unlocked)
                {
                    buttonInstance.onClick.AddListener(() => OpenChapter(chapterIndex));
                }
            }

            ForceLayoutRebuild(chapterButtonParent);
        }

        private void OpenChapter(int chapterIndex)
        {
            _currentChapterIndex = chapterIndex;

            chapterListPanel.SetActive(false);
            levelListPanel.SetActive(true);
            if (headerLabel != null) headerLabel.text = LevelManager.Instance.Chapters[chapterIndex].chapterName;

            BuildLevelButtons(chapterIndex);
        }

        private void BuildLevelButtons(int chapterIndex)
        {
            foreach (Transform child in levelButtonParent)
            {
                Destroy(child.gameObject);
            }

            var levels = LevelManager.Instance.Chapters[chapterIndex].levels;
            for (int i = 0; i < levels.Count; i++)
            {
                int levelIndex = i; // lokale Kopie, wichtig für die Closure
                bool unlocked = LevelManager.Instance.IsLevelUnlocked(chapterIndex, levelIndex);
                bool completed = LevelManager.Instance.IsLevelCompleted(chapterIndex, levelIndex);

                Button buttonInstance = Instantiate(levelButtonPrefab, levelButtonParent);
                TMP_Text label = buttonInstance.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    string suffix = completed ? " (geschafft)" : unlocked ? "" : " (gesperrt)";
                    label.text = levels[i].levelName + suffix;
                }

                buttonInstance.interactable = unlocked;
                if (unlocked)
                {
                    buttonInstance.onClick.AddListener(() => LevelManager.Instance.LoadLevel(chapterIndex, levelIndex));
                }
            }

            ForceLayoutRebuild(levelButtonParent);
        }

        /// <summary>
        /// Zwingt die Vertical Layout Group sofort zur Neuberechnung, statt auf den nächsten
        /// automatischen Layout-Durchlauf zu warten. Ohne das kann die sichtbare Position eines
        /// zur Laufzeit erzeugten Buttons kurzzeitig nicht mit seiner tatsächlichen (für Klicks
        /// genutzten) RectTransform-Größe übereinstimmen - fühlt sich dann an, als wäre nur
        /// ein Teil des Buttons anklickbar.
        /// </summary>
        private void ForceLayoutRebuild(Transform target)
        {
            RectTransform rectTransform = target as RectTransform;
            if (rectTransform == null) return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        /// <summary>
        /// Aus einem Kapitel -> zurück zur Kapitelübersicht.
        /// Aus der Kapitelübersicht -> löst onExitLevelSelect aus.
        /// </summary>
        private void GoBack()
        {
            if (_currentChapterIndex.HasValue)
            {
                ShowChapterList();
            }
            else
            {
                onExitLevelSelect?.Invoke();
            }
        }
    }
}