using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gamify.Racing
{
    public enum GameState
    {
        Loading,
        DrivingToCheckpoint,
        WaitingForAnswer,
        RammingDoor,
        ShowingExplanation,
        ResultsScreen
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Core References")]
        public QuestionManager questionManager;
        public CarController carController;
        public GameUI gameUI;
        public GameAudio gameAudio;

        [Header("Track & Checkpoints")]
        public List<Checkpoint> checkpoints = new List<Checkpoint>();
        public float checkpointSpacing = 120f;
        public Transform checkpointsContainer;

        [Header("Obstacles & Bots")]
        public List<Obstacle> obstacles = new List<Obstacle>();
        public List<BotCarController> botCars = new List<BotCarController>();

        [Header("Gameplay Rules")]
        public float questionTimeLimit = 20f;
        public int selectedDoorIndex = 1; // Start in middle-left lane (0..3)

        public GameState CurrentState { get; private set; } = GameState.Loading;

        private float _timeRemaining;
        private int _activeCheckpointIndex = 0;
        private bool _isTimerRunning = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            StartGame();
        }

        public void StartGame()
        {
            CurrentState = GameState.Loading;
            if (gameUI != null)
            {
                gameUI.HideExplanation();
                gameUI.HideResults();
                gameUI.SetHudVisible(false);
            }

            questionManager.Initialize(() =>
            {
                OnQuestionsLoaded();
            });
        }

        private void OnQuestionsLoaded()
        {
            _activeCheckpointIndex = 0;
            selectedDoorIndex = 1;

            // Reset car position
            if (carController != null)
            {
                carController.TeleportTo(new Vector3(-1.5f, 0f, 0f), 1);
            }

            // Setup the first checkpoint
            PrepareCurrentCheckpoint();

            if (gameUI != null)
            {
                gameUI.SetHudVisible(true);
                UpdateHudStats();
            }

            // Start driving to first checkpoint
            StartDrivingToActiveCheckpoint();
        }

        private void Update()
        {
            HandleInput();
            HandleTimer();
        }

        private void HandleInput()
        {
            bool qPressed = false;
            bool ePressed = false;
            bool spacePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.qKey.wasPressedThisFrame) qPressed = true;
                if (kb.eKey.wasPressedThisFrame) ePressed = true;
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) spacePressed = true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Q)) qPressed = true;
            if (Input.GetKeyDown(KeyCode.E)) ePressed = true;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) spacePressed = true;
