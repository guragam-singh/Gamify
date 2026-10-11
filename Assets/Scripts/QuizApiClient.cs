using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Gamify
{
    public class QuizApiClient : MonoBehaviour
    {
        public const string BaseUrlPrefsKey = "gamify.baseUrl";

        [Tooltip("http://<backend-laptop-LAN-IP>:5050 -- overridden by PlayerPrefs if set in-game.")]
        [SerializeField] private string defaultBaseUrl = "http://127.0.0.1:5050";
        [SerializeField] private int timeoutSeconds = 10;

        private string GetEffectiveDefaultBaseUrl()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return "";
            }
            return defaultBaseUrl;
        }

        public string BaseUrl
        {
            get => PlayerPrefs.GetString(BaseUrlPrefsKey, GetEffectiveDefaultBaseUrl()).TrimEnd('/');
            set { PlayerPrefs.SetString(BaseUrlPrefsKey, value.Trim().TrimEnd('/')); PlayerPrefs.Save(); }
        }

        public IEnumerator GetHealth(Action<ApiResponse<HealthResult>> done) =>
            Send("GET", "/api/health", null, done);

        public IEnumerator GetLatestQuiz(Action<ApiResponse<Quiz>> done) =>
            Send("GET", "/api/questions/latest", null, done);

        public IEnumerator GetQuiz(string quizId, Action<ApiResponse<Quiz>> done) =>
            Send("GET", "/api/quizzes/" + UnityWebRequest.EscapeURL(quizId), null, done);

        public IEnumerator SubmitAnswer(AnswerRequest answer, Action<ApiResponse<AnswerResult>> done) =>
            Send("POST", "/api/answers", JsonUtility.ToJson(answer), done);

        private IEnumerator Send<T>(string method, string path, string jsonBody, Action<ApiResponse<T>> done)
        {
            using (var request = new UnityWebRequest(BaseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (jsonBody != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = timeoutSeconds;

                yield return request.SendWebRequest();

                var response = new ApiResponse<T> { StatusCode = request.responseCode };
                string text = request.downloadHandler?.text;

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    response.NetworkError = true;
                    response.Error = request.error;
                }
                else if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    response.Error = TryParseError(text) ?? request.error;
                }
                else
                {
                    try
                    {
                        response.Data = JsonUtility.FromJson<T>(text);
                        response.Ok = response.Data != null;
                        if (!response.Ok) response.Error = "Empty response";
                    }
                    catch (Exception e)
                    {
                        response.Error = "Bad JSON: " + e.Message;
                    }
                }
                done?.Invoke(response);
            }
        }

        private static string TryParseError(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try { return JsonUtility.FromJson<ApiErrorBody>(text)?.error; }
            catch { return null; }
        }
    }
}
