using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gamify.Racing
{
    // Additive race presentation. GameManager remains the only owner of question/pause rules.
    [DefaultExecutionOrder(100)]
    public class RaceDirector : MonoBehaviour
    {
        public GameManager gameManager;
        public RaceBot[] bots;
        public Transform track;
        public Transform obstacleContainer;
        public TextMeshProUGUI positionText;
        public float checkpointDistance = 275f;
        public float[] botLaneXPositions = new[] { -11f, -8f, 8f, 11f };
        public Material obstacleMaterial;
        public RaceEnvironment raceEnvironment;
        public RacingCollisionGuard collisionGuard;
        public IReadOnlyList<BoxCollider> Obstacles => _obstacles;
        public Vector3[] TrafficSnapshot { get; private set; }
        public float stoppingGap = 12f;
        public float rankHysteresis = 2f;
        public LayerMask obstacleLayer;
        private readonly List<BoxCollider> _obstacles = new List<BoxCollider>();
        private bool[] _ahead;
        private int _lastQuestion = -1;
        private bool _prepared;

        public Checkpoint ActiveCheckpoint
        {
            get
            {
                int i = gameManager.questionManager.currentQuestionIndex;
                return i >= 0 && i < gameManager.checkpoints.Count ? gameManager.checkpoints[i] : null;
            }
        }

        public bool IsDriving => _prepared && gameManager.CurrentState == GameState.DrivingToCheckpoint &&
                                 gameManager.carController.State == CarState.Cruising;

        public void PrepareRace()
        {
            int count = gameManager.questionManager.Questions.Count;
            var checkpoints = gameManager.checkpoints;
            if (checkpoints.Count == 0 || count == 0) return;
            float firstZ = checkpoints[0].CheckpointZ;
            while (checkpoints.Count < count)
            {
                // Clone the authored checkpoint, never its question content.
                Checkpoint clone = Instantiate(checkpoints[0], gameManager.checkpointsContainer);
                clone.name = "Checkpoint_" + (checkpoints.Count + 1);
                checkpoints.Add(clone);
            }
            gameManager.checkpointSpacing = checkpointDistance;
            for (int i = 0; i < checkpoints.Count; i++)
            {
                checkpoints[i].gameObject.SetActive(i < count);
                checkpoints[i].checkpointIndex = i;
                Vector3 p = checkpoints[i].transform.position;
                p.z = firstZ + i * checkpointDistance;
                checkpoints[i].transform.position = p;
                checkpoints[i].ResetAllDoors();
            }
            ExtendTrack(firstZ + (count - 1) * checkpointDistance + 45f);
            BuildObstacles(count);
            _ahead = new bool[bots.Length];
            TrafficSnapshot = new Vector3[bots.Length];
            for (int i = 0; i < bots.Length; i++)
            {
                bots[i].ResetForRace(this, i);
                _ahead[i] = bots[i].transform.position.z > gameManager.carController.transform.position.z;
            }
            _lastQuestion = -1;
            _prepared = true;
            if (collisionGuard != null) collisionGuard.ResetContacts();
            if (raceEnvironment != null) raceEnvironment.RefreshLength(firstZ + (count - 1) * checkpointDistance + 45f);
            UpdatePosition();
        }

        private void LateUpdate()
        {
            if (!_prepared) return;
            bool visible = gameManager.CurrentState != GameState.Loading && gameManager.CurrentState != GameState.ResultsScreen;
            if (positionText != null) positionText.gameObject.SetActive(visible);
            if (!IsDriving)
            {
                foreach (var bot in bots) bot.Hold();
                return;
            }
            int question = gameManager.questionManager.currentQuestionIndex;
            if (question != _lastQuestion)
            {
                foreach (var bot in bots) bot.BeginSegment();
                _lastQuestion = question;
            }
            // Bound the traffic integration step during editor hitches. All bots
            // observe the same snapshot and commit after planning is complete.
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int i = 0; i < bots.Length; i++) TrafficSnapshot[i] = bots[i].transform.position;
            foreach (var bot in bots) bot.Plan(dt);
            foreach (var bot in bots) bot.Commit(dt);
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            int rank = 1;
            float playerZ = gameManager.carController.transform.position.z;
            for (int i = 0; i < bots.Length; i++)
            {
                float delta = bots[i].transform.position.z - playerZ;
                if (delta > rankHysteresis) _ahead[i] = true;
                else if (delta < -rankHysteresis) _ahead[i] = false;
                if (_ahead[i]) rank++;
            }
            if (positionText != null)
            {
                string suffix = rank == 1 ? "st" : rank == 2 ? "nd" : rank == 3 ? "rd" : "th";
                positionText.text = rank + suffix + " / " + (bots.Length + 1);
            }
        }

        public Vector3 ConstrainPlayerMotion(Vector3 from, Vector3 to)
        {
            // Swept AABB tests match the existing transform-driven car. No new physics
            // solver can push it off an answer lane or interfere with ram/recoil.
            foreach (var obstacle in _obstacles)
            {
                if (obstacle == null || !obstacle.gameObject.activeInHierarchy) continue;
                Bounds b = obstacle.bounds;
                b.Expand(new Vector3(2.3f, 2f, 3.8f));
                if (to.x >= b.min.x && to.x <= b.max.x)
                {
                    if (from.z <= b.min.z && to.z >= b.min.z) to.z = b.min.z - 0.02f;
                    else if (from.z >= b.max.z && to.z <= b.max.z) to.z = b.max.z + 0.02f;
                    else if (from.z > b.min.z && from.z < b.max.z) to.x = from.x;
                }
            }
            return to;
        }

        public bool LaneBlocked(float x, float z)
        {
            foreach (var obstacle in _obstacles)
            {
                Bounds b = obstacle.bounds;
                if (Mathf.Abs(b.center.x - x) < 2f && b.max.z > z - 3f && b.min.z < z + 18f) return true;
            }
            return false;
        }

        public float BotObstacleLimit(Vector3 from, float nextX)
        {
            float limit = float.PositiveInfinity;
            foreach (var obstacle in _obstacles)
            {
                if (obstacle == null) continue;
                Bounds b = obstacle.bounds;
                if (Mathf.Abs(b.center.x - nextX) < b.extents.x + 1.2f && b.max.z > from.z - 2f)
                    limit = Mathf.Min(limit, b.min.z - 2.2f);
            }
            return limit;
        }

        private void BuildObstacles(int questionCount)
        {
            _obstacles.Clear();
            foreach (Transform child in obstacleContainer) Destroy(child.gameObject);
            // A small fixed number, one lane each, well outside the answer/stop corridor.
            int total = Mathf.Min(4, questionCount - 1);
            Material mat = obstacleMaterial != null ? obstacleMaterial : gameManager.carController.carModel.GetComponentInChildren<Renderer>().sharedMaterial;
            for (int i = 0; i < total; i++)
            {
                float previousZ = gameManager.checkpoints[i].CheckpointZ;
                float nextWait = gameManager.checkpoints[i + 1].WaitingZ;
                if (nextWait - previousZ < 60f) continue;
                GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.name = "RaceBarrier_" + (i + 1);
                barrier.transform.SetParent(obstacleContainer);
                barrier.transform.position = new Vector3(gameManager.carController.laneXPositions[i % 4], 0.45f, (previousZ + nextWait) * 0.5f);
                barrier.transform.localScale = new Vector3(1.4f, 0.9f, 1.2f);
                barrier.GetComponent<Renderer>().sharedMaterial = mat;
                barrier.layer = LayerMask.NameToLayer("RaceObstacle");
                _obstacles.Add(barrier.GetComponent<BoxCollider>());
            }
        }

        private void ExtendTrack(float endZ)
        {
            // Preserve all original track objects; extend the three existing surfaces.
            foreach (string surface in new[] { "RoadSurface", "LeftShoulder", "RightShoulder" })
            {
                Transform t = track.Find(surface);
                if (t == null) continue;
                Vector3 pos = t.position, scale = t.localScale;
                float start = -30f;
                pos.z = (start + endZ) * 0.5f;
                scale.z = endZ - start;
                t.position = pos;
                t.localScale = scale;
            }
            Transform decorations = track.Find("RaceExtension");
            if (decorations == null)
            {
                decorations = new GameObject("RaceExtension").transform;
                decorations.SetParent(track);
            }
            foreach (Transform child in decorations) Destroy(child.gameObject);
            Transform stripe = track.Find("Stripe");
            if (stripe == null) return;
            if (raceEnvironment != null) return; // The batched environment owns the complete marking layout.
            for (float z = 362f; z < endZ; z += 12f)
                foreach (float x in new[] { -3f, 0f, 3f })
                {
                    Transform copy = Instantiate(stripe, decorations);
                    copy.position = new Vector3(x, stripe.position.y, z);
                }
        }
    }
}
