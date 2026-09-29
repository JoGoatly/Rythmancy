using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

namespace RhythmWitchClone.Gameplay
{
    /// <summary>
    /// Tutorial-Dialog-System: zeigt eine Liste von Text-Schritten nacheinander an, jeweils
    /// Buchstabe für Buchstabe animiert ("Schreibmaschinen-Effekt"). Zwei Arten, wie ein Schritt
    /// weitergeht:
    /// - "Press E": normaler Fortschritt per Taste - erster Druck während des Schreibens zeigt
    ///   sofort den kompletten Text, ein weiterer Druck springt zum nächsten Schritt.
    /// - "Wait For Movement": wartet auf tatsächliche WASD-Eingabe (z.B. für "Probier die
    ///   Bewegung aus") - schaltet dafür automatisch die Spieler-Bewegung frei, die davor
    ///   gesperrt ist, und springt automatisch weiter, sobald sich der Spieler bewegt.
    /// - "Move To Area": zeigt einen Pfeil über dem Spieler, der zur Mitte einer unsichtbaren
    ///   Ziel-Zone ("Target Area", per Transform in der Szene platziert) zeigt - kein echter
    ///   Trigger-Collider nötig, reiner Abstandsvergleich. Springt automatisch weiter, sobald
    ///   der Spieler nah genug an der Zone ist ("Target Area Radius").
    /// - "Wait For Enemy Defeat": wartet, bis der bei "Enemy To Defeat" eingetragene Gegner
    ///   besiegt wurde (springt automatisch weiter, sobald er zerstört ist).
    /// - "Wait For Full Health": wartet, bis der Spieler wieder auf voller Gesundheit ist (z.B.
    ///   damit er die Heilung der Verteidigungskarte einmal selbst ausprobiert).
    /// - "Finish": letzter Schritt - der Text wird ganz normal angezeigt UND gleichzeitig das
    ///   Level-Win-Panel (aus PlayerController) geöffnet. E überspringt nur noch den Schreib-
    ///   Effekt, schaltet aber nicht weiter - der Spieler verlässt das Tutorial über den
    ///   "Zurück zum Menü"-Button des Win-Panels, der wie gewohnt in die Safe Zone führt.
    ///
    /// Zusätzlich pro Schritt möglich: "Damage To Apply On Show" fügt dem Spieler sofort Schaden
    /// zu (praktisch kombiniert mit "Ui Elements To Reveal", um z.B. gleichzeitig die HP-Leiste
    /// einzublenden).
    ///
    /// Jeder Schritt kann zusätzlich ein Marker-Objekt kurzzeitig ein-/ausblenden (z.B. ein Pfeil)
    /// UND/ODER UI-Elemente DAUERHAFT einblenden (z.B. HP-Leiste, Mana-Leiste, Karten) - so kann
    /// der Canvas am Anfang komplett leer sein (außer dem Dialogtext) und im Lauf des Tutorials
    /// nach und nach "aufploppen".
    ///
    /// Setup:
    /// - Kommt auf ein beliebiges GameObject in der Tutorial-Szene (z.B. ein leeres
    ///   "TutorialManager"-Objekt).
    /// - "Player": dein Player-GameObject, für die Bewegungs-Sperre.
    /// - "Steps": Liste im Inspector - pro Eintrag Text, Fortschritts-Art, optional Marker und
    ///   eine Liste an UI-Elementen, die ab diesem Schritt dauerhaft eingeblendet werden.
    /// - "Dialogue Panel": das UI-Panel, das den Dialog umrahmt (wird ein-/ausgeblendet).
    /// - "Dialogue Text": das TextMeshProUGUI-Objekt, in dem der Text erscheint.
    /// - "Continue Prompt UI": optional, ein kleiner "E"-Tastenhinweis, der nur bei "Press E"-
    ///   Schritten erscheint, sobald der aktuelle Text fertig geschrieben ist.
    ///
    /// Wichtig: Alle UI-Elemente, die erst "aufploppen" sollen (HP-Leiste, Mana-Leiste, Karten,
    /// etc.), müssen in der Tutorial-Szene selbst standardmäßig DEAKTIVIERT sein (Checkbox aus) -
    /// das Script blendet sie nur zum passenden Zeitpunkt ein, deaktiviert sie aber nie wieder.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public enum StepAdvanceMode
        {
            PressE,
            WaitForMovement,
            MoveToArea,
            WaitForEnemyDefeat,
            WaitForFullHealth,
            Finish
        }

