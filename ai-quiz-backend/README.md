# GAMIFY Backend MVP

A small Flask backend that turns study material (PDF, TXT, or photos/scans of
notes) into multiple-choice questions for a Unity racing game and the GAMIFY
website. It keeps recent quizzes and private answer keys in memory so it is
easy to run locally.

## Quick start (macOS / Linux)

```bash
brew install tesseract            # optional: enables OCR for photos and scanned PDFs
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
PORT=5050 .venv/bin/python main.py
```

Open http://localhost:5050/ — the server also serves the website from
`../GAMIFY_final_complete_website` (override with `FRONTEND_DIR`). On macOS,
port 5000 is taken by AirPlay Receiver, so use another port such as 5050.
If the website is hosted elsewhere, point it at the API with
`?api=http://host:port` once (it is remembered in the browser).

## How questions are produced

1. **AI** (`source: "ai"`) when `GEMINI_API_KEY` is set and the provider answers.
2. **Offline** (`source: "offline"`) otherwise: fill-in-the-blank questions
   built from sentences in the uploaded notes, with distractors drawn from
   other terms, names, or numbers in the same notes. No network needed.
3. **Fallback** (`source: "fallback"`) generic demo questions, only when the
   notes are too short to build at least three questions.

## OCR

With Tesseract installed, image uploads (PNG/JPG/WEBP/BMP/TIFF) are OCR'd, and
PDF pages with no usable text layer are OCR'd page by page (up to 20 pages).
For a scanned page, the embedded scan is OCR'd at its native resolution. The
image is grayscaled, upscaled when small, and autocontrasted. Lines that
Tesseract has low confidence in are dropped, and common misreads ("1s" → "is")
are fixed. Without Tesseract, text PDFs and TXT files still work, and image
uploads return a clear error. `GET /api/health` reports `ocr_available`.

## Requirements and setup

Python 3.10+ is recommended.

### Windows PowerShell

```powershell
py -3 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
Copy-Item .env.example .env
```

Edit `.env` and set `GEMINI_API_KEY` if AI-generated quizzes are wanted. Leaving it
empty is supported: the API returns a visible `source: "fallback"` quiz.

Start the server:

```powershell
python main.py
```

The server listens on `0.0.0.0:5000`, allowing a second computer on the same
network to connect. For local-only use, `python -m flask --app app run` also
works.

Run tests:

```powershell
python -m pytest -q
```

## Environment variables

| Variable | Default | Purpose |
| --- | --- | --- |
| `GEMINI_API_KEY` | empty | Google AI Studio key; never commit it |
| `GEMINI_MODEL` | `gemini-2.5-flash` | Gemini model identifier |
| `GEMINI_BASE_URL` | `https://generativelanguage.googleapis.com/v1beta` | API base URL |
| `AI_TIMEOUT_SECONDS` | `20` | HTTP timeout for AI requests |
| `CORS_ORIGINS` | `*` | `*` or comma-separated browser origins |
| `HOST` | `0.0.0.0` | Listening address |
| `PORT` | `5000` | Listening port |

AI questions come from Gemini's `POST /models/{model}:generateContent`
endpoint, authenticated with the `x-goog-api-key` header and JSON output mode
(`responseMimeType: application/json`).

## API

All responses are JSON. The public question payload never includes
`correct_index` or `explanation` before an answer is submitted.

### `GET /api/health`

```powershell
Invoke-RestMethod http://localhost:5000/api/health
```

Response:

```json
{"status":"ok","ai_configured":false,"ocr_available":true,"max_files":5,
 "max_file_bytes":10485760,"accepted_extensions":[".bmp",".jpeg",".jpg",".md",".pdf",".png",".tif",".tiff",".txt",".webp"]}
```

### `POST /api/generate-questions`

Use either a multipart upload (repeat the `file` field for up to 5 files,
10 MB each; PDF, TXT/MD, or images):

```powershell
curl.exe -X POST http://localhost:5000/api/generate-questions `
  -F "file=@C:\path\to\notes.txt"
```

or JSON during development:

```powershell
curl.exe -X POST http://localhost:5000/api/generate-questions `
  -H "Content-Type: application/json" `
  -d '{"material":"Mitosis is the process by which one cell divides into two genetically similar daughter cells."}'
```

Response shape:

```json
{
  "quiz_id": "2ec...",
  "source": "ai",
  "questions": [
    {
      "id": "q1",
      "question": "What does mitosis produce?",
      "options": ["...", "...", "...", "..."],
      "topic": "Cell biology"
    }
  ]
}
```

When the provider is unavailable, invalid, times out, or has no key, the same
shape is returned with `source: "offline"` (or `"fallback"`, see above) and a
short `message`. File uploads also return `documents`, with each file's `name`,
`kind`, `pages`, `ocr_pages`, and `characters`.

### `GET /api/questions/latest`

```powershell
curl.exe http://localhost:5000/api/questions/latest
```

Returns the latest public quiz. It returns `404` until a quiz has been created.

### `POST /api/answers`

```powershell
curl.exe -X POST http://localhost:5000/api/answers `
  -H "Content-Type: application/json" `
  -d '{"quiz_id":"2ec...","question_id":"q1","selected_index":1}'
```

Correct response:

```json
{
  "is_correct": true,
  "explanation": "Mitosis produces two genetically similar daughter cells.",
  "obstacle_action": "pass"
}
```

Wrong answers return `is_correct: false`, `obstacle_action: "retry"`, and an
empty `explanation` (so a miss doesn't reveal the answer); Unity controls lives
and should keep the player on the same question. Send a `session_id` per
play-through: correct answers are tracked per session, so a question already
passed **in that session** returns `409`, while other sessions (the website, a
replay) are unaffected.

### `GET /api/quizzes/<quiz_id>`

Same shape as `/api/questions/latest` for one quiz (the 20 most recent are kept).

Full Unity hand-off spec, with C# client code: [`../UNITY_INTEGRATION.md`](../UNITY_INTEGRATION.md).

## Unity on another computer

1. Start this backend on the host computer with `python main.py`.
2. Find the host's local IPv4 address with `ipconfig` (for example,
   `192.168.1.25`).
3. Allow Python or TCP port 5000 through the host computer's private-network
   Windows Firewall when prompted, or add an inbound rule for TCP 5000.
4. Configure Unity's backend base URL as `http://192.168.1.25:5000` and call
   the paths above. Do not use `localhost` in Unity: that refers to the Unity
   computer itself.
5. Confirm from the Unity computer with
   `curl.exe http://192.168.1.25:5000/api/health` or a browser.

Both computers must be on the same reachable network. A public deployment
needs HTTPS, authentication, rate limiting, and a production WSGI server;
those are outside this MVP.

## Troubleshooting and limitations

- **Connection refused/timeouts:** confirm the Flask process is running on
  `0.0.0.0`, use the host's LAN IP, verify both computers' network, and check
  Windows Firewall TCP 5000.
- **Scanned PDF or photo rejected:** install Tesseract (`brew install tesseract`,
  or the UB-Mannheim installer on Windows) and restart the server.
- **AI fallback:** check `GEMINI_API_KEY`, `GEMINI_MODEL`, network access,
  provider quota, and the timeout. Invalid AI JSON is validated and retried
  once before fallback.
- **Upload errors:** up to 5 files of 10 MB each; TXT files must be UTF-8.
- The 20 most recent quizzes and their answer keys are kept in memory and
  disappear when the server restarts. There is no database, authentication,
  or RAG in this prototype.
