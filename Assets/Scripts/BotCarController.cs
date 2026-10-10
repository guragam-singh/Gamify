using System.Collections;
using UnityEngine;

namespace Gamify.Racing
{
    public class BotCarController : MonoBehaviour
    {
        [Header("Speed Settings")]
        public float baseSpeed = 15f;
        public float speedVariance = 2f;
        public float laneChangeSpeed = 4f;

        [Header("Lane Settings")]
        public float[] laneXPositions = new float[] { -4.5f, -1.5f, 1.5f, 4.5f };
        public int currentLaneIndex = 0;

        [Header("Track Bounds")]
        public float loopStartZ = 10f;
        public float loopEndZ = 650f;

        [Header("Visuals")]
        public Transform carModel;
        public Transform[] wheels;
        public Transform frontLeftWheel;
        public Transform frontRightWheel;

        private float _currentSpeed;
        private float _targetSpeed;
        private float _laneChangeTimer;
        private float _wheelSpinAngle;
        private Vector3 _initialPosition;
        private int _initialLane;
        private float _lastBumpTime = -1f;

        private void Awake()
        {
            _initialPosition = transform.position;
            _initialLane = currentLaneIndex;
            _currentSpeed = baseSpeed + Random.Range(-speedVariance, speedVariance);
            _targetSpeed = _currentSpeed;
            _laneChangeTimer = Random.Range(2f, 5f);
        }

        private void Update()
        {
            UpdateAI();
            UpdateMovement();
            UpdateVisuals();
        }

        private void UpdateAI()
        {
            _laneChangeTimer -= Time.deltaTime;
            if (_laneChangeTimer <= 0f)
            {
                _laneChangeTimer = Random.Range(3f, 6f);
                // Choose adjacent lane or random lane
                int step = Random.value < 0.5f ? -1 : 1;
                int newLane = Mathf.Clamp(currentLaneIndex + step, 0, laneXPositions.Length - 1);
                currentLaneIndex = newLane;
                _targetSpeed = baseSpeed + Random.Range(-speedVariance, speedVariance);
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, 3f * Time.deltaTime);
        }

        private void UpdateMovement()
        {
            Vector3 pos = transform.position;

            // Forward movement
            pos.z += _currentSpeed * Time.deltaTime;

            // Smooth X movement towards current lane
            float targetX = laneXPositions[currentLaneIndex];
            pos.x = Mathf.Lerp(pos.x, targetX, laneChangeSpeed * Time.deltaTime);

            // Loop track bounds
            if (pos.z > loopEndZ)
            {
                pos.z = loopStartZ;
                currentLaneIndex = Random.Range(0, laneXPositions.Length);
                pos.x = laneXPositions[currentLaneIndex];
            }

            transform.position = pos;
        }

        private void UpdateVisuals()
        {
            if (carModel == null) return;

            float targetX = laneXPositions[currentLaneIndex];
            float deltaX = targetX - transform.position.x;
            float rollAngle = Mathf.Clamp(-deltaX * 4f, -8f, 8f);
            float steerAngle = Mathf.Clamp(-deltaX * 12f, -20f, 20f);

            carModel.localRotation = Quaternion.Euler(0f, 0f, rollAngle);

            if (_currentSpeed > 0.1f)
            {
                _wheelSpinAngle += (_currentSpeed / 0.35f) * 360f * Time.deltaTime;
                _wheelSpinAngle %= 360f;
            }

            if (wheels != null)
            {
                foreach (var w in wheels)
                {
                    if (w == null) continue;
                    bool isFront = (w == frontLeftWheel || w == frontRightWheel);
                    float yaw = isFront ? steerAngle : 0f;
                    w.localRotation = Quaternion.Euler(_wheelSpinAngle, yaw, 0f);
                }
            }
        }

        public void OnBumpPlayer(CarController player)
        {
            if (Time.time - _lastBumpTime < 0.6f) return;
            _lastBumpTime = Time.time;

            if (player != null)
            {
                // Calculate bump direction
                float diffX = player.transform.position.x - transform.position.x;
                float bumpDir = Mathf.Sign(diffX);
                if (Mathf.Abs(diffX) < 0.1f) bumpDir = 1f;

                player.ApplyBump(bumpDir * 2.2f, 0.25f, 0.2f);

                // Nudge bot away slightly
                Vector3 p = transform.position;
                p.x -= bumpDir * 0.8f;
                transform.position = p;
            }
        }

        public void ResetBot()
        {
            transform.position = _initialPosition;
            currentLaneIndex = _initialLane;
            _currentSpeed = baseSpeed;
            _targetSpeed = baseSpeed;
            _laneChangeTimer = Random.Range(2f, 4f);
        }
    }
}
