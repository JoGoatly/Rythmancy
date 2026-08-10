using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace RhythmWitchClone.UI.Settings
{
    /// <summary>
    /// Steuert die Kapitel-Navigation der Einstellungen.
    /// Die Kapitel werden im Inspector als Liste eingetragen (Name + zugehöriges Inhalts-Panel).
    /// Die Reihenfolge der Liste bestimmt die Anzeige-Reihenfolge der Buttons.
    ///
    /// Alle Kapitel sind von Anfang an anklickbar, auch wenn ihr Panel noch keinen Inhalt hat
    /// (z.B. Steuerung / Grafik). Nur "Audio" hat aktuell ein befülltes Panel.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [System.Serializable]
        public class SettingsChapter
        {
            public string chapterName;
            public GameObject panel; // Inhalts-Panel dieses Kapitels (z.B. AudioSettingsPanel-GameObject)
        }

        [Header("Kapitel (Reihenfolge = Anzeige-Reihenfolge)")]
        [SerializeField] private List<SettingsChapter> chapters = new List<SettingsChapter>();

        [Header("UI-Referenzen")]
        [SerializeField] private GameObject chapterListPanel;   // Übersicht mit allen Kapitel-Buttons
        [SerializeField] private Transform chapterButtonParent; // Container mit Layout-Group für die Buttons
        [SerializeField] private Button chapterButtonPrefab;    // Prefab für einen einzelnen Kapitel-Button
        [SerializeField] private TMP_Text headerLabel;           // Zeigt oben den aktuellen Kapitelnamen an
        [SerializeField] private Button backButton;              // Unten: geht 1 Schritt zurück

        [Header("Navigation")]
        [Tooltip("Wird ausgelöst, wenn 'Zurück' aus der Kapitelübersicht gedrückt wird (nicht aus einem Kapitel). " +
                 "Im Hauptmenü hier MainMenuController.ReturnToMainMenu() eintragen, im Pause-Menü die entsprechende Methode dort.")]
        [SerializeField] private UnityEvent onExitSettings;

        // null = wir sind in der Kapitelübersicht, sonst Index des offenen Kapitels
        private int? _currentChapterIndex;

        private void Awake()
        {
            BuildChapterButtons();
            backButton.onClick.AddListener(GoBack);
        }

        private void OnEnable()
        {
            ShowChapterList();
        }

        private void BuildChapterButtons()
        {
            foreach (Transform child in chapterButtonParent)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < chapters.Count; i++)
            {
                int chapterIndex = i; // lokale Kopie, wichtig für die Closure im Listener
                Button buttonInstance = Instantiate(chapterButtonPrefab, chapterButtonParent);
                TMP_Text label = buttonInstance.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = chapters[i].chapterName;

                buttonInstance.onClick.AddListener(() => OpenChapter(chapterIndex));
                buttonInstance.interactable = true; // ausdrücklich immer anklickbar, auch ohne Inhalt
            }
        }

        private void OpenChapter(int index)
        {
            _currentChapterIndex = index;

            chapterListPanel.SetActive(false);
            for (int i = 0; i < chapters.Count; i++)
            {
                if (chapters[i].panel != null)
                {
                    chapters[i].panel.SetActive(i == index);
                }
            }

            headerLabel.text = chapters[index].chapterName;
        }

        private void ShowChapterList()
        {
            _currentChapterIndex = null;
            chapterListPanel.SetActive(true);

            foreach (var chapter in chapters)
            {
                if (chapter.panel != null) chapter.panel.SetActive(false);
            }

            headerLabel.text = "Einstellungen";
        }

        /// <summary>
        /// "Zurück" geht immer genau einen Schritt zurück:
        /// aus einem Kapitel-Panel -> zur Kapitelübersicht.
        /// aus der Kapitelübersicht -> löst onExitSettings aus (Hauptmenü ODER Pause-Menü, je nach Szene).
        /// </summary>
        private void GoBack()
        {
            if (_currentChapterIndex.HasValue)
            {
                ShowChapterList();
            }
            else
            {
                onExitSettings?.Invoke();
            }
        }
    }
}