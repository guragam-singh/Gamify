using System;
using System.Collections;
using UnityEngine;

namespace Gamify.Racing
{
    public enum CarState
    {
        Idle,
        Cruising,
        Approaching,
        WaitingAtCheckpoint,
        Ramming,
        Recoiling
    }

    public class CarController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        public float cruiseSpeed = 16f;
        public float ramSpeed = 24f;
        public float laneChangeSpeed = 9f;

        [Header("Lane Settings")]
        public float[] laneXPositions = new float[] { -4.5f, -1.5f, 1.5f, 4.5f };
        public int currentLaneIndex = 1;

        [Header("Visual Elements")]
        public Transform carModel;
        public Transform[] wheels;

        [Header("Camera")]
        public Camera followCamera;
        public Vector3 cameraOffset = new Vector3(0f, 3.2f, -6.8f);
        public float cameraSmoothSpeed = 10f;

        public CarState State { get; private set; } = CarState.Idle;

        private float _targetZ = 0f;
        private float _currentSpeed = 0f;
        private Vector3 _originalCameraPos;
        private Coroutine _shakeCoroutine;

        private void Start()
        {
            if (followCamera == null)
            {
                followCamera = Camera.main;
            }
        }

        private void Update()
        {
            UpdateMovement();
            UpdateCamera();
            UpdateVisuals();
        }

        public void SetSelectedLane(int laneIndex)
        {
            if (laneIndex >= 0 && laneIndex < laneXPositions.Length)
            {
                currentLaneIndex = laneIndex;
            }
        }

        public void DriveToCheckpoint(float stopZ)
        {
            _targetZ = stopZ;
            State = CarState.Cruising;
        }

        public void RamForward(float targetDoorZ, Action onHit)
        {
            StartCoroutine(RamRoutine(targetDoorZ, onHit));
        }

        public void Recoil(float recoilZ, Action onComplete)
        {
            StartCoroutine(RecoilRoutine(recoilZ, onComplete));
        }

        private IEnumerator RamRoutine(float targetDoorZ, Action onHit)
        {
            State = CarState.Ramming;
            _currentSpeed = ramSpeed;

            while (transform.position.z < targetDoorZ)
            {
                transform.position += Vector3.forward * (_currentSpeed * Time.deltaTime);

                // Keep aligned with chosen lane
                float targetX = laneXPositions[currentLaneIndex];
                Vector3 pos = transform.position;
                pos.x = Mathf.MoveTowards(pos.x, targetX, laneChangeSpeed * 2f * Time.deltaTime);
                transform.position = pos;

                yield return null;
            }

            onHit?.Invoke();
        }

        private IEnumerator RecoilRoutine(float returnZ, Action onComplete)
        {
            State = CarState.Recoiling;
            TriggerCameraShake(0.35f, 0.4f);

            Vector3 startPos = transform.position;
            float targetX = laneXPositions[currentLaneIndex];
            Vector3 targetPos = new Vector3(targetX, startPos.y, returnZ);

            float elapsed = 0f;
            float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Ease out
                float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                yield return null;
            }

            transform.position = targetPos;
            State = CarState.WaitingAtCheckpoint;
            onComplete?.Invoke();
        }

        private void UpdateMovement()
        {
            // Lane interpolation
            float targetX = laneXPositions[currentLaneIndex];
            Vector3 currentPos = transform.position;
            currentPos.x = Mathf.Lerp(currentPos.x, targetX, laneChangeSpeed * Time.deltaTime);

            if (State == CarState.Cruising)
            {
                float dist = _targetZ - currentPos.z;
                if (dist > 12f)
                {
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, cruiseSpeed, 12f * Time.deltaTime);
                }
                else if (dist > 0.1f)
                {
                    // Decelerate as approaching checkpoint
                    float t = Mathf.Clamp01(dist / 12f);
                    _currentSpeed = Mathf.Lerp(2f, cruiseSpeed, t);
                }
                else
                {
                    currentPos.z = _targetZ;
                    _currentSpeed = 0f;
                    State = CarState.WaitingAtCheckpoint;
                }

                currentPos.z += _currentSpeed * Time.deltaTime;
            }
            else if (State == CarState.WaitingAtCheckpoint)
            {
                _currentSpeed = 0f;
            }

            transform.position = currentPos;
        }

        private void UpdateCamera()
        {
            if (followCamera == null) return;

            Vector3 desiredPos = new Vector3(
                transform.position.x * 0.4f, // Soft lane tracking
                transform.position.y + cameraOffset.y,
                transform.position.z + cameraOffset.z
            );

            followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, desiredPos, cameraSmoothSpeed * Time.deltaTime);
            followCamera.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
        }

        private void UpdateVisuals()
        {
            if (carModel == null) return;

            // Subtle body roll when changing lanes
            float targetX = laneXPositions[currentLaneIndex];
            float deltaX = targetX - transform.position.x;
            float rollAngle = Mathf.Clamp(-deltaX * 6f, -12f, 12f);

            // Subtle engine vibration when idling
            float vibe = (State == CarState.WaitingAtCheckpoint) ? Mathf.Sin(Time.time * 25f) * 0.02f : 0f;

            carModel.localRotation = Quaternion.Euler(0f, 0f, rollAngle);
            carModel.localPosition = new Vector3(0f, vibe, 0f);

            // Spin wheels
            if (wheels != null && _currentSpeed > 0.1f)
            {
                float spinDelta = (_currentSpeed / 0.4f) * 360f * Time.deltaTime;
                foreach (var w in wheels)
                {
                    if (w != null) w.Rotate(Vector3.right, spinDelta, Space.Self);
                }
            }
        }

        public void TriggerCameraShake(float intensity, float duration)
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(CameraShakeRoutine(intensity, duration));
        }

        private IEnumerator CameraShakeRoutine(float intensity, float duration)
        {
            if (followCamera == null) yield break;

            Vector3 baseCamPos = followCamera.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damp = 1f - (elapsed / duration);
                Vector3 offset = UnityEngine.Random.insideUnitSphere * (intensity * damp);
                followCamera.transform.position = baseCamPos + offset;
                yield return null;
            }
        }

        public void TeleportTo(Vector3 position, int laneIndex = 1)
        {
            currentLaneIndex = laneIndex;
            transform.position = position;
            _currentSpeed = 0f;
            State = CarState.Idle;

            if (followCamera != null)
            {
                followCamera.transform.position = new Vector3(
                    position.x * 0.4f,
                    position.y + cameraOffset.y,
                    position.z + cameraOffset.z
                );
            }
        }
    }
}
