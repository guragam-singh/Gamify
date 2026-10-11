using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Gamify;

namespace Gamify.Racing
{
    public class QuestionManager : MonoBehaviour
    {
        [Header("Backend Configuration")]
        [Tooltip("Base URL for GAMIFY quiz backend (e.g. http://127.0.0.1:5050 or host LAN IP)")]
        public string backendBaseUrl = "http://127.0.0.1:5050";
        public bool fetchFromBackendOnStart = true;
        public float requestTimeoutSeconds = 5f;

        [Header("Backend Components")]
        public QuizApiClient apiClient;
        public QuizPoller poller;
        public QuizRun quizRun;

        [Header("Local Fallback")]
        public string localResourceName = "questions";

        [Header("Gameplay State")]
        public int initialLives = 3;
        public int currentLives = 3;
        public int score = 0;
        public int currentQuestionIndex = 0;
        public int correctAnswersCount = 0;
        public bool currentQuestionRetried = false;

        public Quiz CurrentQuiz { get; private set; }
        public string QuizCode => CurrentQuiz != null ? CurrentQuiz.ShortCode : "";
        public string QuizTitle => CurrentQuiz != null ? CurrentQuiz.title : "Racing Quiz";
        public string ServerStatus { get; private set; } = "Initializing...";
        public bool IsLiveQuiz => CurrentQuiz != null;

        private List<QuestionItem> _questions = new List<QuestionItem>();
        public List<QuestionItem> Questions => _questions;

        public event Action<QuestionItem> OnQuestionChanged;
        public event Action<int, int> OnLivesChanged; // current, max
        public event Action<int> OnScoreChanged;
        public event Action<string> OnServerStatusChanged;
        public event Action<Quiz> OnQuizLoaded;

        public bool IsLoaded { get; private set; } = false;
        private bool _isRunInProgress = false;
        private bool _isFallbackActive = false;
        public bool IsFallbackActive => _isFallbackActive;

        private void Awake()
        {
            EnsureBackendComponents();
        }

        private void EnsureBackendComponents()
        {
            if (apiClient == null)
            {
                apiClient = GetComponent<QuizApiClient>() ?? gameObject.AddComponent<QuizApiClient>();
            }

            if (poller == null)
            {
                poller = GetComponent<QuizPoller>() ?? gameObject.AddComponent<QuizPoller>();
            }

            if (quizRun == null)
            {
                quizRun = GetComponent<QuizRun>() ?? gameObject.AddComponent<QuizRun>();
            }

            fetchFromBackendOnStart = true;
            if (!string.IsNullOrEmpty(backendBaseUrl))
            {
                apiClient.BaseUrl = backendBaseUrl;
            }

            poller.SetApiClient(apiClient);
            quizRun.SetDependencies(apiClient, poller);

            poller.NewQuizAvailable += HandleNewQuizAvailable;
            poller.WaitingForUpload += HandleWaitingForUpload;
            poller.ServerUnreachable += HandleServerUnreachable;
        }

        private void OnDestroy()
        {
            if (poller != null)
            {
                poller.NewQuizAvailable -= HandleNewQuizAvailable;
                poller.WaitingForUpload -= HandleWaitingForUpload;
                poller.ServerUnreachable -= HandleServerUnreachable;
            }
        }

        private void HandleNewQuizAvailable(Quiz quiz)
        {
            if (quiz == null || string.IsNullOrEmpty(quiz.quiz_id)) return;
            if (CurrentQuiz != null && CurrentQuiz.quiz_id == quiz.quiz_id) return;

            // If an actual backend quiz is currently being played, do not interrupt mid-run per spec 4.2
            if (_isRunInProgress && !_isFallbackActive && CurrentQuiz != null && !string.IsNullOrEmpty(CurrentQuiz.quiz_id))
            {
                Debug.Log($"[Gamify] Active run already using quiz {CurrentQuiz.quiz_id}; deferring new quiz {quiz.quiz_id}.");
                return;
            }

            Debug.Log($"[Gamify] New quiz available from backend: {quiz.title} (Code: {quiz.ShortCode}, Questions: {quiz.question_count})");
            LoadQuizData(quiz);
            bool wasFallback = _isFallbackActive;
            _isFallbackActive = false;
            ServerStatus = $"Quiz Ready: [{quiz.ShortCode}] {quiz.title}";
            OnServerStatusChanged?.Invoke(ServerStatus);
            OnQuizLoaded?.Invoke(quiz);

            // If we were idling or running fallback questions, safely restart game with the real quiz
            if (GameManager.Instance != null && (wasFallback || CurrentQuiz == null || _isRunInProgress))
            {
                GameManager.Instance.RestartGame();
            }
        }

        private void HandleWaitingForUpload()
        {
            ServerStatus = "Waiting for notes... Upload on GAMIFY website";
            OnServerStatusChanged?.Invoke(ServerStatus);
        }

        private void HandleServerUnreachable(string error)
        {
            ServerStatus = $"Can't reach quiz server at {apiClient.BaseUrl}";
            OnServerStatusChanged?.Invoke(ServerStatus);
        }

