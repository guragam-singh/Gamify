using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Gamify.Racing
{
    public class QuestionManager : MonoBehaviour
    {
        [Header("Backend Configuration")]
        [Tooltip("Configurable URL for remote questions. Fallback will trigger if unreachable.")]
        public string backendUrl = "http://localhost:3000/api/questions/latest";
        public bool fetchFromBackendOnStart = false;
        public float requestTimeoutSeconds = 2.5f;

        [Header("Local Fallback")]
        public string localResourceName = "questions";

        [Header("Gameplay State")]
        public int initialLives = 3;
        public int currentLives = 3;
        public int score = 0;
        public int currentQuestionIndex = 0;
        public int correctAnswersCount = 0;
        public bool currentQuestionRetried = false;

        private List<QuestionItem> _questions = new List<QuestionItem>();
        public List<QuestionItem> Questions => _questions;

        public event Action<QuestionItem> OnQuestionChanged;
        public event Action<int, int> OnLivesChanged; // current, max
        public event Action<int> OnScoreChanged;

        public bool IsLoaded { get; private set; } = false;

        public void Initialize(Action onComplete = null)
        {
            StartCoroutine(LoadQuestionsRoutine(onComplete));
        }

        private IEnumerator LoadQuestionsRoutine(Action onComplete)
        {
            IsLoaded = false;
            _questions.Clear();

            if (fetchFromBackendOnStart && !string.IsNullOrEmpty(backendUrl))
            {
                Debug.Log($"[Gamify] Attempting to fetch questions from backend: {backendUrl}");
                using (UnityWebRequest req = UnityWebRequest.Get(backendUrl))
                {
                    req.timeout = Mathf.RoundToInt(requestTimeoutSeconds);
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
                    {
                        var parsed = QuestionParser.ParseJson(req.downloadHandler.text);
                        if (parsed != null && parsed.Count > 0)
                        {
                            _questions = parsed;
                            Debug.Log($"[Gamify] Successfully loaded {_questions.Count} questions from backend!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Gamify] Backend unavailable ({req.error}). Falling back to local questions.");
                    }
                }
            }

            // Fallback 1: Resources
            if (_questions.Count == 0)
            {
                TextAsset asset = Resources.Load<TextAsset>(localResourceName);
                if (asset != null && !string.IsNullOrEmpty(asset.text))
                {
                    _questions = QuestionParser.ParseJson(asset.text);
                    if (_questions.Count > 0)
                    {
                        Debug.Log($"[Gamify] Loaded {_questions.Count} questions from Resources/{localResourceName}.");
                    }
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

            // Fallback 3: Built-in hardcoded fallback so it's impossible to fail
            if (_questions.Count == 0)
            {
                Debug.LogWarning("[Gamify] Using built-in fallback questions.");
                _questions = GetBuiltinFallbackQuestions();
            }

            ResetGameStats();
            IsLoaded = true;
            onComplete?.Invoke();
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
                    question = "Which data structure operates on a Last-In, First-Out (LIFO) principle?",
                    options = new[] { "Queue", "Stack", "Binary Heap", "Hash Map" },
                    correctIndex = 1,
                    explanation = "A Stack uses LIFO: elements pushed on top are popped off first.",
                    topic = "Data Structures"
                },
                new QuestionItem
                {
                    question = "What is the worst-case time complexity of standard QuickSort?",
                    options = new[] { "O(1)", "O(log n)", "O(n log n)", "O(n^2)" },
                    correctIndex = 3,
                    explanation = "When the pivot chosen is consistently the smallest or largest, QuickSort degrades to O(n^2).",
                    topic = "Algorithms"
                },
                new QuestionItem
                {
                    question = "Which HTTP status code indicates a successful request?",
                    options = new[] { "200 OK", "301 Moved", "404 Not Found", "500 Server Error" },
                    correctIndex = 0,
                    explanation = "HTTP 200 OK is the standard response for successful HTTP requests.",
                    topic = "Networking"
                },
                new QuestionItem
                {
                    question = "Which C# keyword prevents a class from being inherited?",
                    options = new[] { "abstract", "sealed", "static", "readonly" },
                    correctIndex = 1,
                    explanation = "The 'sealed' modifier prevents other classes from deriving from that class.",
                    topic = "C# Programming"
                },
                new QuestionItem
                {
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
