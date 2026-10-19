import io

import pytest

from app import create_app


def valid_questions(_material):
    return [
        {
            "id": "q1",
            "question": "What is two plus two?",
            "options": ["3", "4", "5", "6"],
            "correct_index": 1,
            "explanation": "Two plus two equals four.",
            "topic": "Math",
            "answered": False,
        }
    ]


@pytest.fixture
def client():
    app = create_app({"TESTING": True}, question_generator=valid_questions)
    return app.test_client()


def test_health_and_no_latest_quiz(client):
    health = client.get("/api/health").get_json()
    assert health["status"] == "ok"
    assert ".pdf" in health["accepted_extensions"]
    assert isinstance(health["ocr_available"], bool)
    response = client.get("/api/questions/latest")
    assert response.status_code == 404


def test_json_generation_keeps_answers_private_and_latest_works(client):
    response = client.post("/api/generate-questions", json={"material": "basic arithmetic"})
    assert response.status_code == 200
    quiz = response.get_json()
    assert quiz["source"] == "ai"
    assert "correct_index" not in quiz["questions"][0]
    assert "explanation" not in quiz["questions"][0]

    latest = client.get("/api/questions/latest")
    assert latest.status_code == 200
    assert latest.get_json() == quiz


def test_txt_upload_and_invalid_extension(client):
    response = client.post(
        "/api/generate-questions",
        data={"file": (io.BytesIO(b"cell division notes"), "notes.txt")},
        content_type="multipart/form-data",
    )
    assert response.status_code == 200

    response = client.post(
        "/api/generate-questions",
        data={"file": (io.BytesIO(b"data"), "notes.docx")},
        content_type="multipart/form-data",
    )
    assert response.status_code == 400
    assert "Unsupported" in response.get_json()["error"]


def test_ai_failure_uses_explicit_fallback():
    def broken_generator(_material):
        raise RuntimeError("provider unavailable")

    client = create_app({"TESTING": True}, question_generator=broken_generator).test_client()
    response = client.post("/api/generate-questions", json={"material": "notes"})
    assert response.status_code == 200
    payload = response.get_json()
    assert payload["source"] == "fallback"
    assert payload["message"]
    assert "correct_index" not in payload["questions"][0]


def test_answers_allow_retry_but_not_duplicate_correct_award(client):
    quiz = client.post("/api/generate-questions", json={"material": "notes"}).get_json()
    answer_url = "/api/answers"
    wrong = client.post(
        answer_url,
        json={"quiz_id": quiz["quiz_id"], "question_id": "q1", "selected_index": 0},
    )
    assert wrong.status_code == 200
    assert wrong.get_json() == {
        "is_correct": False,
        "explanation": "",
        "obstacle_action": "retry",
    }

    correct = client.post(
        answer_url,
        json={"quiz_id": quiz["quiz_id"], "question_id": "q1", "selected_index": 1},
    )
    assert correct.status_code == 200
    assert correct.get_json()["obstacle_action"] == "pass"

    repeated = client.post(
        answer_url,
        json={"quiz_id": quiz["quiz_id"], "question_id": "q1", "selected_index": 1},
    )
    assert repeated.status_code == 409


@pytest.mark.parametrize(
    "payload",
    [
        {"quiz_id": "missing", "question_id": "q1", "selected_index": 0},
        {"quiz_id": "", "question_id": "q1", "selected_index": 0},
        {"quiz_id": "missing", "question_id": "q1", "selected_index": 4},
    ],
)
def test_invalid_answer_submissions(client, payload):
    response = client.post("/api/answers", json=payload)
    assert response.status_code in (400, 404)


def test_question_must_belong_to_quiz(client):
    quiz = client.post("/api/generate-questions", json={"material": "notes"}).get_json()
    response = client.post(
        "/api/answers",
        json={"quiz_id": quiz["quiz_id"], "question_id": "other", "selected_index": 0},
    )
    assert response.status_code == 404


NOTES = (
    "Photosynthesis is the process by which green plants use sunlight to make food. "
    "It takes place mainly in the leaves, inside tiny structures called chloroplasts. "
    "Chloroplasts contain a green pigment called chlorophyll, which absorbs light energy. "
    "During photosynthesis, plants take in carbon dioxide through small pores called stomata. "
    "Using light energy, the plant converts carbon dioxide and water into glucose and oxygen. "
    "The scientist Jan Ingenhousz discovered in 1779 that light is essential for photosynthesis."
)


def broken_generator(_material):
    raise RuntimeError("provider unavailable")


