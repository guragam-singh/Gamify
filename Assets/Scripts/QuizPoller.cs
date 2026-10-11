using System;
using System.Collections;
using UnityEngine;

namespace Gamify
{
    /// <summary>Polls /api/questions/latest while enabled. Disable it during a run.</summary>
    public class QuizPoller : MonoBehaviour
    {
        [SerializeField] private QuizApiClient api;
        [SerializeField] private float intervalSeconds = 3f;

        [NonSerialized] public string targetQuizId = null;

        public Quiz Current { get; private set; }
        public event Action<Quiz> NewQuizAvailable;   // fires when quiz_id changes
        public event Action WaitingForUpload;          // 404: nothing generated yet
        public event Action<string> ServerUnreachable; // network error text

        private Coroutine loop;

        public void SetApiClient(QuizApiClient client)
        {
            api = client;
        }

        private void Awake()
        {
            if (api == null) api = GetComponent<QuizApiClient>() ?? FindFirstObjectByType<QuizApiClient>();
        }

        private void OnEnable() => loop = StartCoroutine(Loop());
        private void OnDisable() { if (loop != null) StopCoroutine(loop); }

        private IEnumerator Loop()
        {
            var wait = new WaitForSecondsRealtime(intervalSeconds);
            while (true)
            {
                if (api != null)
                {
                    Action<ApiResponse<Quiz>> onResponse = r =>
                    {
                        if (r.Ok && r.Data != null)
                        {
                            if (Current == null || Current.quiz_id != r.Data.quiz_id)
                            {
                                Current = r.Data;
                                NewQuizAvailable?.Invoke(Current);
                            }
                        }
                        else if (r.StatusCode == 404) WaitingForUpload?.Invoke();
                        else if (r.NetworkError) ServerUnreachable?.Invoke(r.Error);
                        else Debug.LogWarning($"[Gamify] poller failed: {r.StatusCode} {r.Error}");
                    };

                    if (!string.IsNullOrEmpty(targetQuizId))
                    {
                        yield return api.GetQuiz(targetQuizId, onResponse);
                    }
                    else
                    {
                        yield return api.GetLatestQuiz(onResponse);
                    }
                }
                yield return wait;
            }
        }
    }
}
