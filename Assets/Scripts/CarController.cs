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
        [Header("Movement Speeds (WASD)")]
        public float maxSpeed = 22f;
        public float idleSpeed = 8f;
        public float acceleration = 22f;
        public float brakeDeceleration = 32f;
        public float steerSpeed = 12f;
        public float ramSpeed = 26f;
        public float laneChangeSpeed = 12f;

        [Header("Track Boundaries")]
        public float minRoadX = -5.6f;
        public float maxRoadX = 5.6f;

        [Header("Lane Settings")]
        public float[] laneXPositions = new float[] { -4.5f, -1.5f, 1.5f, 4.5f };
        public int currentLaneIndex = 1;

        [Header("Visual Elements")]
        public Transform carModel;
        public Transform[] wheels;
        public Transform frontLeftWheel;
        public Transform frontRightWheel;

        [Header("Camera")]
        public Camera followCamera;
        public Vector3 cameraOffset = new Vector3(0f, 3.4f, -7.2f);
        public float cameraSmoothSpeed = 10f;
        public float normalFov = 56f;
        public float highSpeedFov = 64f;

        public CarState State { get; private set; } = CarState.Idle;
        public float CurrentSpeed => _currentSpeed;

        private float _targetZ = 0f;
        private float _currentSpeed = 0f;
        private float _currentSteerInput = 0f;
        private float _bumpOffsetX = 0f;
        private float _wheelSpinAngle = 0f;
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
            UpdateAudio();
        }

        public void SetSelectedLane(int laneIndex)
        {
            if (laneIndex >= 0 && laneIndex < laneXPositions.Length)
            {
                currentLaneIndex = laneIndex;
            }
        }

        public int GetClosestLane(float xPos)
        {
            int closest = 0;
            float minDiff = float.MaxValue;
            for (int i = 0; i < laneXPositions.Length; i++)
            {
                float diff = Mathf.Abs(xPos - laneXPositions[i]);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = i;
                }
            }
            return closest;
        }

        public void DriveToCheckpoint(float stopZ)
        {
            _targetZ = stopZ;
            State = CarState.Cruising;
            if (_currentSpeed < idleSpeed)
            {
                _currentSpeed = idleSpeed;
            }
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
                pos.x = Mathf.MoveTowards(pos.x, targetX, laneChangeSpeed * 2.5f * Time.deltaTime);
                transform.position = pos;

                yield return null;
            }

            onHit?.Invoke();
        }

        private IEnumerator RecoilRoutine(float returnZ, Action onComplete)
        {
            State = CarState.Recoiling;
            TriggerCameraShake(0.4f, 0.45f);

            Vector3 startPos = transform.position;
            float targetX = laneXPositions[currentLaneIndex];
            Vector3 targetPos = new Vector3(targetX, startPos.y, returnZ);

            float elapsed = 0f;
            float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
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
            Vector3 currentPos = transform.position;

            if (State == CarState.Cruising)
            {
                // Read WASD / Arrow Input
                float rawThrottle = 0f;
                float rawSteer = 0f;

#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) rawThrottle += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) rawThrottle -= 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawSteer -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawSteer += 1f;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) rawThrottle += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) rawThrottle -= 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) rawSteer -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) rawSteer += 1f;
#endif

                // Throttle: W accelerates, S brakes, otherwise maintain idle cruise
                if (rawThrottle > 0f)
                {
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed, acceleration * Time.deltaTime);
                }
                else if (rawThrottle < 0f)
                {
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, brakeDeceleration * Time.deltaTime);
                }
                else
                {
                    // Gentle coast towards idleSpeed
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, idleSpeed, 8f * Time.deltaTime);
                }

                // Smooth Steering (A/D)
                _currentSteerInput = Mathf.MoveTowards(_currentSteerInput, rawSteer, 14f * Time.deltaTime);
                currentPos.x += _currentSteerInput * steerSpeed * Time.deltaTime;

                // Handle Bump displacement
                if (Mathf.Abs(_bumpOffsetX) > 0.01f)
                {
                    currentPos.x += _bumpOffsetX * Time.deltaTime * 6f;
                    _bumpOffsetX = Mathf.MoveTowards(_bumpOffsetX, 0f, 8f * Time.deltaTime);
                }

                // Road bounds clamp
                currentPos.x = Mathf.Clamp(currentPos.x, minRoadX, maxRoadX);

                // Distance to checkpoint
                float dist = _targetZ - currentPos.z;

                if (dist > 14f)
                {
                    // Free driving zone
                    currentPos.z += _currentSpeed * Time.deltaTime;
                }
                else if (dist > 0.15f)
                {
                    // Approaching checkpoint: smoothly slow down as car enters waiting zone
                    float targetApproachSpeed = Mathf.Lerp(2.5f, maxSpeed, Mathf.Clamp01(dist / 14f));
                    _currentSpeed = Mathf.Min(_currentSpeed, targetApproachSpeed);
                    currentPos.z += _currentSpeed * Time.deltaTime;
                }
                else
                {
                    // Arrived at checkpoint!
                    currentPos.z = _targetZ;
                    _currentSpeed = 0f;
                    _currentSteerInput = 0f;
                    currentLaneIndex = GetClosestLane(currentPos.x);
                    State = CarState.WaitingAtCheckpoint;
                }
            }
            else if (State == CarState.WaitingAtCheckpoint)
            {
                _currentSpeed = 0f;
                _currentSteerInput = 0f;
                // Snap smoothly to active lane
                float targetX = laneXPositions[currentLaneIndex];
                currentPos.x = Mathf.Lerp(currentPos.x, targetX, laneChangeSpeed * Time.deltaTime);
            }

            transform.position = currentPos;
        }

        private void UpdateCamera()
        {
            if (followCamera == null) return;

            Vector3 desiredPos = new Vector3(
                transform.position.x * 0.35f,
                transform.position.y + cameraOffset.y,
                transform.position.z + cameraOffset.z
            );

            followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, desiredPos, cameraSmoothSpeed * Time.deltaTime);
            followCamera.transform.rotation = Quaternion.Euler(14f, 0f, 0f);

            // Dynamic FOV for speed sensation
            float speedRatio = Mathf.Clamp01(_currentSpeed / maxSpeed);
            float targetFov = (State == CarState.Ramming) ? highSpeedFov + 4f :
                              Mathf.Lerp(normalFov, highSpeedFov, speedRatio);
            followCamera.fieldOfView = Mathf.Lerp(followCamera.fieldOfView, targetFov, 4f * Time.deltaTime);
        }

        private void UpdateVisuals()
        {
            if (carModel == null) return;

            float steerAngle = 0f;
            float rollAngle = 0f;

            if (State == CarState.Cruising)
            {
                steerAngle = _currentSteerInput * 25f;
                rollAngle = -_currentSteerInput * 7f;
            }
            else
            {
                float targetX = laneXPositions[currentLaneIndex];
                float deltaX = targetX - transform.position.x;
                rollAngle = Mathf.Clamp(-deltaX * 5.5f, -10f, 10f);
                steerAngle = Mathf.Clamp(-deltaX * 18f, -25f, 25f);
            }

            // Engine vibration when waiting at checkpoint
            float vibe = (State == CarState.WaitingAtCheckpoint) ? Mathf.Sin(Time.time * 24f) * 0.015f : 0f;

            carModel.localRotation = Quaternion.Euler(0f, 0f, rollAngle);
            carModel.localPosition = new Vector3(0f, vibe, 0f);

            // Wheel spinning and steering
            if (_currentSpeed > 0.05f)
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

        private void UpdateAudio()
        {
            if (GameManager.Instance != null && GameManager.Instance.gameAudio != null)
            {
                float pitch = 0.8f + (_currentSpeed / maxSpeed) * 0.9f;
                GameManager.Instance.gameAudio.SetEnginePitch(pitch);
            }
        }

        public void ApplyObstacleSlowdown(float factor, float shakeIntensity, float shakeDuration)
        {
            _currentSpeed = Mathf.Max(3f, _currentSpeed * factor);
            TriggerCameraShake(shakeIntensity, shakeDuration);
        }

        public void ApplyBump(float bumpForce, float speedPenaltyRatio, float shakeDuration)
        {
            _bumpOffsetX = bumpForce;
            _currentSpeed = Mathf.Max(4f, _currentSpeed * (1f - speedPenaltyRatio));
            TriggerCameraShake(0.2f, shakeDuration);
        }

        private void OnTriggerEnter(Collider other)
        {
            CheckCollision(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            CheckCollision(collision.gameObject);
        }

        private void CheckCollision(GameObject go)
        {
            Obstacle obs = go.GetComponent<Obstacle>();
            if (obs == null) obs = go.GetComponentInParent<Obstacle>();
            if (obs != null && !obs.HasCollided)
            {
                obs.OnHitByPlayer(this);
                return;
            }

            BotCarController bot = go.GetComponent<BotCarController>();
            if (bot == null) bot = go.GetComponentInParent<BotCarController>();
            if (bot != null)
            {
                bot.OnBumpPlayer(this);
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
            _currentSteerInput = 0f;
            _bumpOffsetX = 0f;
            State = CarState.Idle;

            if (followCamera != null)
            {
                followCamera.transform.position = new Vector3(
                    position.x * 0.35f,
                    position.y + cameraOffset.y,
                    position.z + cameraOffset.z
                );
                followCamera.fieldOfView = normalFov;
            }
        }
    }
}
