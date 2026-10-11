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

        [Header("Racing Input (checkpoint sequences retain control)")]
        public bool enableDrivingInput = true;
        public float acceleration = 18f;
        public float braking = 32f;
        public float reverseSpeed = 6f;
        public float boostedCruiseSpeed = 22f;
        public RaceDirector raceDirector;
        public float CurrentSpeed => _currentSpeed;
        private float _segmentStartZ;
        private float _steeringRepeat;
        private float _bodyPitch;
        public float drivingSteeringResponse = 12f;
        public float steeringRepeatSeconds = 0.28f;

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
            _segmentStartZ = transform.position.z;
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
            bool accelerate, brake;
            ReadDrivingInput(out accelerate, out brake);
            // Lane interpolation
            float targetX = laneXPositions[currentLaneIndex];
            Vector3 currentPos = transform.position;
            float steering = State == CarState.Cruising ? drivingSteeringResponse : laneChangeSpeed;
            float blend = State == CarState.Cruising ? 1f - Mathf.Exp(-steering * Time.deltaTime) : laneChangeSpeed * Time.deltaTime;
            currentPos.x = Mathf.Lerp(currentPos.x, targetX, blend);

            if (State == CarState.Cruising)
            {
                float dist = _targetZ - currentPos.z;
                if (dist > 12f)
                {
                    // No keys preserves the original automatic cruise. W adds throttle;
                    // S brakes and then reverses within this racing segment only.
                    float desiredSpeed = brake ? -reverseSpeed : (accelerate ? boostedCruiseSpeed : cruiseSpeed);
                    float rate = brake ? braking : (accelerate ? acceleration : 12f);
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredSpeed, rate * Time.deltaTime);
                    _bodyPitch = Mathf.Lerp(_bodyPitch, brake ? 2f : accelerate ? -1.5f : 0f, 8f * Time.deltaTime);
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
                currentPos.z = Mathf.Max(_segmentStartZ, currentPos.z);
                if (raceDirector != null)
                {
                    Vector3 safe = raceDirector.ConstrainPlayerMotion(transform.position, currentPos);
                    if (!Mathf.Approximately(safe.z, currentPos.z)) _currentSpeed = 0f;
                    currentPos = safe;
                }
            }
            else if (State == CarState.WaitingAtCheckpoint)
            {
                _currentSpeed = 0f;
            }

            transform.position = currentPos;
        }

        private void ReadDrivingInput(out bool accelerate, out bool brake)
        {
            accelerate = false;
            brake = false;
            if (!enableDrivingInput || State != CarState.Cruising ||
                (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DrivingToCheckpoint)) return;
            bool left = false, right = false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                accelerate = kb.wKey.isPressed;
                brake = kb.sKey.isPressed;
                left = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
                right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            accelerate |= Input.GetKey(KeyCode.W);
            brake |= Input.GetKey(KeyCode.S);
            left |= Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            right |= Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
#endif
            _steeringRepeat -= Time.deltaTime;
            if (left == right) { _steeringRepeat = 0f; return; }
            if (_steeringRepeat <= 0f)
            {
                SetSelectedLane(Mathf.Clamp(currentLaneIndex + (right ? 1 : -1), 0, laneXPositions.Length - 1));
                _steeringRepeat = steeringRepeatSeconds;
            }
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

            carModel.localRotation = Quaternion.Euler(State == CarState.Cruising ? _bodyPitch : 0f, 0f, rollAngle);
            carModel.localPosition = new Vector3(0f, vibe, 0f);

            // Spin wheels
            if (wheels != null && Mathf.Abs(_currentSpeed) > 0.1f)
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
            _bodyPitch = 0f;
            _steeringRepeat = 0f;

            if (followCamera != null)
            {
                followCamera.transform.position = new Vector3(
                    position.x * 0.4f,
                    position.y + cameraOffset.y,
                    position.z + cameraOffset.z
                );
            }
        }

        public void ApplyCollisionSlowdown() { _currentSpeed *= 0.35f; }
        public void StopRacing() { _currentSpeed = 0f; State = CarState.Idle; }
    }
}
