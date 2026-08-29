using UnityEngine;
using TMPro;
using RhythmWitchClone.Audio;

namespace RhythmWitchClone.Gameplay
{
    /// <summary>
    /// Gegner-Verhalten: bewegt sich auf den Spieler zu. Ist der Spieler innerhalb von "Attack Range",
    /// bekommt er Schaden, der Gegner selbst wird vom Spieler weg zurückgestoßen (Knockback) und
    /// braucht danach eine kurze Cooldown-Zeit, bevor er erneut angreifen kann.
    ///
    /// Zusätzlich: Gesundheit inkl. Tod (TakeDamage wird von PlayerController.UpdateProjectiles
    /// bei einem Projektil-Treffer aufgerufen), leichter Knockback bei Treffern, und ein kurz
    /// erscheinender Schadenstext über dem Gegner. Keine visuelle HP-Leiste, wie gewünscht.
    ///
    /// Wiederverwendbar für mehrere Gegner-Typen: Dieses eine Script als Prefab anlegen, für
    /// jede Gegner-Variante das Prefab duplizieren und nur Werte (Tempo, Schaden, Reichweite, ...)
    /// sowie das Aussehen (Textur am Visual-Kind) anpassen - kein neues Script pro Gegnertyp nötig.
    ///
    /// Setup:
    /// - Kommt auf das Gegner-Root-GameObject (bekommt automatisch einen CharacterController).
    /// - "Visual Transform" zeigt auf das Kind-Objekt mit Quad + BillboardSprite-Script,
    ///   genau wie beim Spieler.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Bewegung")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Angriff")]
        [Tooltip("Abstand zum Spieler, ab dem der Gegner angreift statt weiter zu verfolgen.")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private int damage = 10;
        [SerializeField] private float attackCooldown = 1f;

        [Header("Knockback (nach einem erfolgreichen Angriff)")]
        [SerializeField] private float knockbackForce = 6f;
        [SerializeField] private float knockbackDuration = 0.25f;

        [Header("Gesundheit")]
        [SerializeField] private int maxHealth = 50;
        private int _currentHealth;

        [Header("Knockback (durch einen Spieler-Treffer)")]
        [Tooltip("Bewusst schwächer als der Angriffs-Knockback, wie gewünscht ('leichter Knockback').")]
        [SerializeField] private float hitKnockbackForce = 2f;
        [SerializeField] private float hitKnockbackDuration = 0.15f;

        [Header("Schadenstext")]
        [SerializeField] private Color damageTextColor = Color.red;
        [SerializeField] private float damageTextFontSize = 4f;
        [SerializeField] private float damageTextHeight = 2f;
        [SerializeField] private float damageTextLifetime = 0.8f;

        [Header("Sichtbares Bild")]
        [Tooltip("Das Kind-Objekt mit dem sichtbaren Bild (Quad mit BillboardSprite-Script drauf), analog zum Spieler.")]
        [SerializeField] private Transform visualTransform;

        private CharacterController _controller;
        private PlayerController _player;

        private Vector3 _verticalVelocity;
        private Vector3 _knockbackStartVelocity;
        private Vector3 _knockbackVelocity;
        private float _knockbackTimer;
        private float _knockbackDurationTotal; // welche Dauer gerade gilt - Angriff- und Treffer-Knockback haben unterschiedliche Werte
        private float _stunTimer; // Betäubung durch Spieler-Spezialfähigkeiten (siehe ApplyStun)

        private float _attackCooldownTimer;
        private Vector3 _visualBaseScale;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (visualTransform != null)
            {
                _visualBaseScale = visualTransform.localScale;
            }

            // Verhindert, dass sich CharacterControllers von Gegnern gegenseitig wegschieben
            // (Unity würde sie sonst standardmäßig physikalisch auseinanderdrücken).
            // Setzt voraus, dass das Gegner-Prefab einer eigenen Layer zugewiesen ist (siehe Setup).
            Physics.IgnoreLayerCollision(gameObject.layer, gameObject.layer, true);
        }

        private void Start()
        {
            _currentHealth = maxHealth;

            _player = FindFirstObjectByType<PlayerController>();
            if (_player == null)
            {
                Debug.LogWarning("EnemyController: Kein PlayerController in der Szene gefunden.");
            }
        }

        private void Update()
        {
            if (_player == null) return;

            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }

