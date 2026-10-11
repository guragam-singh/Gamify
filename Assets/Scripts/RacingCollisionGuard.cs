using System.Collections.Generic;
using UnityEngine;

namespace Gamify.Racing
{
    // Swept contacts for the project's transform-driven, kinematic cars. Solver
    // callbacks are not reliable here: obstacle movement is clamped before contact.
    [DefaultExecutionOrder(200)]
    public class RacingCollisionGuard : MonoBehaviour
    {
        public GameManager gameManager;
        public float cooldownSeconds = 1.5f;
        public Vector3 playerHalfSize = new Vector3(1.15f, 0.75f, 1.9f);
        private readonly HashSet<int> _contacts = new HashSet<int>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly Dictionary<int, Vector3> _previous = new Dictionary<int, Vector3>();
        private float _nextPenaltyTime;
        private Vector3 _playerPrevious;
        private bool _initialized;

        public void ResetContacts()
        {
            _contacts.Clear(); _seen.Clear(); _previous.Clear();
            _nextPenaltyTime = 0f;
            _playerPrevious = gameManager.carController.transform.position;
            _initialized = true;
        }

        public void BeginSample() { _seen.Clear(); }

        public bool SampleContact(int rootId, bool touching)
        {
            if (!touching) return false;
            _seen.Add(rootId);
            bool enter = _contacts.Add(rootId);
            if (!enter || Time.time < _nextPenaltyTime) return false;
            if (!gameManager.ApplyRacingCollision()) return false;
            _nextPenaltyTime = Time.time + cooldownSeconds;
            return true;
        }

        public void EndSample() { _contacts.IntersectWith(_seen); }

        private void LateUpdate()
        {
            if (!_initialized || gameManager.raceDirector == null) return;
            var race = gameManager.raceDirector;
            Vector3 player = gameManager.carController.transform.position;
            if (!race.IsDriving)
            {
                // New contacts during ram, recoil or answer selection are harmless.
                _playerPrevious = player;
                _contacts.Clear(); _previous.Clear();
                return;
            }
            BeginSample();
            foreach (var bot in race.bots)
            {
                int id = bot.gameObject.GetInstanceID();
                Vector3 other = bot.transform.position;
                Vector3 old;
                if (!_previous.TryGetValue(id, out old)) old = other;
                Vector3 extents = playerHalfSize + new Vector3(1.15f, 0.75f, 1.9f);
                bool contact = Swept(_playerPrevious - old, player - other, new Bounds(Vector3.zero, extents * 2f));
                SampleContact(id, contact);
                _previous[id] = other;
            }
            foreach (var obstacle in race.Obstacles)
            {
                if (obstacle == null || !obstacle.gameObject.activeInHierarchy) continue;
                Bounds bounds = obstacle.bounds;
                bounds.Expand(playerHalfSize * 2f + Vector3.one * 0.08f);
                SampleContact(obstacle.gameObject.GetInstanceID(), Swept(_playerPrevious, player, bounds));
            }
            EndSample();
            _playerPrevious = player;
        }

        public static bool Swept(Vector3 from, Vector3 to, Bounds bounds)
        {
            if (bounds.Contains(from) || bounds.Contains(to)) return true;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.0001f) return false;
            float distance;
            return bounds.IntersectRay(new Ray(from, delta / length), out distance) && distance <= length;
        }
    }
}