        public void Initialize(Action onComplete = null)
        {
            EnsureBackendComponents();
            StartCoroutine(LoadQuestionsRoutine(onComplete));
        }

        private string GetTargetQuizIdFromUrl()
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return null;
            int idx = url.IndexOf("quiz=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            string id = url.Substring(idx + 5);
            int ampersand = id.IndexOf('&');
            if (ampersand >= 0) id = id.Substring(0, ampersand);
            return id.Trim();
        }

        private IEnumerator LoadQuestionsRoutine(Action onComplete)
        {
            IsLoaded = false;
            _questions.Clear();
            CurrentQuiz = null;
            _isFallbackActive = false;

            if (apiClient != null)
            {
                ServerStatus = "Checking quiz server...";
                OnServerStatusChanged?.Invoke(ServerStatus);

                string targetQuizId = GetTargetQuizIdFromUrl();
                bool responded = false;
                Action<ApiResponse<Quiz>> onResponse = r =>
                {
                    responded = true;
                    if (r.Ok && r.Data != null && r.Data.questions != null && r.Data.questions.Length > 0)
                    {
                        LoadQuizData(r.Data);
                        _isFallbackActive = false;
                        ServerStatus = $"Connected: [{r.Data.ShortCode}] {r.Data.title}";
                        Debug.Log($"[Gamify] Loaded {r.Data.questions.Length} questions from backend! Code: {r.Data.ShortCode}");
                    }
                    else if (r.StatusCode == 404)
                    {
                        ServerStatus = "Waiting for notes... Upload on GAMIFY website";
                        Debug.Log("[Gamify] No active quiz on server. Poller will wait for student upload.");
                    }
                    else
                    {
                        ServerStatus = $"Server offline: {r.Error}";
                        Debug.LogWarning($"[Gamify] Backend unavailable: {r.Error}");
                    }
                };

                if (!string.IsNullOrEmpty(targetQuizId))
                {
                    if (poller != null) poller.targetQuizId = targetQuizId;
                    int attempts = 0;
                    const int maxAttempts = 5;
                    while (attempts < maxAttempts && _questions.Count == 0)
                    {
                        attempts++;
                        ServerStatus = attempts == 1 ? "Loading quiz..." : $"Loading quiz (attempt {attempts}/{maxAttempts})...";
                        OnServerStatusChanged?.Invoke(ServerStatus);

                        yield return apiClient.GetQuiz(targetQuizId, onResponse);
                        if (_questions.Count > 0) break;

                        if (attempts < maxAttempts)
                        {
                            yield return new WaitForSeconds(1f);
                        }
                    }
                }
                else
                {
                    yield return apiClient.GetLatestQuiz(onResponse);
                }

                if (!responded)
                {
                    ServerStatus = "Connection timed out.";
                }
                OnServerStatusChanged?.Invoke(ServerStatus);
            }

            // Fallback: only used if backend had no questions
            if (_questions.Count == 0)
            {
                _isFallbackActive = true;
                // Fallback 1: Local Resources
                TextAsset asset = Resources.Load<TextAsset>(localResourceName);
                if (asset != null && !string.IsNullOrEmpty(asset.text))
                {
                    _questions = QuestionParser.ParseJson(asset.text);
                    if (_questions.Count > 0)
                    {
                        Debug.Log($"[Gamify] Loaded {_questions.Count} fallback questions from Resources/{localResourceName}.");
                    }
                }

                // Fallback 2: StreamingAssets
                if (_questions.Count == 0)
                {
                    string streamingPath = Path.Combine(Application.streamingAssetsPath, "questions.json");
                    if (File.Exists(streamingPath))
                    {
                        string json = File.ReadAllText(streamingPath);
                        _questions = QuestionParser.ParseJson(json);
                        if (_questions.Count > 0)
                        {
                            Debug.Log($"[Gamify] Loaded {_questions.Count} questions from StreamingAssets.");
                        }
                    }
                }

                // Fallback 3: Built-in hardcoded fallback so gameplay never fails
                if (_questions.Count == 0)
                {
                    Debug.LogWarning("[Gamify] Using built-in fallback questions.");
                    _questions = GetBuiltinFallbackQuestions();
                }
            }

            ResetGameStats();
            IsLoaded = true;
            onComplete?.Invoke();
        }

        private void LoadQuizData(Quiz quiz)
        {
            CurrentQuiz = quiz;
            _questions.Clear();
            if (quiz != null && quiz.questions != null)
            {
                foreach (var q in quiz.questions)
                {
                    _questions.Add(QuestionItem.FromQuizQuestion(q));
                }
            }
        }

        public void StartRun()
        {
            _isRunInProgress = true;
            if (CurrentQuiz != null && quizRun != null)
            {
                quizRun.Begin(CurrentQuiz);
            }
            if (poller != null)
            {
                // Only disable poller if playing a real backend quiz; if running fallback, keep poller active to receive uploaded quiz
                poller.enabled = _isFallbackActive;
            }
            ResetGameStats();
        }

