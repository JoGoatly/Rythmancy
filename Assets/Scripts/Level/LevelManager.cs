using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmWitchClone.Core;

namespace RhythmWitchClone.Levels
{
    /// <summary>
    /// Persistenter Manager (Singleton, DontDestroyOnLoad - gleiches Prinzip wie AudioManager) für
    /// die Kapitel/Level-Struktur des Spiels. Im Inspector trägst du eine verschachtelte Liste ein:
    /// Kapitel -> Level (Name + Szene). Verwaltet außerdem, welche Level bereits geschafft wurden -
    /// der Fortschritt selbst liegt in GameSession (pro Speicherstand getrennt, wird beim Auswählen
    /// eines Slots geladen und beim Speichern zurückgeschrieben).
    ///
    /// Freischalt-Regeln:
    /// - Kapitel 1 ist immer freigeschaltet. Jedes weitere Kapitel erst, wenn ALLE Level
    ///   des vorherigen Kapitels geschafft wurden.
    /// - Level 1 eines Kapitels ist spielbar, sobald das Kapitel selbst freigeschaltet ist.
    /// - Jedes weitere Level erst, wenn das direkt vorherige Level im selben Kapitel geschafft wurde.
    ///
    /// Platzierung: Auf ein GameObject in der allerersten Szene (z.B. neben deinem AudioManager-
    /// Bootstrap-Objekt), bleibt dank DontDestroyOnLoad über den ganzen Spielverlauf bestehen.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Serializable]
        public class Level
        {
            public string levelName;
            [Tooltip("Exakter Szenen-Dateiname (ohne .unity), der beim Anklicken geladen wird.")]
            public string sceneName;
        }

        [Serializable]
        public class Chapter
        {
            public string chapterName;
            public List<Level> levels = new List<Level>();
        }

        [Header("Kapitel (Reihenfolge = Spielreihenfolge)")]
        [SerializeField] private List<Chapter> chapters = new List<Chapter>();

        public IReadOnlyList<Chapter> Chapters => chapters;

        // Welches Level gerade aktiv gespielt wird, damit man es später (z.B. bei einem
        // Level-Ziel-Trigger) einfach als "geschafft" markieren kann, ohne Indizes mitschleppen zu müssen.
        public int CurrentChapterIndex { get; private set; } = -1;
        public int CurrentLevelIndex { get; private set; } = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool IsChapterUnlocked(int chapterIndex)
        {
            if (chapterIndex == 0) return true;
            if (chapterIndex < 0 || chapterIndex >= chapters.Count) return false;

            return IsChapterCompleted(chapterIndex - 1);
        }

        public bool IsChapterCompleted(int chapterIndex)
        {
            if (chapterIndex < 0 || chapterIndex >= chapters.Count) return false;

            Chapter chapter = chapters[chapterIndex];
            if (chapter.levels.Count == 0) return false;

            for (int i = 0; i < chapter.levels.Count; i++)
            {
                if (!IsLevelCompleted(chapterIndex, i)) return false;
            }
            return true;
        }

        public bool IsLevelUnlocked(int chapterIndex, int levelIndex)
        {
            if (!IsChapterUnlocked(chapterIndex)) return false;
            if (levelIndex == 0) return true;

            return IsLevelCompleted(chapterIndex, levelIndex - 1);
        }

        public bool IsLevelCompleted(int chapterIndex, int levelIndex)
        {
            return GameSession.IsLevelCompleted(GetLevelKey(chapterIndex, levelIndex));
        }

        /// <summary>
        /// Markiert ein Level als geschafft. Rufst du auf, sobald es ein Erfolgs-/Ziel-System
        /// im Level gibt (z.B. LevelManager.Instance.MarkLevelCompleted(chapterIndex, levelIndex)).
        /// Landet in GameSession (pro Speicherstand getrennt) - wird erst beim nächsten Speichern
        /// dauerhaft in die Save-Datei geschrieben.
        /// </summary>
        public void MarkLevelCompleted(int chapterIndex, int levelIndex)
        {
            GameSession.MarkLevelCompleted(GetLevelKey(chapterIndex, levelIndex));
        }

        /// <summary>
        /// Markiert das aktuell gestartete Level als geschafft (praktisch, wenn man die Indizes
        /// nicht selbst mitschleppen will - nutzt CurrentChapterIndex/CurrentLevelIndex).
        /// </summary>
        public void MarkCurrentLevelCompleted()
        {
            if (CurrentChapterIndex < 0 || CurrentLevelIndex < 0) return;
            MarkLevelCompleted(CurrentChapterIndex, CurrentLevelIndex);
        }

        private string GetLevelKey(int chapterIndex, int levelIndex) => $"level_completed_{chapterIndex}_{levelIndex}";

        /// <summary>
        /// Lädt die Szene des angegebenen Levels und merkt sich, welches Level gerade aktiv ist.
        /// </summary>
        public void LoadLevel(int chapterIndex, int levelIndex)
        {
            if (chapterIndex < 0 || chapterIndex >= chapters.Count) return;

            Chapter chapter = chapters[chapterIndex];
            if (levelIndex < 0 || levelIndex >= chapter.levels.Count) return;

            CurrentChapterIndex = chapterIndex;
            CurrentLevelIndex = levelIndex;

            SceneManager.LoadScene(chapter.levels[levelIndex].sceneName);
        }
    }
}