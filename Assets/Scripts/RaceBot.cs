using UnityEngine;

namespace Gamify.Racing
{
    // Share player racing lanes on straights; reserve outer bypasses near gates.
    public class RaceBot : MonoBehaviour
    {
        public Transform model;
        public Transform[] wheels;
        public float speed = 17f;
        public int lane;
        public float acceleration = 7f;
        public float deceleration = 18f;
        public float followingDistance = 7f;
        public float laneChangeTime = 0.65f;
        [Header("Off-screen recovery pace (open racing segments only)")]
        public float catchUpSpeed = 28f;
        public float catchUpAcceleration = 12f;
        public float CurrentSpeed => _speed;
        public Vector3 PlannedPosition { get; private set; }
        private RaceDirector _race;
        private int _index;
        private float _speed;
        private float _lateralVelocity;
        private Vector3 _before;
        private bool _bypassing;
        private float _nextLaneDecision;
        private bool _recoveringVisibility;

        public void ResetForRace(RaceDirector race, int index)
        {
            _race = race;
            _index = index;
            lane = index % race.gameManager.carController.laneXPositions.Length;
            _speed = _lateralVelocity = 0f;
            transform.position = new Vector3(race.gameManager.carController.laneXPositions[lane], 0.3f,
                index < 2 ? 8f + index * 8f : -8f - (index - 2) * 8f);
            PlannedPosition = transform.position;
            _bypassing = false;
            _nextLaneDecision = 0f;
            _recoveringVisibility = false;
            if (model != null) model.localRotation = Quaternion.identity;
        }

        public void BeginSegment() { Hold(); }

        public void Hold()
        {
            _speed = 0f;
            _lateralVelocity = 0f;
            if (model != null) model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, 0.2f);
        }

