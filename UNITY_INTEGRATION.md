# GAMIFY × Unity — Integration Spec

**Audience:** the developer (or AI agent) wiring the GAMIFY quiz backend into the
Unity 3D game. This document is self-contained: you do not need the backend
source to implement the Unity side.

**Goal:** a student uploads study notes on the GAMIFY website. The backend turns
them into multiple-choice questions. The Unity game picks up that quiz
automatically and uses it in gameplay — each question is an obstacle/gate the
player must answer correctly to get past.

---

## 1. How the pieces fit

```
 ┌──────────────────────┐   1. upload notes    ┌───────────────────────────┐
 │  GAMIFY website       │ ──────────────────▶ │  Quiz backend (Flask)      │
 │  (any browser)        │   POST /api/        │  host laptop, port 5050    │
 │                       │   generate-questions│  - reads PDF/TXT/photos    │
 │  shows "Quiz code     │ ◀────────────────── │  - Gemini → questions      │
 │   AECE04"             │   quiz + quiz_id    │  - keeps answer keys       │
 └──────────────────────┘                      └─────────────▲─────────────┘
                                                             │
                     2. poll GET /api/questions/latest       │
                     3. POST /api/answers per attempt        │
                                                ┌────────────┴─────────────┐
                                                │  Unity game               │
                                                │  (same or other laptop)   │
                                                └──────────────────────────┘
```

- Unity **never uploads documents** and never calls `/api/generate-questions`.
- Unity **polls** `GET /api/questions/latest` to find the newest quiz.
- Unity **never sees the correct answer**. It sends the player's choice to
  `POST /api/answers` and the server says pass or retry. This keeps the answer
  key off the client.
- The website shows a 6-character **quiz code** (first 6 characters of
  `quiz_id`, upper-cased). Unity should show the same code so the student can
  confirm the game loaded *their* quiz.

### Who owns what

| Concern | Owner |
| --- | --- |
| Reading documents, OCR, generating questions | Backend |
| Correct answers, checking answers, explanations | Backend |
| Which quiz is "latest" | Backend |
| Lives, score/XP, timers, level layout, how a gate looks | **Unity** |
| Retrying the same question after a miss | **Unity** (server just says `retry`) |
| Choosing when to load a new quiz (never mid-run) | **Unity** |

---

## 2. Network setup (do this first)

The backend runs on the "host" laptop. Unity can run on the same laptop or on
another machine on the **same network**.

### 2.1 Start the backend (host laptop)

```bash
cd ai-quiz-backend
# one-time: python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
# put GEMINI_API_KEY=... in ai-quiz-backend/.env
HOST=0.0.0.0 PORT=5050 .venv/bin/python main.py
```

- `HOST=0.0.0.0` makes it reachable from other machines (this is the default).
- Port **5050** is used because macOS reserves 5000 for AirPlay. On Windows,
  5000 also works; just keep both sides in agreement.

### 2.2 Find the host's LAN IP

- macOS: `ipconfig getifaddr en0` (Wi-Fi) → e.g. `10.21.98.61`
- Windows: `ipconfig` → "IPv4 Address" of the Wi-Fi adapter

The Unity **base URL** is then `http://<that-ip>:5050`, e.g.
`http://10.21.98.61:5050`. When Unity runs on the host itself,
`http://127.0.0.1:5050` also works.

> Do **not** use `localhost` on the Unity laptop if the backend is on a
> different laptop — `localhost` means "this machine".

### 2.3 Firewall

- macOS: on first start, allow "Python" to accept incoming connections
  (System Settings → Network → Firewall → Options).
- Windows: allow Python on **Private** networks when prompted, or add an
  inbound rule for TCP 5050.

### 2.4 Verify from the Unity laptop before writing any code

