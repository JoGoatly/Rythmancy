using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using RhythmWitchClone.Audio;
using RhythmWitchClone.Levels;
using RhythmWitchClone.Core;

namespace RhythmWitchClone.Gameplay
{
    /// <summary>
    /// Erzeugt Bälle, die kreisförmig um den Spieler rotieren (wie in Rhythm Witch).
    /// Bei Linksklick wird geprüft, ob sich gerade ein Ball in der festen "Hit-Zone" befindet -
    /// je nach Genauigkeit gibt es Perfekt/Gut/OK als kurzen Text über dem Spieler, plus ein
    /// kurzes Aufleuchten + Scale-Bump auf dem getroffenen Ball.
    ///
    /// Wiederverwendung: Die Bälle nutzen intern BillboardSprite.cs für die Kamera-Ausrichtung -
    /// kein zusätzliches Skript pro Ball nötig.
    ///
    /// Setup:
    /// - Kommt auf das Player-Root-GameObject (neben PlayerController).
    /// - "Ball Textures": ein Bild pro gewünschtem Ball, Reihenfolge ist egal. Kein Material nötig -
    ///   wird über BillboardSprite automatisch erzeugt.
    /// - "Feedback Text": ein TextMeshProUGUI-Objekt in einem Canvas (normales UI-Element) für
    ///   Perfekt/Gut/OK/Schlecht. Position, Größe, Font etc. stellst du ganz normal per RectTransform
    ///   im Editor ein - das Skript animiert nur noch Fade + leichtes Aufsteigen relativ zu dieser Position.
    /// </summary>
    public class OrbitingBallsController : MonoBehaviour
    {
        private const int MaxBallCount = 8;

        [Header("Bälle")]
        [Tooltip("Ein Eintrag = ein Ball mit diesem Bild. Maximal 8 Bälle, der Abstand zwischen ihnen passt sich automatisch an die Anzahl an.")]
        [SerializeField] private List<Texture2D> ballTextures = new List<Texture2D>();
        [SerializeField] private float orbitRadius = 2f;
        [SerializeField] private float orbitHeight = 1.5f;
        [SerializeField] private float ballScale = 0.6f;

        [Header("Takt")]
        [Tooltip("Schläge pro Minute. Die Rotationsgeschwindigkeit wird automatisch so berechnet, dass bei gleichmäßigem Klicken exakt in diesem Takt IMMER ein Ball auf der Linie steht - unabhängig von der Ballanzahl.")]
        [SerializeField] private float beatsPerMinute = 120f;

        // Wird in SpawnBalls() aus beatsPerMinute + Ballanzahl berechnet, nicht direkt im Inspector gesetzt.
        private float _rotationSpeed;

        [Header("Hit-Zone")]
        [Tooltip("Fester Winkel (0-360°) auf dem Kreis, an dem geklickt werden muss.")]
        [SerializeField] private float hitZoneAngle = 90f;
        [Tooltip("Ab welchem Anteil des Ball-Radius (0-1) ein Treffer noch als 'Perfekt' zählt (0 = exakte Mitte).")]
        [SerializeField] private float perfectHitRatio = 0.3f;
        [Tooltip("Ab welchem Anteil des Ball-Radius (0-1) ein Treffer noch als 'Gut' zählt. Ab 1.0 (Ballrand) ist es kein Treffer mehr.")]
        [SerializeField] private float goodHitRatio = 0.6f;

        [Header("Ziel-Linie (visuelle Markierung der Hit-Zone)")]
        [SerializeField] private Color hitLineColor = Color.green;
        [SerializeField] private float hitLineWidth = 0.08f;
        [SerializeField] private float hitLineHeight = 1.5f;

        [Header("Treffereffekt")]
        [SerializeField] private float hitScaleMultiplier = 1.3f;
        [SerializeField] private float hitFlashDuration = 0.2f;
        [SerializeField] private float hitFlashBrightness = 4f;