        [System.Serializable]
        public class TutorialStep
        {
            [TextArea(2, 5)]
            public string text;
            public StepAdvanceMode advanceMode = StepAdvanceMode.PressE;
            [Tooltip("Schaltet die Spieler-Bewegung frei, sobald dieser Schritt gezeigt wird (z.B. für 'Probier WASD aus').")]
            public bool unlocksMovement;
            [Tooltip("Fügt dem Spieler sofort diesen Schaden zu, sobald dieser Schritt gezeigt wird (0 = kein Schaden). Praktisch kombiniert mit 'Ui Elements To Reveal', um z.B. gleichzeitig die HP-Leiste einzublenden.")]
            public int damageToApplyOnShow;
            [Tooltip("Optional: ein Objekt in der Szene, das NUR WÄHREND dieses Schritts ein-/ausgeblendet wird (z.B. Pfeil, Rahmen, Spotlight, oder ein kurzer 'wirft etwas'-Effekt).")]
            public GameObject marker;
            [Tooltip("UI-Elemente, die ab diesem Schritt DAUERHAFT eingeblendet werden (z.B. HP-Leiste) - bleiben danach sichtbar, anders als der Marker.")]
            public List<GameObject> uiElementsToReveal = new List<GameObject>();
            [Tooltip("Nur für 'Move To Area': Objekt in der Szene, dessen Position die Mitte der unsichtbaren Ziel-Zone markiert.")]
            public Transform targetArea;
            [Tooltip("Nur für 'Move To Area': Radius der Zone in Weltraum-Einheiten - näher als das gilt als 'erreicht'.")]
            public float targetAreaRadius = 1.5f;
            [Tooltip("Nur für 'Wait For Enemy Defeat': der Gegner, der besiegt werden muss, bevor es weitergeht.")]
            public EnemyController enemyToDefeat;
            [Tooltip("Nur für 'Wait For Enemy Defeat': Bild, das der Gegner beim Tod auf den Spieler wirft (leer lassen für keinen Wurf-Effekt).")]
            public Sprite thrownObjectSprite;
            [Tooltip("Schaden, den der geworfene Gegenstand beim Einschlag verursacht.")]
            public int thrownObjectDamage;
            [Tooltip("Wie lange der Gegenstand braucht, um beim Spieler anzukommen (Sekunden).")]
            public float thrownObjectFlightDuration = 0.5f;
        }

        [Header("Referenzen")]
        [SerializeField] private PlayerController player;
        [Tooltip("Pfeil-Objekt in der Szene (Sprite/Quad mit Pfeil-Textur, nach 'oben' zeigend gezeichnet), das bei 'Move To Area'-Schritten um den Spieler herum kreist und zur Ziel-Zone zeigt.")]
        [SerializeField] private Transform guideArrow;
        [SerializeField] private float guideArrowHeightOffset = 1f;
        [Tooltip("Wie weit der Pfeil vom Spieler entfernt kreist (in Richtung der Ziel-Zone) - wie ein Uhrzeiger um den Spieler herum.")]
        [SerializeField] private float guideArrowOrbitRadius = 1.5f;

