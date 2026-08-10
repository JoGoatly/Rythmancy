using UnityEngine;

namespace RhythmWitchClone.Gameplay
{
    /// <summary>
    /// Lässt die Kamera dem Spieler folgen, ohne die feste Kamera-Rotation zu verändern.
    /// Der Versatz (Offset) wird beim Start automatisch aus der Position übernommen, die
    /// du der Kamera im Editor gegeben hast - platziere die Kamera also einfach im Scene-View
    /// so, wie der Blickwinkel aussehen soll, den Rest übernimmt das Script.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target; // der Spieler
        [SerializeField] private float followSpeed = 5f;

        private Vector3 _offset;

        private void Start()
        {
            if (target != null)
            {
                _offset = transform.position - target.position;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + _offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        }
    }
}
