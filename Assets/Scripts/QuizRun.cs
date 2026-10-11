using System;
using System.Collections;
using UnityEngine;

namespace Gamify
{
    public class QuizRun : MonoBehaviour
    {
        [SerializeField] private QuizApiClient api;
        [SerializeField] private QuizPoller poller;
        [SerializeField] private int startingLives = 3;

        public Quiz Quiz { get; private set; }
        public string SessionId { get; private set; }
        public int Lives { get; private set; }
        public int Xp { get; private set; }
        public int Cleared { get; private set; }
        public bool Busy { get; private set; }   // true while an answer is in flight

        private int missesOnCurrent;

        public event Action<int> GateCleared;          // question index
        public event Action<int> WrongAnswer;          // lives left
        public event Action RunOver;                   // out of lives or all cleared
        public event Action QuizExpired;               // server restarted -> back to menu
        public event Action<string> ConnectionProblem; // offer "retry"

        public void SetDependencies(QuizApiClient apiClient, QuizPoller quizPoller)
        {
            api = apiClient;
            poller = quizPoller;
        }

        private void Awake()
        {
            if (api == null) api = GetComponent<QuizApiClient>() ?? FindFirstObjectByType<QuizApiClient>();
            if (poller == null) poller = GetComponent<QuizPoller>() ?? FindFirstObjectByType<QuizPoller>();
        }

        public void Begin(Quiz quiz)
        {
            Quiz = quiz;
            SessionId = "unity-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            Lives = startingLives; Xp = 0; Cleared = 0; missesOnCurrent = 0;
            if (poller != null) poller.enabled = false; // never swap quizzes mid-run
        }

        public void End()
        {
            if (poller != null) poller.enabled = true;
            RunOver?.Invoke();
        }

        /// <summary>questionIndex: position in Quiz.questions. optionIndex: 0-3 in original option order.</summary>
        public void Answer(int questionIndex, int optionIndex, Action<AnswerResult> onResult)
        {
            if (Busy) return;
            StartCoroutine(AnswerRoutine(questionIndex, optionIndex, onResult));
        }

        private IEnumerator AnswerRoutine(int questionIndex, int optionIndex, Action<AnswerResult> onResult)
        {
            Busy = true;
            var q = Quiz.questions[questionIndex];
            var request = new AnswerRequest
            {
                quiz_id = Quiz.quiz_id, question_id = q.id, selected_index = optionIndex, session_id = SessionId,
            };

            ApiResponse<AnswerResult> response = null;
            yield return api.SubmitAnswer(request, r => response = r);
            Busy = false;

            if (response.NetworkError) { ConnectionProblem?.Invoke(response.Error); yield break; }
            if (response.StatusCode == 404 && response.Error != null && response.Error.StartsWith("Quiz not found"))
            {
                QuizExpired?.Invoke(); End(); yield break;
            }

            // 409 = already passed in this session -> treat as a pass
            var result = response.StatusCode == 409
                ? new AnswerResult { is_correct = true, explanation = "", obstacle_action = "pass" }
                : response.Data;
            if (result == null) { Debug.LogError($"[Gamify] answer failed: {response.StatusCode} {response.Error}"); yield break; }

            if (result.is_correct)
            {
                Xp += Mathf.Max(25, 100 - 35 * missesOnCurrent);
                Cleared += 1; missesOnCurrent = 0;
                GateCleared?.Invoke(questionIndex);
                onResult?.Invoke(result);
                if (Cleared >= Quiz.questions.Length) End();
            }
            else
            {
                missesOnCurrent += 1; Lives -= 1;
                WrongAnswer?.Invoke(Lives);
                onResult?.Invoke(result);
                if (Lives <= 0) End();
            }
        }
    }
}
