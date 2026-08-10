using UnityEngine;

namespace RhythmWitchClone.Gameplay
{
    /// <summary>
    /// Zeigt ein 2D-Bild (Textur) auf einem 3D-Objekt an und richtet es automatisch zur Kamera aus
    /// ("Billboard"-Technik, wie z.B. bei Doom oder vielen 2.5D-Spielen).
    ///
    /// Wiederverwendbar: Dieses eine Script kannst du sowohl für den Spieler als auch für alle
    /// zukünftigen Gegner verwenden - einfach das Prefab kopieren und im Inspector eine andere
    /// Textur eintragen. Es braucht kein eigenes Script pro Charakter.
    ///
    /// Setup im Editor:
    /// 1. Erstelle unter deinem Spieler-Objekt ein Quad (Rechtsklick -> 3D Object -> Quad).
    /// 2. Häng dieses Script auf das Quad.
    /// 3. Zieh dein Charakterbild (mit transparentem Hintergrund, z.B. PNG) ins "Texture"-Feld.
    /// Kein Material nötig - das Script erzeugt sich beim Start automatisch ein passendes,
    /// transparenzfähiges Material (Sprites/Default-Shader, in jedem Unity-Projekt enthalten).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class BillboardSprite : MonoBehaviour
    {
        [Header("Bild")]
        [Tooltip("Das Bild, das auf diesem Objekt angezeigt wird (mit transparentem Hintergrund empfohlen).")]
        [SerializeField] private Texture2D texture;

        [Header("Kamera-Ausrichtung")]
        [Tooltip("Falls leer, wird automatisch die Kamera mit dem Tag 'MainCamera' verwendet.")]
        [SerializeField] private Transform targetCamera;

        [Tooltip("An: Objekt übernimmt exakt die Kamera-Rotation (ideal bei fester Kamera, wie in diesem Projekt). " +
                 "Aus: Objekt dreht sich individuell zur Kamera-Position (nötig bei frei beweglicher/rotierender Kamera).")]
        [SerializeField] private bool matchCameraRotation = true;

        private MeshRenderer _meshRenderer;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();

            // Erzeugt automatisch ein passendes, transparenzfähiges Material - kein manuelles
            // Erstellen/Zuweisen im Editor mehr nötig, es reicht die Textur zuzuweisen.
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                _meshRenderer.material = new Material(spriteShader);
            }

            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            ApplyTexture();
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;

            if (matchCameraRotation)
            {
                // Übernimmt exakt die Rotation der Kamera - passend für eine feste Kamera,
                // die sich nicht um die Objekte herum dreht (wie in diesem Projekt).
                transform.rotation = targetCamera.rotation;
            }
            else
            {
                // Dreht sich individuell zur Kamera-Position - nötig, falls die Kamera
                // frei um die Szene rotieren kann.
                transform.forward = -(targetCamera.position - transform.position).normalized;
            }
        }

        /// <summary>
        /// Setzt die angezeigte Textur zur Laufzeit neu (z.B. für Animationen oder unterschiedliche Zustände).
        /// </summary>
        public void SetTexture(Texture2D newTexture)
        {
            texture = newTexture;
            ApplyTexture();
        }

        private void ApplyTexture()
        {
            if (texture == null || _meshRenderer == null) return;

            // .material (statt .sharedMaterial) erzeugt eine eigene Material-Instanz,
            // damit nicht versehentlich alle Objekte mit demselben Material betroffen sind.
            _meshRenderer.material.mainTexture = texture;
        }
    }
}