#endif

            switch (CurrentState)
            {
                case GameState.WaitingForAnswer:
                    if (qPressed)
                    {
                        CycleDoor(-1);
                    }
                    else if (ePressed)
                    {
                        CycleDoor(1);
                    }
                    else if (spacePressed)
                    {
                        ConfirmSelection();
                    }
                    break;

                case GameState.ShowingExplanation:
                    if (spacePressed)
                    {
                        DismissExplanationAndRetry();
                    }
                    break;

                case GameState.ResultsScreen:
                    if (spacePressed)
                    {
                        RestartGame();
                    }
                    break;
            }
        }

        private void HandleTimer()
        {
            if (!_isTimerRunning || CurrentState != GameState.WaitingForAnswer)
                return;

            _timeRemaining -= Time.deltaTime;
            UpdateHudStats();

            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                _isTimerRunning = false;
                Debug.Log("[Gamify] Time ran out for this question!");
                OnTimeOut();
            }
        }

        private void CycleDoor(int direction)
        {
            // Cycle 0..3 with wrap-around
            selectedDoorIndex = (selectedDoorIndex + direction + 4) % 4;

            if (gameAudio != null) gameAudio.PlaySelect();

            Checkpoint cp = GetActiveCheckpoint();
            if (cp != null)
            {
                cp.HighlightDoor(selectedDoorIndex);
            }

            if (carController != null)
            {
                carController.SetSelectedLane(selectedDoorIndex);
            }

            UpdateSelectionPreview();
        }

        private void UpdateSelectionPreview()
        {
            QuestionItem q = questionManager.GetCurrentQuestion();
            if (q != null && gameUI != null)
            {
                string text = (selectedDoorIndex >= 0 && selectedDoorIndex < q.options.Length)
                    ? q.options[selectedDoorIndex]
                    : "";
                gameUI.UpdateSelectedDoorPreview(selectedDoorIndex, text);
            }
        }

        private void ConfirmSelection()
        {
            _isTimerRunning = false;
            CurrentState = GameState.RammingDoor;

            Checkpoint cp = GetActiveCheckpoint();
            if (cp == null) return;

            float targetDoorZ = cp.CheckpointZ - 0.2f;

            QuestionItem currentQ = questionManager.GetCurrentQuestion();
            bool isCorrect = (selectedDoorIndex == currentQ.correctIndex);

            carController.RamForward(targetDoorZ, () =>
            {
                OnCarReachedDoor(isCorrect, currentQ, cp);
            });
        }

        private void OnCarReachedDoor(bool isCorrect, QuestionItem q, Checkpoint cp)
        {
            DoorController chosenDoor = cp.GetDoor(selectedDoorIndex);

            if (isCorrect)
            {
                // Correct!
                Debug.Log($"[Gamify] Correct answer! Option: {selectedDoorIndex}");
                if (gameAudio != null) gameAudio.PlayCorrect();
                if (gameUI != null) gameUI.FlashScreen(new Color(1f, 1f, 1f, 0.4f), 0.25f);

                if (chosenDoor != null)
                {
                    chosenDoor.BreakWall();
                }

                questionManager.RecordCorrectAnswer(Mathf.CeilToInt(_timeRemaining));
                UpdateHudStats();

                StartCoroutine(CorrectSequenceRoutine());
            }
            else
            {
                // Wrong!
                Debug.Log($"[Gamify] Wrong answer! Option: {selectedDoorIndex}, Correct was: {q.correctIndex}");
                if (gameAudio != null) gameAudio.PlayWrong();
                if (gameUI != null) gameUI.FlashScreen(new Color(0.2f, 0.2f, 0.2f, 0.6f), 0.35f);

                if (chosenDoor != null)
                {
                    chosenDoor.FlashWallHit();
                }

                questionManager.RecordWrongAnswer();
                UpdateHudStats();

                // Recoil car back to waiting spot
                carController.Recoil(cp.WaitingZ, () =>
                {
                    if (questionManager.currentLives <= 0)
                    {
                        // Game Over (Out of lives)
                        ShowGameOver();
                    }
                    else
                    {
                        // Show explanation and let player retry the same question
                        CurrentState = GameState.ShowingExplanation;
                        if (gameUI != null)
                        {
                            gameUI.ShowExplanation(q.explanation);
                        }
                    }
                });
            }
        }

        private IEnumerator CorrectSequenceRoutine()
        {
            yield return new WaitForSeconds(0.45f);

            if (questionManager.HasMoreQuestions())
            {
                questionManager.AdvanceToNextQuestion();
                _activeCheckpointIndex++;
                PrepareCurrentCheckpoint();
                StartDrivingToActiveCheckpoint();
            }
            else
            {
                // All questions complete! Victory!
                ShowVictory();
            }
        }

        private void OnTimeOut()
        {
            QuestionItem q = questionManager.GetCurrentQuestion();
            Checkpoint cp = GetActiveCheckpoint();

            if (gameAudio != null) gameAudio.PlayWrong();
            if (gameUI != null) gameUI.FlashScreen(new Color(0.1f, 0.1f, 0.1f, 0.7f), 0.4f);

            questionManager.RecordWrongAnswer();
            UpdateHudStats();

            if (questionManager.currentLives <= 0)
            {
                ShowGameOver();
            }
            else
            {
                CurrentState = GameState.ShowingExplanation;
                if (gameUI != null)
                {
                    gameUI.ShowExplanation($"TIME OUT! {q.explanation}");
                }
            }
        }

        private void DismissExplanationAndRetry()
        {
            if (gameUI != null)
            {
                gameUI.HideExplanation();
            }

            // Reset door wall graphics if needed
            Checkpoint cp = GetActiveCheckpoint();
            if (cp != null)
            {
                cp.HighlightDoor(selectedDoorIndex);
            }

            _timeRemaining = questionTimeLimit;
            _isTimerRunning = true;
            CurrentState = GameState.WaitingForAnswer;
            UpdateSelectionPreview();
        }

        private void StartDrivingToActiveCheckpoint()
        {
            CurrentState = GameState.DrivingToCheckpoint;
            Checkpoint cp = GetActiveCheckpoint();
            if (cp == null) return;

            // Car drives forward to waiting spot in front of doors
            carController.DriveToCheckpoint(cp.WaitingZ);
            StartCoroutine(WatchForArrivalAtCheckpoint(cp));
        }

        private IEnumerator WatchForArrivalAtCheckpoint(Checkpoint cp)
        {
            while (carController.State == CarState.Cruising)
            {
                yield return null;
            }

            // Arrived at checkpoint
            CurrentState = GameState.WaitingForAnswer;
            _timeRemaining = questionTimeLimit;
            _isTimerRunning = true;

            selectedDoorIndex = carController.currentLaneIndex;
            cp.HighlightDoor(selectedDoorIndex);
            carController.SetSelectedLane(selectedDoorIndex);
            UpdateSelectionPreview();
        }

        private void PrepareCurrentCheckpoint()
        {
            QuestionItem q = questionManager.GetCurrentQuestion();
            if (q == null) return;

            Checkpoint cp = GetActiveCheckpoint();
            if (cp != null)
            {
                cp.SetupDoors(q);
                cp.HighlightDoor(selectedDoorIndex);
            }

            if (gameUI != null)
            {
                gameUI.UpdateQuestion(q, questionManager.currentQuestionIndex, questionManager.Questions.Count);
            }

            UpdateSelectionPreview();
        }

        private Checkpoint GetActiveCheckpoint()
        {
            if (checkpoints != null && _activeCheckpointIndex >= 0 && _activeCheckpointIndex < checkpoints.Count)
            {
                return checkpoints[_activeCheckpointIndex];
            }
            return null;
        }

        private void UpdateHudStats()
        {
            if (gameUI != null)
            {
                gameUI.UpdateStats(
                    questionManager.currentLives,
                    questionManager.initialLives,
                    questionManager.score,
                    Mathf.CeilToInt(_timeRemaining)
                );
            }
        }

        private void ShowGameOver()
        {
            CurrentState = GameState.ResultsScreen;
            _isTimerRunning = false;
            if (gameUI != null)
            {
                gameUI.HideExplanation();
                gameUI.ShowResults(
                    false,
                    questionManager.score,
                    questionManager.correctAnswersCount,
                    questionManager.Questions.Count,
                    questionManager.GetAccuracyPercentage()
                );
            }
        }

        private void ShowVictory()
        {
            CurrentState = GameState.ResultsScreen;
            _isTimerRunning = false;
            if (gameUI != null)
            {
                gameUI.HideExplanation();
                gameUI.ShowResults(
                    true,
                    questionManager.score,
                    questionManager.correctAnswersCount,
                    questionManager.Questions.Count,
                    questionManager.GetAccuracyPercentage()
                );
            }
        }

        public void RestartGame()
        {
            // Reset all checkpoints
            foreach (var cp in checkpoints)
            {
                if (cp != null) cp.ResetAllDoors();
            }

            // Reset all obstacles
            foreach (var obs in obstacles)
            {
                if (obs != null) obs.ResetObstacle();
            }

            // Reset all bot cars
            foreach (var bot in botCars)
            {
                if (bot != null) bot.ResetBot();
            }

            StartGame();
        }
    }
}
