using UnityEngine;

namespace Gamify.Racing
{
    public enum ObstacleType
    {
        Cone,
        TireStack,
        Barrier
    }

    public class Obstacle : MonoBehaviour
    {
        public ObstacleType obstacleType = ObstacleType.Cone;
        [Range(0.1f, 0.9f)]
        public float speedMultiplierOnHit = 0.45f;
        public float cameraShakeIntensity = 0.35f;
        public float cameraShakeDuration = 0.3f;

        public bool HasCollided { get; private set; } = false;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Rigidbody _rb;
        private Collider _collider;

        private void Awake()
        {
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
        }

        public void OnHitByPlayer(CarController car)
        {
            if (HasCollided) return;
            HasCollided = true;

            if (car != null)
            {
                car.ApplyObstacleSlowdown(speedMultiplierOnHit, cameraShakeIntensity, cameraShakeDuration);
            }

            if (GameManager.Instance != null && GameManager.Instance.gameAudio != null)
            {
                GameManager.Instance.gameAudio.PlayWrong();
            }

            // Launch or tumble obstacle if it has a rigidbody
            if (_rb != null)
            {
                _rb.isKinematic = false;
                Vector3 blast = new Vector3(Random.Range(-2f, 2f), 3.5f, Random.Range(3f, 7f));
                _rb.AddForce(blast, ForceMode.Impulse);
                _rb.AddTorque(Random.insideUnitSphere * 12f, ForceMode.Impulse);
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }
        }

        public void ResetObstacle()
        {
            HasCollided = false;
            transform.position = _startPosition;
            transform.rotation = _startRotation;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }
        }
    }
}
