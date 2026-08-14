using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using RhythmWitchClone.Core;
using RhythmWitchClone.Gameplay;

namespace RhythmWitchClone.Levels
{
    /// <summary>
    /// Upgrade-Station in der Safe Zone - gleiches Grundprinzip wie SafeZoneController
    /// (Trigger-Bereich, "E"-Tastenhinweis, öffnet ein Panel). Bietet 4 Upgrades an:
    /// Tasten-Anzahl, Projektil-Anzahl, Projektil-Schaden, Münzmagnet-Radius.
    ///
    /// Die Upgrade-Stufen werden über GameSession pro Speicherstand getrennt gehalten (siehe die
    /// oben in dieser Klasse) -
    /// dadurch wendet BeatLaneController/PlayerController sie beim nächsten Levelstart
    /// automatisch selbst an, ohne dass dieses Script irgendeine Referenz auf laufende
    /// Level-Objekte braucht.
    ///
    /// Bezahlt wird mit GameSession.CoinCount (dieselben Münzen, die in den Levels gesammelt werden).
    /// Die Kosten steigen mit jeder gekauften Stufe.
    ///
    /// Setup:
    /// - Kommt auf ein GameObject mit einem Collider (Is Trigger = an) in der Safe Zone.
    /// - Der Spieler braucht den Tag "Player" (wie beim Level-Auswahl-Trigger).
    /// </summary>
    public class UpgradeController : MonoBehaviour
    {
        // Zentrale Schlüssel für die Upgrade-Stufen (werden in GameSession gehalten, pro
        // Speicherstand getrennt) - public, damit BeatLaneController/PlayerController sie beim Anwenden der Upgrades
        // referenzieren können, ohne den Schlüssel selbst nochmal als String zu tippen.
        public const string BallCountKey = "Upgrade_BallCount";
        public const string ProjectileCountKey = "Upgrade_ProjectileCount";
        public const string ProjectileDamageKey = "Upgrade_ProjectileDamage";
        public const string CoinMagnetKey = "Upgrade_CoinMagnet";

        [Header("Interaktion")]
        [Tooltip("UI-Element mit dem 'E'-Tastenhinweis, das erscheint, wenn der Spieler in Reichweite ist.")]
        [SerializeField] private GameObject keyPromptUI;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private Button closeButton;

        [Tooltip("Für die sofortige Aktualisierung der Münzanzeige nach einem Kauf.")]
        [SerializeField] private PlayerController player;

        [Tooltip("Wird ausgeblendet/pausiert, solange das Upgrade-Panel offen ist (wie bei der Level-Auswahl).")]
        [SerializeField] private BeatLaneController beatLane;

        [Header("Kosten")]
        [SerializeField] private int baseCost = 10;
        [SerializeField] private int costIncreasePerLevel = 5;

        [Header("Tasten-Anzahl")]
        [SerializeField] private TMP_Text ballCountLabel;
        [SerializeField] private Button ballCountBuyButton;
        [SerializeField] private int ballCountMaxLevel = 6;

        [Header("Projektil-Anzahl")]
        [SerializeField] private TMP_Text projectileCountLabel;
        [SerializeField] private Button projectileCountBuyButton;
        [SerializeField] private int projectileCountMaxLevel = 4;

        [Header("Projektil-Schaden")]
        [SerializeField] private TMP_Text projectileDamageLabel;
        [SerializeField] private Button projectileDamageBuyButton;
        [SerializeField] private int projectileDamageMaxLevel = 10;

        [Header("Münzmagnet-Radius")]
        [SerializeField] private TMP_Text coinMagnetLabel;
        [SerializeField] private Button coinMagnetBuyButton;
        [SerializeField] private int coinMagnetMaxLevel = 10;

        private bool _playerInRange;

        private void Awake()
        {
            ballCountBuyButton.onClick.AddListener(() => TryBuy(BallCountKey, ballCountMaxLevel));
            projectileCountBuyButton.onClick.AddListener(() => TryBuy(ProjectileCountKey, projectileCountMaxLevel));
            projectileDamageBuyButton.onClick.AddListener(() => TryBuy(ProjectileDamageKey, projectileDamageMaxLevel));
            coinMagnetBuyButton.onClick.AddListener(() => TryBuy(CoinMagnetKey, coinMagnetMaxLevel));

            if (closeButton != null) closeButton.onClick.AddListener(CloseUpgradePanel);
        }

        private void Start()
        {
            if (keyPromptUI != null) keyPromptUI.SetActive(false);
            if (upgradePanel != null) upgradePanel.SetActive(false);
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (upgradePanel != null && upgradePanel.activeSelf) return; // schon offen

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                OpenUpgradePanel();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInRange = true;
            if (keyPromptUI != null) keyPromptUI.SetActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInRange = false;
            if (keyPromptUI != null) keyPromptUI.SetActive(false);
        }

        private void OpenUpgradePanel()
        {
            if (keyPromptUI != null) keyPromptUI.SetActive(false);
            if (upgradePanel != null) upgradePanel.SetActive(true);
            if (beatLane != null) beatLane.SetActive(false);

            RefreshUpgradeUI();
        }

        private void CloseUpgradePanel()
        {
            if (upgradePanel != null) upgradePanel.SetActive(false);
            if (keyPromptUI != null && _playerInRange) keyPromptUI.SetActive(true);
            if (beatLane != null) beatLane.SetActive(true);
        }

        private int GetLevel(string key) => GameSession.GetUpgradeLevel(key);

        private int GetCost(string key) => baseCost + GetLevel(key) * costIncreasePerLevel;

        /// <summary>
        /// Versucht, eine Upgrade-Stufe zu kaufen: prüft Max-Stufe + genug Münzen, zieht die
        /// Kosten ab und erhöht die gespeicherte Stufe um 1. Projektil-Schaden/-Anzahl und
        /// Münzmagnet wirken sich erst beim nächsten Levelstart aus (siehe PlayerController.
        /// ApplyUpgrades) - Münzanzeige und Tasten-Anzahl werden aber SOFORT aktualisiert, damit
        /// der Kauf schon in der Safe Zone sichtbar ist.
        /// </summary>
        private void TryBuy(string key, int maxLevel)
        {
            int level = GetLevel(key);
            if (level >= maxLevel) return;

            int cost = GetCost(key);
            if (!GameSession.SpendCoins(cost)) return; // nicht genug Münzen

            GameSession.IncreaseUpgradeLevel(key);
            RefreshUpgradeUI();

            if (player != null)
            {
                player.RefreshCoinDisplay();
            }

            if (key == BallCountKey && beatLane != null)
            {
                beatLane.RefreshNoteCount();
            }
        }

        private void RefreshUpgradeUI()
        {
            UpdateUpgradeLabel(ballCountLabel, "Tasten", BallCountKey, ballCountMaxLevel);
            UpdateUpgradeLabel(projectileCountLabel, "Projektile", ProjectileCountKey, projectileCountMaxLevel);
            UpdateUpgradeLabel(projectileDamageLabel, "Schaden", ProjectileDamageKey, projectileDamageMaxLevel);
            UpdateUpgradeLabel(coinMagnetLabel, "Münzmagnet", CoinMagnetKey, coinMagnetMaxLevel);
        }

        private void UpdateUpgradeLabel(TMP_Text label, string displayName, string key, int maxLevel)
        {
            if (label == null) return;

            int level = GetLevel(key);
            label.text = level >= maxLevel
                ? $"{displayName}: Stufe {level} (Max)"
                : $"{displayName}: Stufe {level} -> {GetCost(key)} Münzen";
        }
    }
}