            if (_stunTimer > 0f)
            {
                // Betäubt: keine Bewegung, kein Angriff - nur Knockback (falls gerade aktiv) und Schwerkraft laufen weiter.
                _stunTimer -= Time.deltaTime;

                if (_knockbackTimer > 0f)
                {
                    HandleKnockback();
                }
            }
            else if (_knockbackTimer > 0f)
            {
                HandleKnockback();
            }
            else
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _player.transform.position);

                if (distanceToPlayer <= attackRange)
                {
                    HandleAttack();
                }
                else
                {
                    HandleChase();
                }
            }

            ApplyGravity();
        }

        private void HandleChase()
        {
            Vector3 direction = _player.transform.position - transform.position;
            direction.y = 0f;
            direction.Normalize();

            FaceDirection(direction);

            Vector3 motion = direction * moveSpeed + Vector3.up * _verticalVelocity.y;
            _controller.Move(motion * Time.deltaTime);
        }

        private void HandleAttack()
        {
            if (_attackCooldownTimer > 0f) return; // noch in der Angriffspause - einfach stehen bleiben

            _player.TakeDamage(damage);
            _attackCooldownTimer = attackCooldown;

            // Knockback vom Spieler weg auslösen.
            Vector3 knockDirection = transform.position - _player.transform.position;
            knockDirection.y = 0f;

            TriggerKnockback(knockDirection, knockbackForce, knockbackDuration);
        }

        /// <summary>
        /// Betäubt den Gegner für die angegebene Dauer (keine Bewegung, kein Angriff) und stößt
        /// ihn zusätzlich kurz in die übergebene Richtung weg - für Spieler-Spezialfähigkeiten
        /// wie eine Schockwelle.
        /// </summary>
        public void ApplyStun(Vector3 knockbackDirection, float knockbackForce, float stunDuration)
        {
            _stunTimer = stunDuration;
            TriggerKnockback(knockbackDirection, knockbackForce, hitKnockbackDuration);
        }

        /// <summary>
        /// Setzt einen Knockback in die übergebene Richtung, mit eigener Stärke/Dauer -
        /// wird sowohl vom Angriffs-Rückstoß als auch vom Treffer-Knockback (TakeDamage) genutzt.
        /// </summary>
        private void TriggerKnockback(Vector3 direction, float force, float duration)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            _knockbackStartVelocity = direction.normalized * force;
            _knockbackVelocity = _knockbackStartVelocity;
            _knockbackTimer = duration;
            _knockbackDurationTotal = duration;
        }

        private void HandleKnockback()
        {
            _knockbackTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(_knockbackTimer / _knockbackDurationTotal); // 1 = gerade getroffen, 0 = Knockback vorbei
            _knockbackVelocity = _knockbackStartVelocity * t; // klingt linear ab

            Vector3 motion = _knockbackVelocity + Vector3.up * _verticalVelocity.y;
            _controller.Move(motion * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity.y < 0f)
            {
                _verticalVelocity.y = -2f;
            }
            _verticalVelocity.y += gravity * Time.deltaTime;
        }

        private void FaceDirection(Vector3 direction)
        {
            if (visualTransform == null || direction.sqrMagnitude < 0.0001f) return;

            bool movingRight = direction.x > 0f;
            float flippedSignX = movingRight ? -1f : 1f;

            visualTransform.localScale = new Vector3(
                Mathf.Abs(_visualBaseScale.x) * flippedSignX,
                _visualBaseScale.y,
                _visualBaseScale.z);
        }

        /// <summary>
        /// Zieht dem Gegner Schaden ab (z.B. von PlayerController.UpdateProjectiles bei einem
        /// Projektil-Treffer aufgerufen). hitSourcePosition bestimmt die Knockback-Richtung.
        /// Bei 0 HP wird der Gegner zerstört (tötbar, wie gewünscht - keine visuelle HP-Leiste)
        /// und lässt eine Münze fallen.
        /// </summary>
        public void TakeDamage(int amount, Vector3 hitSourcePosition)
        {
            _currentHealth -= amount;
            ShowDamageText(amount);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyDamagedSound(transform.position);
            }

            if (_currentHealth <= 0)
            {
                if (_player != null)
                {
                    _player.SpawnCoin(transform.position);
                }
                Destroy(gameObject);
                return;
            }

            Vector3 knockDirection = transform.position - hitSourcePosition;
            knockDirection.y = 0f;
            TriggerKnockback(knockDirection, hitKnockbackForce, hitKnockbackDuration);
        }

        /// <summary>
        /// Erzeugt eine kurz sichtbare Schadenszahl über dem Gegner (3D-Weltraum-Text, kein UI-
        /// Canvas nötig, da die Position pro Gegner unterschiedlich ist). Kein Bild/Textur -
        /// reiner Text, wird direkt zur Laufzeit erzeugt und nach kurzer Zeit automatisch entfernt.
        /// Bewusst NICHT als Kind des Gegners, damit die Zahl auch bei einem Todestreffer
        /// noch kurz sichtbar bleibt, statt sofort mit zerstört zu werden.
        /// </summary>
        private void ShowDamageText(int amount)
        {
            GameObject textObject = new GameObject("DamageText");
            textObject.transform.position = transform.position + Vector3.up * damageTextHeight;

            if (Camera.main != null)
            {
                // Die Kamera dreht sich in diesem Projekt nie, ein einmaliges Ausrichten reicht.
                textObject.transform.rotation = Camera.main.transform.rotation;
            }

            TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();
            tmp.text = amount.ToString();
            tmp.fontSize = damageTextFontSize;
            tmp.color = damageTextColor;
            tmp.alignment = TextAlignmentOptions.Center;

            Destroy(textObject, damageTextLifetime);
        }
    }
}