        [Header("Feedback-Text (UI, im Canvas)")]
        [Tooltip("TextMeshProUGUI-Objekt in einem Canvas für Perfekt/Gut/OK/Schlecht. Position/Größe/Font stellst du ganz normal per RectTransform im Editor ein.")]
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private float feedbackDuration = 0.7f;
        [Tooltip("Wie weit der Text (in UI-Pixeln) beim Ausklingen nach oben wandert.")]
        [SerializeField] private float feedbackRiseDistance = 20f;
        [Tooltip("Maximale zufällige Neigung (Grad) beim Erscheinen, für den 'hingeworfen'-Effekt.")]
        [SerializeField] private float feedbackMaxTiltDegrees = 15f;

        private Vector2 _feedbackBaseAnchoredPos;
        private class OrbitBall
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float AngleOffset;
            public bool JudgedThisPass;
            public float FlashTimer;
            public Vector3 BaseScale;
        }

        private readonly List<OrbitBall> _balls = new List<OrbitBall>();
        private float _currentBaseAngle;
        private float _feedbackTimer;
        private PlayerController _player; // für den Projektil-Abschuss bei Treffer, automatisch vom selben GameObject geholt
        private GameObject _hitLineObject;

        private void Start()
        {
            _player = GetComponent<PlayerController>();
            SpawnBalls();
            CreateHitLine();

            if (feedbackText != null)
            {
                _feedbackBaseAnchoredPos = feedbackText.rectTransform.anchoredPosition;
                feedbackText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            UpdateBallPositionsAndAnimation();
            HandleHitInput();
            UpdateFeedbackText();
        }

        /// <summary>
        /// Blendet die Bälle + Ziel-Linie komplett ein/aus und pausiert die komplette Logik
        /// dieses Skripts (z.B. während die Level-Auswahl in der Safe Zone offen ist).
        /// </summary>
        public void SetActive(bool isActive)
        {
            enabled = isActive;

            foreach (OrbitBall ball in _balls)
            {
                if (ball.Transform != null) ball.Transform.gameObject.SetActive(isActive);
            }

            if (_hitLineObject != null) _hitLineObject.SetActive(isActive);

            if (!isActive && feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Zerstört alle vorhandenen Bälle und erzeugt sie neu, basierend auf der aktuell
        /// gespeicherten Upgrade-Stufe (GameSession, pro Speicherstand getrennt). Wird von UpgradeController direkt nach
        /// einem Ball-Anzahl-Kauf aufgerufen, damit die Änderung sofort sichtbar ist, statt
        /// erst beim nächsten Levelstart.
        /// </summary>
        public void RefreshBallCount()
        {
            foreach (OrbitBall ball in _balls)
            {
                if (ball.Transform != null) Destroy(ball.Transform.gameObject);
            }
            _balls.Clear();

            SpawnBalls();
        }

        private void SpawnBalls()
        {
            if (ballTextures.Count == 0) return;

            // Standardmäßig (Upgrade-Stufe 0) IMMER nur 1 Ball, egal wie viele Texturen in der
            // Liste stehen - die Liste dient nur als Bilder-Vorrat für spätere Upgrade-Stufen.
            // Reicht die Textur-Liste nicht für die volle Anzahl, werden die Bilder einfach wiederholt.
            int upgradeLevel = GameSession.GetUpgradeLevel(UpgradeController.BallCountKey);
            int count = Mathf.Clamp(1 + upgradeLevel, 1, MaxBallCount);

            // Geschwindigkeit so berechnen, dass der Zeitabstand zwischen zwei aufeinanderfolgenden
            // "Ball steht auf der Linie"-Momenten exakt einer Taktlänge (60 / BPM Sekunden) entspricht:
            // Winkelabstand zwischen Bällen (360/count) geteilt durch Taktlänge = nötige Grad/Sekunde.
            // -> vereinfacht: Geschwindigkeit = 6 * BPM / count
            _rotationSpeed = 6f * beatsPerMinute / count;

            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ballObject.name = $"OrbitBall_{i}";

                Collider existingCollider = ballObject.GetComponent<Collider>();
                if (existingCollider != null) Destroy(existingCollider);

                ballObject.transform.SetParent(transform, worldPositionStays: false);
                ballObject.transform.localScale = Vector3.one * ballScale;

                MeshRenderer renderer = ballObject.GetComponent<MeshRenderer>();

                // Wiederverwendung: BillboardSprite übernimmt Material-Erzeugung + Textur-Zuweisung + Kamera-Ausrichtung.
                BillboardSprite billboard = ballObject.AddComponent<BillboardSprite>();
                billboard.SetTexture(ballTextures[i % ballTextures.Count]);

                _balls.Add(new OrbitBall
                {
                    Transform = ballObject.transform,
                    Renderer = renderer,
                    AngleOffset = i * angleStep,
                    BaseScale = ballObject.transform.localScale
                });
            }
        }

        /// <summary>
        /// Erzeugt eine feste, senkrechte Linie an der Hit-Zone-Position (siehe Screenshot-Referenz).
        /// LineRenderer.alignment = View richtet die Linie automatisch zur Kamera aus, wie ein Billboard -
        /// dafür ist kein zusätzlicher Code nötig.
        /// </summary>
        private void CreateHitLine()
        {
            GameObject lineObject = new GameObject("HitLine");
            lineObject.transform.SetParent(transform, worldPositionStays: false);
            _hitLineObject = lineObject;

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.positionCount = 2;
            line.startWidth = hitLineWidth;
            line.endWidth = hitLineWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = hitLineColor;
            line.endColor = hitLineColor;

            float radians = hitZoneAngle * Mathf.Deg2Rad;
            Vector3 basePos = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * orbitRadius;

            line.SetPosition(0, basePos + Vector3.up * (orbitHeight - hitLineHeight / 2f));
            line.SetPosition(1, basePos + Vector3.up * (orbitHeight + hitLineHeight / 2f));
        }

        private void UpdateBallPositionsAndAnimation()
        {
            _currentBaseAngle = (_currentBaseAngle + _rotationSpeed * Time.deltaTime) % 360f;

            foreach (OrbitBall ball in _balls)
            {
                float angle = (_currentBaseAngle + ball.AngleOffset) % 360f;
                float radians = angle * Mathf.Deg2Rad;

                Vector3 localOffset = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * orbitRadius;
                localOffset.y = orbitHeight;
                ball.Transform.localPosition = localOffset;

                float angularDistance = Mathf.Abs(Mathf.DeltaAngle(angle, hitZoneAngle));
                float arcDistance = angularDistance * Mathf.Deg2Rad * orbitRadius; // Bogenlänge = echter Abstand in Weltraum-Einheiten
                float ballRadius = ball.BaseScale.x * 0.5f; // Quad-Grundmaß ist 1x1, Radius = halbe Kantenlänge

                if (arcDistance > ballRadius)
                {
                    ball.JudgedThisPass = false; // Ball berührt die Linie nicht mehr -> beim nächsten Durchgang wieder treffbar
                }

                UpdateHitAnimation(ball);
            }
        }

        private void UpdateHitAnimation(OrbitBall ball)
        {
            if (ball.FlashTimer <= 0f) return;

            ball.FlashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(ball.FlashTimer / hitFlashDuration); // 1 = gerade getroffen, 0 = Effekt vorbei

            ball.Transform.localScale = Vector3.Lerp(ball.BaseScale, ball.BaseScale * hitScaleMultiplier, t);

            if (ball.Renderer != null)
            {
                float brightness = Mathf.Lerp(1f, hitFlashBrightness, t);
                Color flashColor = Color.white * brightness;
                flashColor.a = 1f;
                ball.Renderer.material.color = flashColor;
            }
        }

        private void HandleHitInput()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            // Klicks auf UI-Elemente (Buttons in der Level-Auswahl, im Pause-Menü, etc.) sollen
            // NICHT zusätzlich als Ball-Treffer/-Fehlschlag gewertet werden - sonst poppt bei jedem
            // Button-Klick ein "Schlecht"-Text auf, der (mit Raycast Target) den nächsten Klick blockieren kann.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            OrbitBall bestCandidate = null;
            float bestRatio = float.MaxValue;

            foreach (OrbitBall ball in _balls)
            {
                if (ball.JudgedThisPass) continue;

                float angle = (_currentBaseAngle + ball.AngleOffset) % 360f;
                float angularDistance = Mathf.Abs(Mathf.DeltaAngle(angle, hitZoneAngle));
                float arcDistance = angularDistance * Mathf.Deg2Rad * orbitRadius;
                float ballRadius = ball.BaseScale.x * 0.5f;

                // Nur ein "Treffer", wenn die Linie den Ball tatsächlich visuell überschneidet.
                if (ballRadius <= 0f) continue;
                float ratio = arcDistance / ballRadius; // 0 = exakte Mitte, 1 = gerade noch am Rand berührt

                if (ratio <= 1f && ratio < bestRatio)
                {
                    bestRatio = ratio;
                    bestCandidate = ball;
                }
            }

            if (bestCandidate != null)
            {
                RegisterHit(bestCandidate, bestRatio);
            }
            else
            {
                ShowFeedback("Schlecht");
                if (AudioManager.Instance != null) AudioManager.Instance.PlayMissHitSound();
            }
        }

        private void RegisterHit(OrbitBall ball, float hitRatio)
        {
            ball.JudgedThisPass = true;
            ball.FlashTimer = hitFlashDuration;

            string rating = hitRatio <= perfectHitRatio ? "Perfekt"
                : hitRatio <= goodHitRatio ? "Gut"
                : "OK";

            ShowFeedback(rating);
            PlayHitSound(rating);

            // Nur bei einem echten Treffer wird geschossen - hitRatio steuert dort den Schaden
            // (0 = exakte Mitte = voller Schaden, 1 = Rand des Balls = kaum Schaden).
            if (_player != null)
            {
                _player.FireProjectileAtNearestEnemy(hitRatio);
            }
        }

        private void PlayHitSound(string rating)
        {
            if (AudioManager.Instance == null) return;

            switch (rating)
            {
                case "Perfekt":
                    AudioManager.Instance.PlayPerfectHitSound();
                    break;
                case "Gut":
                    AudioManager.Instance.PlayGoodHitSound();
                    break;
                default:
                    AudioManager.Instance.PlayOkHitSound();
                    break;
            }
        }

        private void ShowFeedback(string rating)
        {
            if (feedbackText == null) return;

            feedbackText.text = rating;
            feedbackText.rectTransform.anchoredPosition = _feedbackBaseAnchoredPos;

            float randomTilt = Random.Range(-feedbackMaxTiltDegrees, feedbackMaxTiltDegrees);
            feedbackText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, randomTilt);

            Color c = feedbackText.color;
            c.a = 1f;
            feedbackText.color = c;
            feedbackText.gameObject.SetActive(true);
            _feedbackTimer = feedbackDuration;
        }

        private void UpdateFeedbackText()
        {
            if (feedbackText == null) return;
            if (_feedbackTimer <= 0f)
            {
                if (feedbackText.gameObject.activeSelf) feedbackText.gameObject.SetActive(false);
                return;
            }

            _feedbackTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(_feedbackTimer / feedbackDuration);

            // Text steigt beim Ausklingen leicht nach oben (in UI-Pixeln) und verblasst.
            feedbackText.rectTransform.anchoredPosition =
                _feedbackBaseAnchoredPos + Vector2.up * ((1f - t) * feedbackRiseDistance);

            Color c = feedbackText.color;
            c.a = t;
            feedbackText.color = c;
        }
    }
}