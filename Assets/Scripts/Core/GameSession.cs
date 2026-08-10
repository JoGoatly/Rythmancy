using System.Collections.Generic;
using RhythmWitchClone.Save;
using RhythmWitchClone.Levels;

namespace RhythmWitchClone.Core
{
    /// <summary>
    /// Statische Klasse, die einfache Daten über Szenenwechsel hinweg am Leben hält:
    /// welcher Speicherstand ausgewählt wurde, sowie Münzen + Upgrade-Stufen + Level-Fortschritt
    /// für GENAU diesen Speicherstand (werden beim Auswählen eines Slots aus SaveData geladen und
    /// beim Speichern wieder zurückgeschrieben - jeder Speicherstand hat also seinen eigenen
    /// Münz-/Upgrade-/Fortschritts-Stand, statt sich einen globalen Zustand zu teilen).
    /// Kein MonoBehaviour nötig - kein GameObject, keine DontDestroyOnLoad-Verwaltung erforderlich.
    /// </summary>
    public static class GameSession
    {
        public static int SelectedSaveSlot { get; private set; } = -1;
        public static int CoinCount { get; private set; }

        public static int BallCountUpgradeLevel { get; private set; }
        public static int ProjectileCountUpgradeLevel { get; private set; }
        public static int ProjectileDamageUpgradeLevel { get; private set; }
        public static int CoinMagnetUpgradeLevel { get; private set; }

        private static readonly HashSet<string> _completedLevelKeys = new HashSet<string>();

        public static void SelectSaveSlot(int slotIndex)
        {
            SelectedSaveSlot = slotIndex;
        }

        public static void AddCoins(int amount)
        {
            CoinCount += amount;
        }

        /// <summary>
        /// Zieht Münzen ab, falls genug vorhanden sind. Gibt true zurück bei Erfolg,
        /// false wenn nicht genug Münzen da waren (dann wird nichts abgezogen).
        /// </summary>
        public static bool SpendCoins(int amount)
        {
            if (CoinCount < amount) return false;

            CoinCount -= amount;
            return true;
        }

        /// <summary>
        /// Aktuelle Stufe eines Upgrades (per String-Schlüssel, siehe UpgradeController-Konstanten).
        /// </summary>
        public static int GetUpgradeLevel(string key)
        {
            if (key == UpgradeController.BallCountKey) return BallCountUpgradeLevel;
            if (key == UpgradeController.ProjectileCountKey) return ProjectileCountUpgradeLevel;
            if (key == UpgradeController.ProjectileDamageKey) return ProjectileDamageUpgradeLevel;
            if (key == UpgradeController.CoinMagnetKey) return CoinMagnetUpgradeLevel;
            return 0;
        }

        public static void IncreaseUpgradeLevel(string key)
        {
            if (key == UpgradeController.BallCountKey) BallCountUpgradeLevel++;
            else if (key == UpgradeController.ProjectileCountKey) ProjectileCountUpgradeLevel++;
            else if (key == UpgradeController.ProjectileDamageKey) ProjectileDamageUpgradeLevel++;
            else if (key == UpgradeController.CoinMagnetKey) CoinMagnetUpgradeLevel++;
        }

        public static bool IsLevelCompleted(string levelKey) => _completedLevelKeys.Contains(levelKey);

        public static void MarkLevelCompleted(string levelKey) => _completedLevelKeys.Add(levelKey);

        /// <summary>
        /// Lädt Münzen + Upgrade-Stufen + Level-Fortschritt aus den Save-Daten eines
        /// Speicherstands - aufrufen, sobald ein Slot ausgewählt wird (siehe SaveSlotSelectionController).
        /// </summary>
        public static void LoadFromSaveData(SaveData data)
        {
            CoinCount = data.coinCount;
            BallCountUpgradeLevel = data.ballCountUpgradeLevel;
            ProjectileCountUpgradeLevel = data.projectileCountUpgradeLevel;
            ProjectileDamageUpgradeLevel = data.projectileDamageUpgradeLevel;
            CoinMagnetUpgradeLevel = data.coinMagnetUpgradeLevel;

            _completedLevelKeys.Clear();
            foreach (string key in data.completedLevelKeys)
            {
                _completedLevelKeys.Add(key);
            }
        }

        /// <summary>
        /// Schreibt die aktuellen Münzen + Upgrade-Stufen + Level-Fortschritt in die übergebenen
        /// Save-Daten, zum anschließenden Speichern über SaveSystem.Save(...).
        /// </summary>
        public static void WriteToSaveData(SaveData data)
        {
            data.coinCount = CoinCount;
            data.ballCountUpgradeLevel = BallCountUpgradeLevel;
            data.projectileCountUpgradeLevel = ProjectileCountUpgradeLevel;
            data.projectileDamageUpgradeLevel = ProjectileDamageUpgradeLevel;
            data.coinMagnetUpgradeLevel = CoinMagnetUpgradeLevel;

            data.completedLevelKeys.Clear();
            data.completedLevelKeys.AddRange(_completedLevelKeys);
        }
    }
}