Open `http://<host-ip>:5050/api/health` in a browser on the Unity laptop. You
should see `{"status":"ok", ...}`. If it times out: same Wi-Fi? firewall?
Many school/guest Wi-Fi networks block device-to-device traffic ("client
isolation") — use a phone hotspot or a home router instead.

### 2.5 Unity project settings (required — easy to miss)

The backend speaks plain **HTTP**, not HTTPS. Unity 2022.1+ blocks HTTP by
default and fails with *"Insecure connection not allowed"*.

- **Edit → Project Settings → Player → Other Settings → Allow downloads over
  HTTP\* → `Always allowed`.**
- This one setting also covers Android cleartext traffic and iOS ATS.
- **WebGL builds:** the backend allows any origin (CORS `*`), but a WebGL build
  served over **https** cannot call an **http** API (mixed content). Serve the
  WebGL build over http on the LAN, or run it in the Editor/desktop build.

---

## 3. API contract

Base URL: `http://<host-ip>:5050`. All bodies are JSON (UTF-8). All error
responses are `{"error": "human readable message"}` with a 4xx status.

### 3.1 `GET /api/health`

Use on startup and in a settings screen to show "Connected".

```json
{
  "status": "ok",
  "ai_configured": true,
  "ocr_available": true,
  "accepted_extensions": [".bmp", ".jpeg", ".jpg", ".md", ".pdf", ".png", ".tif", ".tiff", ".txt", ".webp"],
  "max_file_bytes": 10485760,
  "max_files": 5
}
```

Only `status` matters to Unity. Treat any other fields as informational.

### 3.2 `GET /api/questions/latest` — the main call

Returns the most recently generated quiz.

**200 OK**

```json
{
  "quiz_id": "aece04c38f8b4705a2a95271323c5626",
  "created_at": "2026-10-10T21:08:58+00:00",
  "title": "photosynthesis",
  "question_count": 5,
  "source": "ai",
  "message": "optional, only present when source is not \"ai\"",
  "documents": [
    { "name": "photosynthesis.pdf", "kind": "pdf", "pages": 3, "ocr_pages": 0, "characters": 4812 }
  ],
  "questions": [
    {
      "id": "q1",
      "question": "What pigment absorbs light energy in chloroplasts?",
      "options": ["Chlorophyll", "Glucose", "Stomata", "Starch"],
      "topic": "Photosynthesis"
    }
  ]
}
```

**404** `{"error": "No active quiz exists. Generate questions first."}` —
nobody has uploaded anything since the server started. Show a "Waiting for
notes…" screen and keep polling.

Field guarantees:

| Field | Guarantee |
| --- | --- |
| `quiz_id` | 32-char hex string. Changes every time a new quiz is generated. |
| `questions` | 1–10 items, **usually 5**. Never assume exactly 5. |
| `questions[].id` | Unique within the quiz (`"q1"`, `"q2"`, … or `"fallback-1"`, …). Treat as opaque. |
| `questions[].options` | **Always exactly 4** distinct non-empty strings. |
| `questions[].question` | Plain text, up to ~300 characters. Offline questions look like `"Fill in the blank: … _____ …"`. Your UI must word-wrap. |
| `questions[].topic` | Short label, may be an empty string. |
| `source` | `"ai"` (Gemini), `"offline"` (built from sentences in the notes because Gemini was unavailable), or `"fallback"` (generic demo questions — notes were unusable). |
| `documents` | Absent when the quiz came from typed text instead of files. |
| `message`, `documents` | Optional — use `null`-safe access. |

The response **never** contains the correct answer.

### 3.3 `GET /api/quizzes/{quiz_id}`

Same shape as 3.2 for a specific quiz. Use it to re-load the quiz the player is
in the middle of (e.g. after a scene reload). **404** if the server restarted or
the quiz aged out (the server keeps the 20 most recent quizzes in memory).

### 3.4 `POST /api/answers` — check one attempt

Request:

```json
{
  "quiz_id": "aece04c38f8b4705a2a95271323c5626",
  "question_id": "q1",
  "selected_index": 2,
  "session_id": "unity-3f9c2a1b7d4e"
}
```

| Field | Rules |
| --- | --- |
| `quiz_id` | From the quiz you loaded. |
| `question_id` | `questions[].id`. |
| `selected_index` | Integer **0–3** — index into that question's `options` as received. If you shuffle options in Unity, map back to the original index. |
| `session_id` | **Send one.** 1–64 chars from `A-Z a-z 0-9 - _ . :`. Generate a new one for every play-through (see §4.3). Optional on the wire (defaults to `"default"`), but without it, two clients playing the same quiz block each other. |

Responses:

| Status | Body | Meaning / what Unity does |
| --- | --- | --- |
| 200 | `{"is_correct": true, "explanation": "Chlorophyll absorbs…", "obstacle_action": "pass"}` | Open the gate. Optionally show `explanation`. |
| 200 | `{"is_correct": false, "explanation": "", "obstacle_action": "retry"}` | Wrong. Apply your penalty (lose a life, knock-back…). **Keep the same question**; let the player choose again. The explanation is deliberately empty so it doesn't reveal the answer. |
| 409 | `{"error": "…already been answered correctly in this session."}` | Already passed in this session (e.g. a double-submit). **Treat as pass.** |
| 404 | `{"error": "Quiz not found or no longer active."}` | Server restarted or quiz aged out. End the run gracefully and return to the menu to load the latest quiz. |
| 404 | `{"error": "Question not found in this quiz."}` | Bug on the Unity side (wrong id). Log it. |
| 400 | `{"error": "…"}` | Malformed request (bad index/session id). Log it. |

`obstacle_action` is always `"pass"` when `is_correct` is true and `"retry"`
otherwise — use whichever is more convenient.

### 3.5 Endpoints Unity must NOT call

- `POST /api/generate-questions` — the website does this.

---

## 4. Required Unity behaviour

### 4.1 Startup / menu

1. Read the base URL (inspector default, overridable from an in-game settings
   field saved to `PlayerPrefs` so changing laptops doesn't need a rebuild).
2. `GET /api/health`. On failure show *"Can't reach the quiz server at
   {baseUrl}"* with a Retry button and the settings field.
3. Start polling `GET /api/questions/latest` **every 3 seconds** while on the
   menu/lobby screen.
   - 404 → show *"Waiting for notes… upload them on the GAMIFY website."*
   - 200 → show title, `question_count`, and the **quiz code**
     (`quiz_id[..6].ToUpper()`), and enable **Play**.
   - A different `quiz_id` than the one shown → swap to the new quiz (this is
     how "the student uploaded new notes" reaches the game).

### 4.2 During a run

- **Stop polling** (or ignore new quizzes) while a run is in progress. Never
  swap questions under the player. Optionally show a small "New quiz ready —
  it'll load after this run" banner.
- Map questions to gates/obstacles in order (`questions[0]` → first gate…).
  Build the level/gate count from `questions.Length`, not a hard-coded 5.
- When the player reaches a gate: freeze/slow gameplay, show question + 4
  options (keyboard/gamepad navigable), submit with `POST /api/answers`.
- Disable the option buttons while a request is in flight (prevents
  double-submits). Re-enable the remaining ones after a `retry`.
- Lives, score and fail conditions are Unity's decision. Suggested defaults
  that match the website: 3 lives per run; XP per gate = `max(25, 100 − 35 ×
  misses on that gate)`.
- If a request fails with a **network error/timeout**, show *"Connection lost —
  retry?"* and resend the same answer. Do not count it as a wrong answer.

### 4.3 Session id rules

- New `session_id` per **run**: `"unity-" + Guid.NewGuid().ToString("N").Substring(0, 12)`.
- Reuse it for every answer in that run.
- Replaying the same quiz = new run = new session id (otherwise previously
  passed questions return 409).

### 4.4 End of run

Show gates cleared / total and XP. Return to the menu, which resumes polling.

---

## 5. Reference implementation (C#)

Uses only built-in Unity APIs (`UnityWebRequest`, `JsonUtility`) — no packages.
Tested patterns for Unity 2021.3 LTS and newer. Field names are snake_case on
purpose: `JsonUtility` maps JSON keys to field names exactly.

### 5.1 `QuizModels.cs`

```csharp
using System;

namespace Gamify
{
    [Serializable]
    public class QuizQuestion
    {
        public string id;
        public string question;
        public string[] options;   // always 4
        public string topic;
    }

    [Serializable]
    public class QuizDocument
    {
        public string name;
        public string kind;        // "pdf" | "text" | "image"
        public int pages;
        public int ocr_pages;
        public int characters;
    }

    [Serializable]
    public class Quiz
    {
        public string quiz_id;
        public string created_at;
        public string title;
        public int question_count;
        public string source;      // "ai" | "offline" | "fallback"
        public string message;     // may be null/empty
        public QuizQuestion[] questions;
        public QuizDocument[] documents; // may be null/empty

        public string ShortCode =>
            string.IsNullOrEmpty(quiz_id) ? "" : quiz_id.Substring(0, Math.Min(6, quiz_id.Length)).ToUpperInvariant();
    }

    [Serializable]
    public class AnswerRequest
    {
        public string quiz_id;
        public string question_id;
        public int selected_index;
        public string session_id;
    }

    [Serializable]
    public class AnswerResult
    {
        public bool is_correct;
        public string explanation;
        public string obstacle_action; // "pass" | "retry"
    }

    [Serializable]
    public class HealthResult
    {
        public string status;
        public bool ai_configured;
        public bool ocr_available;
    }

    [Serializable]
    public class ApiErrorBody
    {
        public string error;
    }

    /// <summary>Result wrapper so callers can branch on status codes without exceptions.</summary>
    public class ApiResponse<T>
    {
        public bool Ok;            // true only for HTTP 2xx with a parsed body
        public long StatusCode;    // 0 when the network failed before an HTTP response
        public bool NetworkError;  // timeout, refused, DNS, insecure-HTTP blocked…
        public string Error;       // server "error" message or network error text
        public T Data;
    }
}
```

### 5.2 `QuizApiClient.cs`

```csharp
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

        [Tooltip("http://<backend-laptop-LAN-IP>:5050 — overridden by PlayerPrefs if set in-game.")]
        [SerializeField] private string defaultBaseUrl = "http://127.0.0.1:5050";
        [SerializeField] private int timeoutSeconds = 10;

        public string BaseUrl
        {
            get => PlayerPrefs.GetString(BaseUrlPrefsKey, defaultBaseUrl).TrimEnd('/');
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
                    response.Error = request.error; // e.g. "Insecure connection not allowed" → see Player Settings
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
```

### 5.3 `QuizPoller.cs` — detects new uploads

```csharp
using System;
using System.Collections;
using UnityEngine;

namespace Gamify
{
    /// Polls /api/questions/latest while enabled. Disable it during a run.
    public class QuizPoller : MonoBehaviour
    {
        [SerializeField] private QuizApiClient api;
        [SerializeField] private float intervalSeconds = 3f;

        public Quiz Current { get; private set; }
        public event Action<Quiz> NewQuizAvailable;   // fires when quiz_id changes
        public event Action WaitingForUpload;          // 404: nothing generated yet
        public event Action<string> ServerUnreachable; // network error text

        private Coroutine loop;

        private void OnEnable() => loop = StartCoroutine(Loop());
        private void OnDisable() { if (loop != null) StopCoroutine(loop); }

        private IEnumerator Loop()
        {
            var wait = new WaitForSecondsRealtime(intervalSeconds);
            while (true)
            {
                yield return api.GetLatestQuiz(r =>
                {
                    if (r.Ok)
                    {
                        if (Current == null || Current.quiz_id != r.Data.quiz_id)
                        {
                            Current = r.Data;
                            NewQuizAvailable?.Invoke(Current);
                        }
                    }
                    else if (r.StatusCode == 404) WaitingForUpload?.Invoke();
                    else if (r.NetworkError) ServerUnreachable?.Invoke(r.Error);
                    else Debug.LogWarning($"[Gamify] latest quiz failed: {r.StatusCode} {r.Error}");
                });
                yield return wait;
            }
        }
    }
}
```

### 5.4 `QuizRun.cs` — one play-through

Glue between the API and your gameplay. Your gate/obstacle script calls
`Answer(...)` when the player picks an option.

```csharp
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
        public event Action QuizExpired;               // server restarted → back to menu
        public event Action<string> ConnectionProblem; // offer "retry"

        public void Begin(Quiz quiz)
        {
            Quiz = quiz;
            SessionId = "unity-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            Lives = startingLives; Xp = 0; Cleared = 0; missesOnCurrent = 0;
            poller.enabled = false; // never swap quizzes mid-run
        }

        public void End()
        {
            poller.enabled = true;
            RunOver?.Invoke();
        }

        /// questionIndex: position in Quiz.questions. optionIndex: 0–3 in the ORIGINAL option order.
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

            // 409 = already passed in this session → treat as a pass.
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
```

### 5.5 Wiring checklist

1. Empty GameObject **GamifyServices** with `QuizApiClient`, `QuizPoller`,
   `QuizRun`; assign references in the inspector. Mark it
   `DontDestroyOnLoad` if menu and level are separate scenes.
2. Menu UI subscribes to `QuizPoller.NewQuizAvailable / WaitingForUpload /
   ServerUnreachable` and shows title, question count, and `Quiz.ShortCode`.
3. **Play** → `QuizRun.Begin(poller.Current)` → load the level; spawn one gate
   per `Quiz.questions` entry.
4. Gate trigger → show question panel → option click →
   `QuizRun.Answer(gateIndex, optionIndex, result => …)`; on `pass` open the
   gate, on `retry` keep the panel open with that option disabled.
5. `RunOver` → results screen → back to menu (`QuizRun.End()` already
   re-enabled polling).
6. Settings screen: text field bound to `QuizApiClient.BaseUrl` + "Test
   connection" button calling `GetHealth`.

---

## 6. Testing without the website

Generate a quiz straight from a terminal (any machine that can reach the host):

```bash
# from typed text
curl -X POST http://<host-ip>:5050/api/generate-questions \
  -H "Content-Type: application/json" \
  -d '{"material":"Photosynthesis is the process by which green plants use sunlight to make food. It takes place in the leaves inside chloroplasts. Chloroplasts contain a green pigment called chlorophyll which absorbs light energy. Plants take in carbon dioxide through pores called stomata. The plant converts carbon dioxide and water into glucose and oxygen."}'

# from a file (PDF/TXT/photo)
curl -X POST http://<host-ip>:5050/api/generate-questions -F "file=@notes.pdf"

# what Unity will see
curl http://<host-ip>:5050/api/questions/latest

# simulate an answer
curl -X POST http://<host-ip>:5050/api/answers -H "Content-Type: application/json" \
  -d '{"quiz_id":"<id>","question_id":"q1","selected_index":0,"session_id":"unity-test"}'
```

For fully offline Unity work, save a §3.2 example payload to
`Assets/Resources/sample_quiz.json` and load it with
`JsonUtility.FromJson<Quiz>(Resources.Load<TextAsset>("sample_quiz").text)`.
(Answers still need the server.)

---

## 7. Acceptance criteria

- [ ] With the backend down, the menu shows a clear "can't reach server" state
      and recovers by itself when the server starts (polling continues).
- [ ] Before any upload, the menu shows "Waiting for notes…".
- [ ] Uploading notes on the website makes the new quiz appear in Unity within
      ~3 seconds, with the **same quiz code** as the website.
- [ ] Uploading again while on the menu swaps to the newer quiz; uploading
      during a run does **not** change the current run.
- [ ] Gate count equals `questions.Length` (test a 3-question quiz too).
- [ ] Wrong answer → penalty applied, same question stays, chosen option
      disabled, no answer revealed. Right answer → gate opens.
- [ ] Double-clicking an option sends one request.
- [ ] Replaying the same quiz works (new `session_id`, no 409s).
- [ ] Playing the quiz in the browser first does not affect the Unity run.
- [ ] Restarting the backend mid-run → the game returns to the menu with a
      message instead of hanging.
- [ ] Long "Fill in the blank: …" questions wrap and stay readable.
- [ ] Works from a second laptop using the host's LAN IP (HTTP allowed in
      Player Settings).

---

## 8. Known limits

- Quizzes live in server memory: restarting the backend clears them (Unity
  sees 404 and should re-poll).
- "Latest" is global — one active quiz for the whole classroom/server. If
  several students upload at once, the last upload wins. (A per-student
  pairing code would be the next step if needed.)
- No authentication; run only on a trusted local network.
