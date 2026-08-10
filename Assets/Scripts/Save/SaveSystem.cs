using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RhythmWitchClone.Save
{
    [Serializable]
    public class SaveData
    {
        public bool exists;
        public int level = 1;
        public float playtimeSeconds;
        public string lastPlayedIso;

        [Header("Spieler-Position")]
        public float playerPosX;
        public float playerPosY;
        public float playerPosZ;
        public bool playerFacingRight; // true = Spieler stand zuletzt nach rechts gedreht (Sprite gespiegelt)

        [Header("Münzen + Upgrades (pro Speicherstand getrennt)")]
        public int coinCount;
        public int ballCountUpgradeLevel;
        public int projectileCountUpgradeLevel;
        public int projectileDamageUpgradeLevel;
        public int coinMagnetUpgradeLevel;

        [Header("Level-Fortschritt (pro Speicherstand getrennt)")]
        [Tooltip("Schlüssel im Format 'KapitelIndex_LevelIndex', ein Eintrag pro geschafftem Level.")]
        public List<string> completedLevelKeys = new List<string>();

        // TODO: Hier weitere eigene Spielstand-Felder ergänzen
        // (Fortschritt, Highscores, freigeschaltete Songs, Kapitel-Status, etc.)
    }

    /// <summary>
    /// Einfaches, dateibasiertes Speichersystem mit 3 festen Slots.
    /// Speichert JSON-Dateien in Application.persistentDataPath.
    /// </summary>
    public static class SaveSystem
    {
        private const int SlotCount = 3;

        private static string GetPath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

        public static SaveData Load(int slot)
        {
            string path = GetPath(slot);
            if (!File.Exists(path))
            {
                return new SaveData { exists = false };
            }

            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            data.exists = true;
            return data;
        }

        public static void Save(int slot, SaveData data)
        {
            data.exists = true;
            data.lastPlayedIso = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetPath(slot), json);
        }

        public static void DeleteSlot(int slot)
        {
            string path = GetPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        public static int GetSlotCount() => SlotCount;
    }
}