        public void Plan(float dt)
        {
            _before = transform.position;
            PlannedPosition = _before;
            var cp = _race.ActiveCheckpoint;
            if (cp == null || dt <= 0f) return;
            var player = _race.gameManager.carController;
            int question = _race.gameManager.questionManager.currentQuestionIndex;
            if (_before.z > cp.WaitingZ - 65f) _bypassing = true;
            if (_bypassing && question > 0 && _before.z > _race.gameManager.checkpoints[question - 1].CheckpointZ + 14f && _before.z < cp.WaitingZ - 90f)
                _bypassing = false;
            Vector3 playerPosition = player.transform.position;
            float behind = playerPosition.z - _before.z;
            if (behind > 12f) _recoveringVisibility = true;
            else if (behind < -12f) _recoveringVisibility = false;
            bool recovering = _recoveringVisibility && cp.WaitingZ - playerPosition.z > 80f;
            float pace = recovering ? catchUpSpeed : speed;
            float targetX = _bypassing ? _race.botLaneXPositions[_index % _race.botLaneXPositions.Length] : player.laneXPositions[Mathf.Clamp(lane, 0, player.laneXPositions.Length - 1)];
            // Committed lane changes: only decide every two seconds and only when
            // a slower leader/barrier warrants a safe adjacent overtake.
            if (!_bypassing && Time.time >= _nextLaneDecision)
            {
                bool blocked = _race.LaneBlocked(targetX, _before.z);
                // A faster recovering bot must be able to pass the player too;
                // otherwise the following limit strands it behind the forward camera.
                if (Mathf.Abs(playerPosition.x - targetX) < 2.4f && behind > 0f &&
                    behind < 20f && player.CurrentSpeed < pace - 0.5f) blocked = true;
                for (int i = 0; i < _race.bots.Length; i++)
                    if (_race.bots[i] != this && Mathf.Abs(_race.TrafficSnapshot[i].x - targetX) < 2.4f &&
                        _race.TrafficSnapshot[i].z > _before.z && _race.TrafficSnapshot[i].z - _before.z < 20f && _race.bots[i].CurrentSpeed < pace - 0.5f) blocked = true;
                if (blocked)
                    foreach (int candidate in new[] { lane - 1, lane + 1 })
                        if (candidate >= 0 && candidate < player.laneXPositions.Length && LaneClear(player.laneXPositions[candidate], _before.z))
                        { lane = candidate; targetX = player.laneXPositions[lane]; break; }
                _nextLaneDecision = Time.time + 2f;
            }
            float nextX = Mathf.SmoothDamp(_before.x, targetX, ref _lateralVelocity,
                laneChangeTime, 3f, dt);
            float limit = cp.WaitingZ - _race.stoppingGap - (_index % 2) * 8f;

            // Everyone plans against the same unmodified snapshot; use actual
            // swept lane overlap, not an arbitrary "same side of road" rule.
            for (int i = 0; i < _race.bots.Length; i++)
            {
                if (_race.bots[i] == this) continue;
                Vector3 other = _race.TrafficSnapshot[i];
                float otherTarget = _race.bots[i]._bypassing ? _race.botLaneXPositions[i % _race.botLaneXPositions.Length] : player.laneXPositions[Mathf.Clamp(_race.bots[i].lane, 0, player.laneXPositions.Length - 1)];
                bool sharedLane = Mathf.Abs(other.x - nextX) < 2.4f || Mathf.Abs(otherTarget - targetX) < 2.4f;
                if (sharedLane && (other.z > _before.z + 0.01f ||
                    (Mathf.Abs(other.z - _before.z) < 0.01f && i < _index)))
                    limit = Mathf.Min(limit, other.z - followingDistance);
            }
            if (Mathf.Abs(playerPosition.x - nextX) < 2.4f && playerPosition.z > _before.z)
                limit = Mathf.Min(limit, playerPosition.z - followingDistance);
            float obstacleLimit = _race.BotObstacleLimit(_before, nextX);
            limit = Mathf.Min(limit, obstacleLimit);
            float remaining = Mathf.Max(0f, limit - _before.z);
            // Long checkpoint segments used to leave the whole field permanently
            // behind the forward camera. Recover by driving, never repositioning.
            float desired = Mathf.Min(pace, Mathf.Sqrt(2f * deceleration * remaining));
            _speed = Mathf.MoveTowards(_speed, desired, (desired < _speed ? deceleration : recovering ? catchUpAcceleration : acceleration) * dt);
            float z = Mathf.Min(limit, _before.z + _speed * dt);
            z = Mathf.Max(_before.z, z); // Following never teleports a bot backwards.
            if (z <= _before.z + 0.0001f) _speed = 0f;
            // Don't merge sideways into adjacent traffic while alongside it.
            for (int i = 0; i < _race.bots.Length; i++)
                if (_race.bots[i] != this && Mathf.Abs(_race.TrafficSnapshot[i].z - z) < 5f &&
                    Mathf.Abs(_race.TrafficSnapshot[i].x - nextX) < 2.4f)
                { nextX = _before.x; _lateralVelocity = 0f; }
            PlannedPosition = new Vector3(nextX, _before.y, z);
        }

        private bool LaneClear(float x, float z)
        {
            if (_race.LaneBlocked(x, z)) return false;
            Vector3 p = _race.gameManager.carController.transform.position;
            if (Mathf.Abs(p.x - x) < 2.6f && Mathf.Abs(p.z - z) < 18f) return false;
            for (int i = 0; i < _race.bots.Length; i++)
                if (_race.bots[i] != this && Mathf.Abs(_race.TrafficSnapshot[i].x - x) < 2.6f && Mathf.Abs(_race.TrafficSnapshot[i].z - z) < 18f) return false;
            return true;
        }

        public void Commit(float dt)
        {
            transform.position = PlannedPosition;
            float travelled = PlannedPosition.z - _before.z;
            if (model != null)
            {
                float roll = Mathf.Clamp(-_lateralVelocity * 2f, -6f, 6f);
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(0f, 0f, roll), 8f * dt);
            }
            if (wheels != null)
                foreach (var wheel in wheels)
                    if (wheel != null) wheel.Rotate(Vector3.right, travelled / 0.4f * 360f, Space.Self);
        }
    }
}
