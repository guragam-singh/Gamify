using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Gamify.Racing.Editor
{
    // Opt-in integration checks against the actual scene and existing game logic.
    // Never runs during normal gameplay or in a player build.
    public static class RacingSmokeChecks
    {
        public static readonly List<string> Results = new List<string>();
        public static bool Running { get; private set; }
        private static IEnumerator _routine;
        private static readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private static Keyboard _keyboard;
        private static Keyboard _previousKeyboard;
        private static GameManager _gm;
        private static float _previousScale;
        private static InputSettings.EditorInputBehaviorInPlayMode _previousInputBehavior;

        public static void Run()
        {
            if (!Application.isPlaying || Running) throw new InvalidOperationException("Enter Play Mode first.");
            _gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Results.Clear();
            _previousScale = Time.timeScale;
            _previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _previousKeyboard = Keyboard.current;
            _keyboard = InputSystem.AddDevice<Keyboard>("RacingCheckKeyboard");
            _keyboard.MakeCurrent();
            Running = true;
            _routine = Checks();
            _stack.Clear();
            _stack.Push(_routine);
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                while (_stack.Count > 0)
                {
                    IEnumerator current = _stack.Peek();
                    if (!current.MoveNext()) { _stack.Pop(); continue; }
                    var nested = current.Current as IEnumerator;
                    if (nested != null) { _stack.Push(nested); continue; }
                    return;
                }
                Finish();
            }
            catch (Exception ex)
            {
                Results.Add("FAIL: " + ex.Message);
                Debug.LogError("[Racing checks] " + ex);
                Finish();
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Time.timeScale = _previousScale;
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousInputBehavior;
            if (_gm != null) { _gm.enabled = true; _gm.carController.enabled = true; }
            if (_gm != null && _gm.raceDirector.collisionGuard != null) _gm.raceDirector.collisionGuard.enabled = true;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
            Running = false;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Results.Add("PASS: " + message);
        }

        private static void Invoke(string method)
        {
            typeof(GameManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_gm, null);
        }

        private static void InvokeCycleDoor(int direction)
        {
            typeof(GameManager).GetMethod("CycleDoor", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_gm, new object[] { direction });
        }

        private static IEnumerator Until(Func<bool> condition, float timeout = 15f)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out waiting for gameplay state: " + _gm.CurrentState);
                yield return null;
            }
        }

        private static IEnumerator Hold(float seconds, params Key[] keys)
        {
            float end = Time.time + seconds;
            // Execute the existing Update methods once per player frame with known
            // input, avoiding OS keyboard/focus events replacing the test keyboard.
            // Outside this short input window all components run normally.
            _gm.enabled = false;
            _gm.carController.enabled = false;
            int frame = -1;
            while (Time.time < end)
            {
                if (frame != Time.frameCount)
                {
                    frame = Time.frameCount;
                    _keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
                    InputSystem.Update();
                    typeof(CarController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_gm.carController, null);
                    Invoke("Update");
                }
                yield return null;
            }
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            _gm.enabled = true;
            _gm.carController.enabled = true;
            // Allow a player update to consume the released state.
            float release = Time.time + 0.08f;
            while (Time.time < release) yield return null;
        }

        private static IEnumerator Checks()
        {
            Time.timeScale = 1f;
            _gm.RestartGame();
            yield return Until(() => _gm.CurrentState == GameState.DrivingToCheckpoint);
            var car = _gm.carController;
            var race = _gm.raceDirector;
            var qm = _gm.questionManager;
            // Isolate controller/quiz regression from intentional driving contacts;
            // dedicated collision checks run with the guard enabled separately.
            if (race.collisionGuard != null) race.collisionGuard.enabled = false;
            Assert(qm.Questions.Count == 8, "Eight distinct local questions loaded");
            Assert(_gm.checkpoints.Count >= qm.Questions.Count, "Checkpoint capacity matches loaded questions");
            for (int i = 1; i < qm.Questions.Count; i++)
                Assert(Mathf.Approximately(_gm.checkpoints[i].CheckpointZ - _gm.checkpoints[i - 1].CheckpointZ, race.checkpointDistance), race.checkpointDistance + "m checkpoint spacing " + i);
            float start = car.transform.position.z;
            yield return Hold(0.4f);
            Assert(car.transform.position.z > start, "Original no-input automatic cruise preserved");
            int lane = car.currentLaneIndex;
            yield return Hold(0.1f, Key.A);
            Assert(car.currentLaneIndex == lane - 1, "A steers using existing lane interpolation (lane=" + car.currentLaneIndex + ", state=" + _gm.CurrentState + ")");
            yield return Hold(0.1f, Key.D);
            Assert(car.currentLaneIndex == lane, "D steers using existing lane interpolation");
            yield return Hold(1.1f, Key.S);
            Assert(car.CurrentSpeed < 0f, "S brakes and then reverses");
            float reverseZ = car.transform.position.z;
            yield return Hold(0.2f, Key.S);
            Assert(car.transform.position.z < reverseZ && car.transform.position.z >= 0f, "Reverse is bounded to current segment");
            // Stronger acceleration reaches the short first approach sooner; give
            // the throttle check a full open segment instead of testing inside braking.
            var throttleStart = car.transform.position;
            throttleStart.z = 0f;
            car.transform.position = throttleStart;
            yield return Hold(1.7f, Key.W);
            Assert(car.CurrentSpeed > car.cruiseSpeed, "W accelerates above automatic cruise speed");
            Assert(race.bots[0].transform.position.z > 8f, "Opponents advance along track");
            Time.timeScale = 4f;
            yield return Until(() => _gm.CurrentState == GameState.WaitingForAnswer);
            Time.timeScale = 1f;
            float waitingZ = car.transform.position.z;
            var positions = new Vector3[race.bots.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = race.bots[i].transform.position;
                Assert(waitingZ - positions[i].z >= race.stoppingGap - 0.1f, "Bot " + i + " reserves safe longitudinal stopping gap");
            }
            yield return Hold(0.5f, Key.W, Key.S);
            Assert(Mathf.Approximately(car.transform.position.z, waitingZ), "W/S cannot move the player during questions");
            for (int i = 0; i < positions.Length; i++)
                Assert(race.bots[i].transform.position == positions[i], "Bot " + i + " holds during question pause");
            for (int i = 0; i < 4; i++)
            {
                int previous = _gm.selectedDoorIndex;
                InvokeCycleDoor(1);
                yield return Hold(0.6f);
                Assert(_gm.selectedDoorIndex == (previous + 1) % 4 && Mathf.Abs(car.transform.position.x - car.laneXPositions[_gm.selectedDoorIndex]) < 0.1f,
                    "Question answer lane " + _gm.selectedDoorIndex + " selectable without blocking");
            }
            Assert(qm.score == 0 && qm.currentLives == qm.initialLives, "Steering/pausing adds no score or life penalties");
            // Wrong-answer ram, recoil, explanation, and same-question retry.
            _gm.selectedDoorIndex = (qm.GetCurrentQuestion().correctIndex + 1) % 4;
            car.SetSelectedLane(_gm.selectedDoorIndex);
            yield return Hold(0.6f);
            Invoke("ConfirmSelection");
            yield return Until(() => _gm.CurrentState == GameState.ShowingExplanation);
            Assert(qm.currentLives == qm.initialLives - 1 && qm.currentQuestionIndex == 0 && qm.score == 0, "Wrong answer loses one life, recoils, and preserves question/score");
            Assert(Mathf.Abs(car.transform.position.z - race.ActiveCheckpoint.WaitingZ) < 0.1f, "Wrong-answer recoil returns to original waiting position");
            yield return Hold(0.12f, Key.Space);
            Assert(_gm.CurrentState == GameState.WaitingForAnswer && qm.currentQuestionIndex == 0, "Space retries same question");
            // Obstacle sweep: both directions stop at the barrier, adjacent lane is free.
            var barrier = race.obstacleContainer.GetChild(0);
            Vector3 from = barrier.position + new Vector3(0f, 0f, -8f);
            Vector3 to = barrier.position + new Vector3(0f, 0f, 8f);
            Assert(race.ConstrainPlayerMotion(from, to).z < barrier.position.z, "Swept obstacle guard prevents forward tunnelling");
            Assert(race.ConstrainPlayerMotion(to, from).z > barrier.position.z, "Swept obstacle guard prevents reverse tunnelling");
            from.x += 3f; to.x += 3f;
            Assert(race.ConstrainPlayerMotion(from, to) == to, "Adjacent lane remains open beside obstacle");
            // Complete every loaded question through the original confirmation/ram/progression.
            Time.timeScale = 3f;
            for (int q = 0; q < qm.Questions.Count; q++)
            {
                if (_gm.CurrentState == GameState.DrivingToCheckpoint)
                {
                    car.SetSelectedLane((q + 1) % 4); // Keep clear of this segment's single barrier.
                    yield return Until(() => _gm.CurrentState == GameState.WaitingForAnswer, 70f);
                }
                _gm.selectedDoorIndex = qm.GetCurrentQuestion().correctIndex;
                car.SetSelectedLane(_gm.selectedDoorIndex);
                yield return Hold(0.55f);
                int beforeScore = qm.score;
                Invoke("ConfirmSelection");
                yield return Until(() => qm.correctAnswersCount == q + 1);
                Assert(qm.score > beforeScore && qm.currentLives == qm.initialLives - 1, "Correct answer " + (q + 1) + " scores without life changes");
                yield return Until(() => _gm.CurrentState == GameState.DrivingToCheckpoint || _gm.CurrentState == GameState.ResultsScreen);
            }
            Assert(_gm.CurrentState == GameState.ResultsScreen && _gm.gameUI.resultsPanel.activeSelf && qm.correctAnswersCount == 8,
                "All eight questions reach existing victory/results screen");
            yield return Hold(0.12f, Key.Space);
            yield return Until(() => _gm.CurrentState == GameState.DrivingToCheckpoint);
            Assert(qm.score == 0 && qm.currentLives == qm.initialLives && qm.currentQuestionIndex == 0, "Results Space restart restores existing stats");
            Assert(_gm.checkpoints.Count == 8 && race.obstacleContainer.childCount == 4, "Restart creates no duplicate checkpoints/obstacles");
            // HUD progress and hysteresis in the true straight-track distance metric.
            string rankBefore = race.positionText.text;
            race.bots[0].transform.position = new Vector3(-7f, 0.3f, car.transform.position.z + 30f);
            yield return Hold(0.1f);
            Assert(race.positionText.text.Contains("/ 5"), "Race position HUD includes player and four opponents");
            yield return Until(() => _gm.CurrentState == GameState.WaitingForAnswer);
            string frozenRank = race.positionText.text;
            yield return Hold(0.25f);
            Assert(race.positionText.text == frozenRank, "Race position remains stable during question pauses");
            // Timeout and lives exhaustion through the existing timer handler.
            for (int life = qm.initialLives; life > 0; life--)
            {
                typeof(GameManager).GetField("_timeRemaining", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_gm, 0.02f);
                yield return Until(() => _gm.CurrentState == GameState.ShowingExplanation || _gm.CurrentState == GameState.ResultsScreen);
                Assert(qm.currentLives == life - 1 && qm.currentQuestionIndex == 0, "Timeout consumes one life and retains checkpoint");
                if (life > 1) { yield return Hold(0.12f, Key.Space); }
            }
            Assert(_gm.CurrentState == GameState.ResultsScreen && _gm.gameUI.resultsPanel.activeSelf, "Out-of-lives results progression preserved");
            Results.Add("COMPLETE: local scene integration checks finished");
        }
    }
}