def test_ai_failure_builds_questions_from_notes():
    client = create_app({"TESTING": True}, question_generator=broken_generator).test_client()
    payload = client.post("/api/generate-questions", json={"material": NOTES}).get_json()
    assert payload["source"] == "offline"
    assert 3 <= len(payload["questions"]) <= 5
    for question in payload["questions"]:
        assert "_____" in question["question"]
        assert len(set(question["options"])) == 4
        assert "correct_index" not in question


def test_multiple_file_upload_reports_documents(client):
    response = client.post(
        "/api/generate-questions",
        data={"file": [(io.BytesIO(b"cell division notes"), "a.txt"), (io.BytesIO(b"more notes"), "b.txt")]},
        content_type="multipart/form-data",
    )
    assert response.status_code == 200
    documents = response.get_json()["documents"]
    assert [doc["name"] for doc in documents] == ["a.txt", "b.txt"]


def test_bad_file_in_batch_is_named(client):
    response = client.post(
        "/api/generate-questions",
        data={"file": [(io.BytesIO(b"fine"), "a.txt"), (io.BytesIO(b""), "empty.txt")]},
        content_type="multipart/form-data",
    )
    assert response.status_code == 400
    assert response.get_json()["error"].startswith("empty.txt:")


def test_older_quiz_still_answerable_after_new_quiz(client):
    first = client.post("/api/generate-questions", json={"material": "notes"}).get_json()
    client.post("/api/generate-questions", json={"material": "other notes"})
    response = client.post(
        "/api/answers",
        json={"quiz_id": first["quiz_id"], "question_id": "q1", "selected_index": 1},
    )
    assert response.status_code == 200


def test_serves_frontend_when_configured(tmp_path):
    (tmp_path / "index.html").write_text("<h1>GAMIFY</h1>")
    game_dir = tmp_path / "game"
    game_dir.mkdir()
    (game_dir / "index.html").write_text("<h1>Road Rush</h1>")
    build_dir = game_dir / "Build"
    build_dir.mkdir()
    (build_dir / "Build.wasm.br").write_bytes(b"\x1b\x00\x00")
    client = create_app({"TESTING": True, "FRONTEND_DIR": str(tmp_path)}, question_generator=valid_questions).test_client()
    assert b"GAMIFY" in client.get("/").data
    assert client.get("/missing.png").status_code == 404
    assert client.get("/api/health").status_code == 200

    # Game route redirects and serves WebGL index
    game_redirect = client.get("/game", follow_redirects=False)
    assert game_redirect.status_code == 302
    assert game_redirect.headers.get("Location") == "/game/"

    game_home = client.get("/game/")
    assert game_home.status_code == 200
    assert b"Road Rush" in game_home.data

    wasm_resp = client.get("/game/Build/Build.wasm.br")
    assert wasm_resp.status_code == 200
    assert wasm_resp.headers.get("Content-Encoding") == "br"
    assert wasm_resp.headers.get("Content-Type") == "application/wasm"


def answer(client, quiz_id, index, session_id=None):
    body = {"quiz_id": quiz_id, "question_id": "q1", "selected_index": index}
    if session_id is not None:
        body["session_id"] = session_id
    return client.post("/api/answers", json=body)


def test_sessions_track_answers_independently(client):
    quiz = client.post("/api/generate-questions", json={"material": "notes"}).get_json()
    # The web preview answering must not block a Unity run of the same quiz.
    assert answer(client, quiz["quiz_id"], 1, "web-tab").status_code == 200
    assert answer(client, quiz["quiz_id"], 1, "web-tab").status_code == 409
    unity = answer(client, quiz["quiz_id"], 1, "unity-run-1")
    assert unity.status_code == 200
    assert unity.get_json()["explanation"] == "Two plus two equals four."
    assert answer(client, quiz["quiz_id"], 1, "bad id!").status_code == 400


def test_latest_exposes_metadata_for_polling(client):
    created = client.post(
        "/api/generate-questions",
        data={"file": (io.BytesIO(b"cell division notes"), "biology.txt")},
        content_type="multipart/form-data",
    ).get_json()
    latest = client.get("/api/questions/latest").get_json()
    assert latest["quiz_id"] == created["quiz_id"]
    assert latest["title"] == "biology"
    assert latest["question_count"] == 1
    assert latest["created_at"]
    assert latest["documents"][0]["name"] == "biology.txt"


def test_quiz_by_id(client):
    quiz = client.post("/api/generate-questions", json={"material": "notes"}).get_json()
    assert client.get(f"/api/quizzes/{quiz['quiz_id']}").get_json() == quiz
    assert client.get("/api/quizzes/nope").status_code == 404