        public void EndRun()
        {
            _isRunInProgress = false;
            if (quizRun != null)
            {
                quizRun.End();
            }
            if (poller != null)
            {
                poller.enabled = true;
            }
        }

        public void SubmitAnswer(int questionIndex, int optionIndex, Action<AnswerResult, string> callback)
        {
            if (questionIndex != currentQuestionIndex || optionIndex < 0 || optionIndex >= 4)
            {
                callback?.Invoke(null, "Invalid question or answer selection");
                return;
            }

            // If connected to live backend quiz, submit via QuizRun / API
            if (CurrentQuiz != null && quizRun != null)
            {
                quizRun.Answer(questionIndex, optionIndex, result =>
                {
                    callback?.Invoke(result, null);
                });
                return;
            }

            // Otherwise evaluate locally for offline fallback
            QuestionItem q = GetCurrentQuestion();
            if (q != null && q.correctIndex >= 0 && q.correctIndex < q.options.Length)
            {
                bool isCorrect = (optionIndex == q.correctIndex);
                var result = new AnswerResult
                {
                    is_correct = isCorrect,
                    explanation = isCorrect ? q.explanation : "",
                    obstacle_action = isCorrect ? "pass" : "retry"
                };

                // GameManager records score/count/lives once after the ram resolves.
                callback?.Invoke(result, null);
            }
            else
            {
                callback?.Invoke(null, "No active question or missing local answer key");
            }
        }

        public void ResetGameStats()
        {
            currentLives = initialLives;
            score = 0;
            currentQuestionIndex = 0;
            correctAnswersCount = 0;
            currentQuestionRetried = false;

            OnLivesChanged?.Invoke(currentLives, initialLives);
            OnScoreChanged?.Invoke(score);
        }

        public QuestionItem GetCurrentQuestion()
        {
            if (_questions == null || _questions.Count == 0) return null;
            if (currentQuestionIndex >= 0 && currentQuestionIndex < _questions.Count)
            {
                return _questions[currentQuestionIndex];
            }
            return null;
        }

        public bool HasMoreQuestions()
        {
            return currentQuestionIndex < _questions.Count - 1;
        }

        public void AdvanceToNextQuestion()
        {
            currentQuestionIndex++;
            currentQuestionRetried = false;
            if (currentQuestionIndex < _questions.Count)
            {
                OnQuestionChanged?.Invoke(GetCurrentQuestion());
            }
        }

        public void RecordCorrectAnswer(int remainingSeconds)
        {
            int points = 100 + Mathf.Max(0, remainingSeconds * 10);
            score += points;
            correctAnswersCount++;
            OnScoreChanged?.Invoke(score);
        }

        public void RecordWrongAnswer()
        {
            currentQuestionRetried = true;
            LoseLife();
        }

        public void LoseLife()
        {
            currentLives = Mathf.Max(0, currentLives - 1);
            OnLivesChanged?.Invoke(currentLives, initialLives);
        }

        public float GetAccuracyPercentage()
        {
            if (_questions.Count == 0) return 0f;
            return ((float)correctAnswersCount / _questions.Count) * 100f;
        }

        private List<QuestionItem> GetBuiltinFallbackQuestions()
        {
            return new List<QuestionItem>
            {
                new QuestionItem
                {
                    id = "local-1",
                    question = "Which data structure operates on a Last-In, First-Out (LIFO) principle?",
                    options = new[] { "Queue", "Stack", "Binary Heap", "Hash Map" },
                    correctIndex = 1,
                    explanation = "A Stack uses LIFO: elements pushed on top are popped off first.",
                    topic = "Data Structures"
                },
                new QuestionItem
                {
                    id = "local-2",
                    question = "What is the worst-case time complexity of standard QuickSort?",
                    options = new[] { "O(1)", "O(log n)", "O(n log n)", "O(n^2)" },
                    correctIndex = 3,
                    explanation = "When the pivot chosen is consistently the smallest or largest, QuickSort degrades to O(n^2).",
                    topic = "Algorithms"
                },
                new QuestionItem
                {
                    id = "local-3",
                    question = "Which HTTP status code indicates a successful request?",
                    options = new[] { "200 OK", "301 Moved", "404 Not Found", "500 Server Error" },
                    correctIndex = 0,
                    explanation = "HTTP 200 OK is the standard response for successful HTTP requests.",
                    topic = "Networking"
                },
                new QuestionItem
                {
                    id = "local-4",
                    question = "Which C# keyword prevents a class from being inherited?",
                    options = new[] { "abstract", "sealed", "static", "readonly" },
                    correctIndex = 1,
                    explanation = "The 'sealed' modifier prevents other classes from deriving from that class.",
                    topic = "C# Programming"
                },
                new QuestionItem
                {
                    id = "local-5",
                    question = "In Unity, which method is called once per physics update frame?",
                    options = new[] { "Update()", "LateUpdate()", "FixedUpdate()", "Awake()" },
                    correctIndex = 2,
                    explanation = "FixedUpdate() runs at a consistent framerate tied to Unity's physics engine interval.",
                    topic = "Unity Engine"
                }
            };
        }
    }
}