        [Header("Schritte")]
        [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

        [Header("UI")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TMP_Text dialogueText;
        [Tooltip("Erscheint nur bei 'Press E'-Schritten, sobald der aktuelle Text fertig geschrieben ist.")]
        [SerializeField] private GameObject continuePromptUI;

        [Header("Schreibmaschinen-Effekt")]
        [SerializeField] private float charactersPerSecond = 30f;

        private int _currentStepIndex = -1;
        private bool _isTyping;
        private string _currentFullText = "";
        private int _visibleCharacterCount;
        private float _typeTimer;
        private Vector3 _lastKnownEnemyPosition;

        private class ThrownObject
        {
            public Transform Transform;
            public Vector3 StartPosition;
            public float Duration;
            public float Timer;
            public int Damage;
        }

        private readonly List<ThrownObject> _activeThrownObjects = new List<ThrownObject>();

        private void Start()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            HideAllMarkers();

            // Bewegung ist zu Beginn des Tutorials gesperrt - wird erst durch einen Schritt mit
            // "Unlocks Movement" wieder freigeschaltet.
            if (player != null) player.SetMovementLocked(true);

            ShowStep(0);
        }

        private void Update()
        {
            HandleTypewriter();
            UpdateThrownObjects();

            if (_currentStepIndex < 0 || _currentStepIndex >= steps.Count)
            {
                if (continuePromptUI != null) continuePromptUI.SetActive(false);
                return;
            }

            TutorialStep currentStep = steps[_currentStepIndex];

            if (currentStep.advanceMode == StepAdvanceMode.WaitForMovement)
            {
                if (continuePromptUI != null) continuePromptUI.SetActive(false);
                if (guideArrow != null) guideArrow.gameObject.SetActive(false);
                HandleWaitForMovement();
            }
            else if (currentStep.advanceMode == StepAdvanceMode.MoveToArea)
            {
                if (continuePromptUI != null) continuePromptUI.SetActive(false);
                HandleMoveToArea(currentStep);
            }
            else if (currentStep.advanceMode == StepAdvanceMode.WaitForEnemyDefeat)
            {
                if (continuePromptUI != null) continuePromptUI.SetActive(false);
                if (guideArrow != null) guideArrow.gameObject.SetActive(false);
                HandleWaitForEnemyDefeat(currentStep);
            }
            else if (currentStep.advanceMode == StepAdvanceMode.WaitForFullHealth)
            {
                if (continuePromptUI != null) continuePromptUI.SetActive(false);
                if (guideArrow != null) guideArrow.gameObject.SetActive(false);
                HandleWaitForFullHealth();
            }
            else if (currentStep.advanceMode == StepAdvanceMode.Finish)
            {
                // Letzter Schritt: Text läuft normal zu Ende, aber E schaltet nichts mehr weiter -
                // der Spieler verlässt das Tutorial über den Button im Win-Panel.
                if (continuePromptUI != null) continuePromptUI.SetActive(false);
                if (guideArrow != null) guideArrow.gameObject.SetActive(false);

                if (_isTyping && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    // E darf aber weiterhin den Schreib-Effekt überspringen.
                    _visibleCharacterCount = _currentFullText.Length;
                    if (dialogueText != null) dialogueText.text = _currentFullText;
                    _isTyping = false;
                }
            }
            else
            {
                if (continuePromptUI != null) continuePromptUI.SetActive(!_isTyping);
                if (guideArrow != null) guideArrow.gameObject.SetActive(false);
                HandleInput();
            }
        }

        private void HandleTypewriter()
        {
            if (!_isTyping) return;

            _typeTimer += Time.deltaTime;
            float secondsPerChar = charactersPerSecond > 0f ? 1f / charactersPerSecond : 0f;

            while (secondsPerChar > 0f && _typeTimer >= secondsPerChar && _visibleCharacterCount < _currentFullText.Length)
            {
                _typeTimer -= secondsPerChar;
                _visibleCharacterCount++;

                if (dialogueText != null)
                {
                    dialogueText.text = _currentFullText.Substring(0, _visibleCharacterCount);
                }
            }

            if (_visibleCharacterCount >= _currentFullText.Length)
            {
                _isTyping = false;
            }
        }

        private void HandleInput()
        {
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

            if (_isTyping)
            {
                // Erster Druck während des Schreibens: sofort den kompletten Text anzeigen,
                // statt direkt weiterzuspringen (aus den meisten Spielen bekanntes Verhalten).
                _visibleCharacterCount = _currentFullText.Length;
                if (dialogueText != null) dialogueText.text = _currentFullText;
                _isTyping = false;
                return;
            }

            ShowStep(_currentStepIndex + 1);
        }

        /// <summary>
        /// Für Schritte mit StepAdvanceMode.WaitForMovement: springt automatisch weiter, sobald
        /// der Spieler tatsächlich eine Bewegungstaste drückt - kein "E" nötig. Zählt erst, wenn
        /// der Text fertig geschrieben ist.
        /// </summary>
        private void HandleWaitForMovement()
        {
            if (_isTyping || Keyboard.current == null) return;

            bool movementPressed =
                Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed ||
                Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed ||
                Keyboard.current.upArrowKey.isPressed || Keyboard.current.leftArrowKey.isPressed ||
                Keyboard.current.downArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed;

            if (movementPressed)
            {
                ShowStep(_currentStepIndex + 1);
            }
        }

        /// <summary>
        /// Für Schritte mit StepAdvanceMode.MoveToArea: zeigt den Führungs-Pfeil zur Ziel-Zone
        /// und prüft jeden Frame den Abstand - kein echter Trigger-Collider nötig, reiner
        /// Abstandsvergleich zur Position von "Target Area". Springt automatisch weiter, sobald
        /// der Spieler nah genug dran ist.
        /// </summary>
        private void HandleMoveToArea(TutorialStep step)
        {
            if (_isTyping || player == null || step.targetArea == null)
            {
                if (guideArrow != null) guideArrow.gameObject.SetActive(false);
                return;
            }

            UpdateGuideArrow(step.targetArea.position);

            float distance = Vector3.Distance(player.transform.position, step.targetArea.position);
            if (distance <= step.targetAreaRadius)
            {
                if (guideArrow != null) guideArrow.gameObject.SetActive(false);
                ShowStep(_currentStepIndex + 1);
            }
        }

        /// <summary>
        /// Für Schritte mit StepAdvanceMode.WaitForEnemyDefeat: wartet, bis der übergebene Gegner
        /// zerstört wird (Unitys "== null"-Vergleich erkennt das automatisch, sobald Destroy()
        /// gelaufen ist, auch wenn die Referenz technisch noch gesetzt ist). Kein Event-System
        /// nötig, reines Prüfen jeden Frame. Merkt sich dabei laufend die letzte bekannte Position
        /// des Gegners, damit beim Tod (wo sein Transform nicht mehr existiert) trotzdem bekannt
        /// ist, von wo aus der Wurf-Effekt starten soll.
        /// </summary>
        private void HandleWaitForEnemyDefeat(TutorialStep step)
        {
            if (_isTyping) return;

            if (step.enemyToDefeat != null)
            {
                _lastKnownEnemyPosition = step.enemyToDefeat.transform.position;
                return;
            }

            // Gegner ist gerade besiegt worden (Referenz ist jetzt null).
            if (step.thrownObjectSprite != null)
            {
                SpawnThrownObject(_lastKnownEnemyPosition, step.thrownObjectSprite, step.thrownObjectDamage, step.thrownObjectFlightDuration);
            }
            else if (step.thrownObjectDamage > 0 && player != null)
            {
                player.TakeDamage(step.thrownObjectDamage);
            }

            ShowStep(_currentStepIndex + 1);
        }

        /// <summary>
        /// Für Schritte mit StepAdvanceMode.WaitForFullHealth: wartet, bis der Spieler wieder
        /// auf voller Gesundheit ist (z.B. damit er die Heilung der Verteidigungskarte einmal
        /// selbst ausprobiert). Springt automatisch weiter, sobald CurrentHealth == MaxHealth.
        /// </summary>
        private void HandleWaitForFullHealth()
        {
            if (_isTyping || player == null) return;

            if (player.CurrentHealth >= player.MaxHealth)
            {
                ShowStep(_currentStepIndex + 1);
            }
        }

        /// <summary>
        /// Erzeugt ein Bild, das sichtbar vom Todesort des Gegners zum Spieler fliegt und dort
        /// nach der übergebenen Flugzeit Schaden verursacht.
        /// </summary>
        private void SpawnThrownObject(Vector3 fromPosition, Sprite sprite, int damage, float duration)
        {
            if (player == null) return;

            GameObject thrownObject = new GameObject("ThrownObject");
            Vector3 startPosition = fromPosition + Vector3.up * 1f;
            thrownObject.transform.position = startPosition;

            if (Camera.main != null)
            {
                // Kamera dreht sich in diesem Projekt nie - einmaliges Ausrichten reicht.
                thrownObject.transform.rotation = Camera.main.transform.rotation;
            }

            SpriteRenderer renderer = thrownObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            _activeThrownObjects.Add(new ThrownObject
            {
                Transform = thrownObject.transform,
                StartPosition = startPosition,
                Duration = Mathf.Max(0.01f, duration),
                Timer = 0f,
                Damage = damage
            });
        }

        /// <summary>
        /// Bewegt alle aktuell fliegenden geworfenen Gegenstände vom Startpunkt zum Spieler und
        /// löst beim Ankommen den Schaden aus.
        /// </summary>
        private void UpdateThrownObjects()
        {
            for (int i = _activeThrownObjects.Count - 1; i >= 0; i--)
            {
                ThrownObject obj = _activeThrownObjects[i];
                obj.Timer += Time.deltaTime;
                float t = Mathf.Clamp01(obj.Timer / obj.Duration);

                Vector3 targetPosition = player != null ? player.transform.position + Vector3.up * 1f : obj.StartPosition;
                obj.Transform.position = Vector3.Lerp(obj.StartPosition, targetPosition, t);

                if (t >= 1f)
                {
                    if (player != null && obj.Damage > 0)
                    {
                        player.TakeDamage(obj.Damage);
                    }

                    Destroy(obj.Transform.gameObject);
                    _activeThrownObjects.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Positioniert den Pfeil auf einem Kreis um den Spieler herum, versetzt in Richtung der
        /// Ziel-Zone (wie ein Uhrzeiger) - liegt die Zone links, erscheint der Pfeil links vom
        /// Spieler und zeigt auch nach links. Bleibt dabei zur Kamera ausgerichtet (gleiche
        /// Technik wie beim Projektil: Blickrichtung zur Kamera + Ausrichtung in der Bildebene).
        /// </summary>
        private void UpdateGuideArrow(Vector3 targetPosition)
        {
            if (guideArrow == null || player == null) return;

            guideArrow.gameObject.SetActive(true);

            Vector3 direction = targetPosition - player.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();

                Vector3 orbitOffset = direction * guideArrowOrbitRadius;
                guideArrow.position = player.transform.position + orbitOffset + Vector3.up * guideArrowHeightOffset;

                Vector3 faceCameraDirection = Camera.main != null ? -Camera.main.transform.forward : Vector3.forward;
                guideArrow.rotation = Quaternion.LookRotation(faceCameraDirection, direction);
            }
        }

        private void ShowStep(int index)
        {
            HideAllMarkers();

            if (index < 0 || index >= steps.Count)
            {
                // Tutorial fertig - Dialog schließen, Spiel läuft normal weiter.
                if (dialoguePanel != null) dialoguePanel.SetActive(false);
                _currentStepIndex = steps.Count;
                return;
            }

            TutorialStep step = steps[index];

            _currentStepIndex = index;

            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (step.marker != null) step.marker.SetActive(true);

            foreach (GameObject uiElement in step.uiElementsToReveal)
            {
                if (uiElement != null) uiElement.SetActive(true);
            }

            if (step.unlocksMovement && player != null)
            {
                player.SetMovementLocked(false);
            }

            if (step.damageToApplyOnShow > 0 && player != null)
            {
                player.TakeDamage(step.damageToApplyOnShow);
            }

            // Bei einem "Finish"-Schritt zusätzlich das Win-Panel öffnen - der Dialogtext läuft
            // dabei ganz normal weiter, beides ist gleichzeitig sichtbar. Über den "Zurück zum
            // Menü"-Button des Win-Panels kommt der Spieler dann in die Safe Zone.
            if (step.advanceMode == StepAdvanceMode.Finish && player != null)
            {
                player.WinLevel();
            }

            _currentFullText = step.text ?? "";
            _visibleCharacterCount = 0;
            _typeTimer = 0f;
            _isTyping = true;

            if (dialogueText != null) dialogueText.text = "";
        }

        private void HideAllMarkers()
        {
            foreach (TutorialStep step in steps)
            {
                if (step.marker != null) step.marker.SetActive(false);
            }
        }
    }
}