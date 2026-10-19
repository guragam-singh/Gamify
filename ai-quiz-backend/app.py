"""Flask API for the GAMIFY educational racing quiz MVP."""

from __future__ import annotations

import os
import re
import uuid
from collections import OrderedDict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from dotenv import load_dotenv
from flask import Flask, abort, jsonify, redirect, request, send_from_directory
from flask_cors import CORS

from document_parser import (
    DEFAULT_MAX_BYTES,
    SUPPORTED_EXTENSIONS,
    DocumentParseError,
    ocr_available,
    parse_document,
)
from offline_questions import OfflineGenerationError, generate_offline_questions
from question_generator import configured_api_key, generate_ai_questions

load_dotenv()

FALLBACK_QUESTIONS = [
    {
        "id": "fallback-1",
        "question": "Which process do plants use to convert light energy into chemical energy?",
        "options": ["Photosynthesis", "Respiration", "Digestion", "Fermentation"],
        "correct_index": 0,
        "explanation": "Photosynthesis uses light energy to make sugars from carbon dioxide and water.",
        "topic": "Biology",
        "answered": False,
    },
    {
        "id": "fallback-2",
        "question": "What is the value of 7 multiplied by 8?",
        "options": ["54", "56", "64", "48"],
        "correct_index": 1,
        "explanation": "Seven groups of eight equal 56.",
        "topic": "Mathematics",
        "answered": False,
    },
    {
        "id": "fallback-3",
        "question": "Which gas makes up most of Earth's atmosphere?",
        "options": ["Oxygen", "Carbon dioxide", "Nitrogen", "Hydrogen"],
        "correct_index": 2,
        "explanation": "Nitrogen makes up roughly 78 percent of Earth's atmosphere.",
        "topic": "Earth science",
        "answered": False,
    },
    {
        "id": "fallback-4",
        "question": "What is the main function of a constitution?",
        "options": ["To set fundamental rules of government", "To list daily weather", "To create currency", "To schedule elections only"],
        "correct_index": 0,
        "explanation": "A constitution establishes the fundamental principles and structure of a government.",
        "topic": "Civics",
        "answered": False,
    },
    {
        "id": "fallback-5",
        "question": "Which word is a synonym for 'rapid'?",
        "options": ["Slow", "Quick", "Quiet", "Distant"],
        "correct_index": 1,
        "explanation": "Rapid means happening quickly, so quick is the synonym.",
        "topic": "Language",
        "answered": False,
    },
]


MAX_FILES_PER_REQUEST = 5
# Enough for the web client and a Unity client to play different quizzes at once.
MAX_STORED_QUIZZES = 20
# Each play-through (a Unity run, a browser tab) tracks its own correct answers.
MAX_SESSIONS_PER_QUIZ = 200
DEFAULT_SESSION = "default"
_SESSION_ID = re.compile(r"^[A-Za-z0-9_.:-]{1,64}$")
DEFAULT_FRONTEND_DIR = Path(__file__).resolve().parent.parent / "GAMIFY_final_complete_website"

SOURCE_MESSAGES = {
    "offline": "AI generation was unavailable; questions were built directly from your notes.",
    "fallback": "AI generation was unavailable; generic demo questions were returned.",
}


def _public_question(question: dict[str, Any]) -> dict[str, Any]:
    return {
        "id": question["id"],
        "question": question["question"],
        "options": question["options"],
        "topic": question.get("topic", ""),
    }


def _public_quiz(quiz_id: str, quiz: dict[str, Any]) -> dict[str, Any]:
    payload = {
        "quiz_id": quiz_id,
        "created_at": quiz["created_at"],
        "title": quiz["title"],
        "question_count": len(quiz["questions"]),
        "questions": [_public_question(question) for question in quiz["questions"]],
        "source": quiz["source"],
    }
    if quiz["source"] in SOURCE_MESSAGES:
        payload["message"] = SOURCE_MESSAGES[quiz["source"]]
    if quiz["documents"]:
        payload["documents"] = quiz["documents"]
    return payload


def _quiz_title(documents: list[dict[str, Any]]) -> str:
    if not documents:
        return "Typed notes"
    names = [os.path.splitext(doc["name"])[0] for doc in documents]
    return names[0] if len(names) == 1 else f"{names[0]} + {len(names) - 1} more"


def _fallback_questions() -> list[dict[str, Any]]:
    # Copy nested lists/dicts so a submission cannot mutate the template.
    return [
        {**question, "options": list(question["options"]), "answered": False}
        for question in FALLBACK_QUESTIONS
    ]


def create_app(config: dict[str, Any] | None = None, question_generator: Callable[[str], list[dict[str, Any]]] | None = None) -> Flask:
    app = Flask(__name__)
    app.config.from_mapping(
        MAX_UPLOAD_BYTES=DEFAULT_MAX_BYTES,
        MAX_CONTENT_LENGTH=DEFAULT_MAX_BYTES * MAX_FILES_PER_REQUEST + 64 * 1024,
        CORS_ORIGINS=os.getenv("CORS_ORIGINS", "*"),
        FRONTEND_DIR=os.getenv("FRONTEND_DIR", str(DEFAULT_FRONTEND_DIR)),
    )
    if config:
        app.config.update(config)

    cors_origins = app.config["CORS_ORIGINS"]
    if isinstance(cors_origins, str) and cors_origins.strip() != "*":
        cors_origins = [origin.strip() for origin in cors_origins.split(",") if origin.strip()]
    CORS(app, origins=cors_origins)
    quiz_store: OrderedDict[str, dict[str, Any]] = OrderedDict()
    app.extensions["quiz_store"] = quiz_store
    app.extensions["latest_quiz_id"] = None
    generator = question_generator or generate_ai_questions
    max_mb = f"{app.config['MAX_UPLOAD_BYTES'] / (1024 * 1024):g} MB"

    @app.errorhandler(413)
    def request_too_large(_error):
        return jsonify(error=f"The request is too large. Upload at most {MAX_FILES_PER_REQUEST} files of up to {max_mb} each."), 413

    def frontend_dir() -> Path | None:
        path = Path(app.config["FRONTEND_DIR"])
        return path if (path / "index.html").is_file() else None

    @app.get("/")
    def home():
        # Serving the website from the API avoids CORS/file:// problems locally.
        directory = frontend_dir()
        if directory:
            return send_from_directory(directory, "index.html")
        return jsonify(status="hello world")

    @app.get("/game")
    def game_redirect():
        return redirect("/game/", code=302)

    @app.get("/game/")
    def game_home():
        directory = frontend_dir()
        if directory and (directory / "game" / "index.html").is_file():
            return send_from_directory(directory / "game", "index.html")
        abort(404)

    @app.get("/<path:filename>")
    def frontend_asset(filename: str):
        directory = frontend_dir()
        if not directory or filename.startswith("api/"):
            abort(404)
        target = directory / filename
        if target.is_dir():
            if (target / "index.html").is_file():
                return send_from_directory(target, "index.html")
            abort(404)
        resp = send_from_directory(directory, filename)
        if filename.endswith(".br"):
            resp.headers["Content-Encoding"] = "br"
            if filename.endswith(".wasm.br"):
                resp.headers["Content-Type"] = "application/wasm"
            elif filename.endswith(".js.br"):
                resp.headers["Content-Type"] = "application/javascript"
            elif filename.endswith(".data.br"):
                resp.headers["Content-Type"] = "application/octet-stream"
        return resp

    @app.get("/api/health")
    def health():
        return jsonify(
            status="ok",
            ai_configured=bool(configured_api_key()) or question_generator is not None,
            ocr_available=ocr_available(),
            accepted_extensions=sorted(SUPPORTED_EXTENSIONS),
            max_file_bytes=app.config["MAX_UPLOAD_BYTES"],
            max_files=MAX_FILES_PER_REQUEST,
        )

    @app.post("/api/generate-questions")
    def generate_questions_route():
        documents: list[dict[str, Any]] = []
        try:
            uploads = request.files.getlist("file")
            if uploads:
                if len(uploads) > MAX_FILES_PER_REQUEST:
                    return jsonify(error=f"Upload at most {MAX_FILES_PER_REQUEST} files at a time."), 400
                parts = []
                for uploaded in uploads:
                    name = uploaded.filename or ""
                    try:
                        parsed = parse_document(
                            name,
                            uploaded.read(app.config["MAX_UPLOAD_BYTES"] + 1),
                            app.config["MAX_UPLOAD_BYTES"],
                        )
                    except DocumentParseError as exc:
                        raise DocumentParseError(f"{name}: {exc}" if len(uploads) > 1 else str(exc)) from exc
                    parts.append(parsed.text)
                    documents.append(
                        {
                            "name": name,
                            "kind": parsed.kind,
                            "pages": parsed.pages,
                            "ocr_pages": parsed.ocr_pages,
                            "characters": len(parsed.text),
                        }
                    )
                material = "\n\n".join(parts)
            else:
                body = request.get_json(silent=True)
                if not isinstance(body, dict) or not isinstance(body.get("material"), str):
                    return jsonify(error="Provide a PDF/TXT file in 'file' or JSON with a 'material' string."), 400
                material = body["material"].strip()
                if not material:
                    return jsonify(error="The material cannot be empty."), 400
                if len(material.encode("utf-8")) > app.config["MAX_UPLOAD_BYTES"]:
                    return jsonify(error=f"The material is too large. The maximum size is {max_mb}."), 400
        except DocumentParseError as exc:
            return jsonify(error=str(exc)), 400

        source = "ai"
        try:
            questions = generator(material)
        except Exception:
            # Provider errors and invalid provider output must never make the MVP unusable.
            # Prefer questions built from the notes; generic demo questions are the last resort.
            try:
                questions = generate_offline_questions(material)
                source = "offline"
            except OfflineGenerationError:
                questions = _fallback_questions()
                source = "fallback"

        quiz_id = uuid.uuid4().hex
        quiz_store[quiz_id] = {
            "questions": questions,
            "source": source,
            "documents": documents,
            "title": _quiz_title(documents),
            "created_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
            "sessions": {},  # session_id -> ids of questions answered correctly
        }
        while len(quiz_store) > MAX_STORED_QUIZZES:
            quiz_store.popitem(last=False)
        app.extensions["latest_quiz_id"] = quiz_id
        return jsonify(_public_quiz(quiz_id, quiz_store[quiz_id])), 200

    @app.get("/api/questions/latest")
    def latest_questions():
        quiz_id = app.extensions.get("latest_quiz_id")
        quiz = quiz_store.get(quiz_id) if quiz_id else None
        if not quiz:
            return jsonify(error="No active quiz exists. Generate questions first."), 404
        return jsonify(_public_quiz(quiz_id, quiz))

    @app.get("/api/quizzes/<quiz_id>")
    def quiz_by_id(quiz_id: str):
        quiz = quiz_store.get(quiz_id)
        if not quiz:
            return jsonify(error="Quiz not found or no longer active."), 404
        return jsonify(_public_quiz(quiz_id, quiz))

    @app.post("/api/answers")
    def submit_answer():
        body = request.get_json(silent=True)
        if not isinstance(body, dict):
            return jsonify(error="Request body must be JSON."), 400
        quiz_id = body.get("quiz_id")
        question_id = body.get("question_id")
        selected_index = body.get("selected_index")
        if not isinstance(quiz_id, str) or not quiz_id or not isinstance(question_id, str) or not question_id:
            return jsonify(error="quiz_id and question_id are required strings."), 400
        if isinstance(selected_index, bool) or not isinstance(selected_index, int) or not 0 <= selected_index <= 3:
            return jsonify(error="selected_index must be an integer from 0 to 3."), 400

        session_id = body.get("session_id", DEFAULT_SESSION)
        if not isinstance(session_id, str) or not _SESSION_ID.match(session_id):
            return jsonify(error="session_id must be 1-64 letters, digits, or - _ . :"), 400

        quiz = quiz_store.get(quiz_id)
        if not quiz:
            return jsonify(error="Quiz not found or no longer active."), 404
        question = next((item for item in quiz["questions"] if item["id"] == question_id), None)
        if not question:
            return jsonify(error="Question not found in this quiz."), 404
        sessions = quiz["sessions"]
        answered = sessions.get(session_id, set())
        if question_id in answered:
            return jsonify(error="This question has already been answered correctly in this session."), 409

        is_correct = selected_index == question["correct_index"]
        if is_correct:
            if session_id not in sessions and len(sessions) >= MAX_SESSIONS_PER_QUIZ:
                sessions.pop(next(iter(sessions)))  # forget the oldest play-through
            sessions.setdefault(session_id, set()).add(question_id)
        return jsonify(
            is_correct=is_correct,
            # Only explain once the answer is earned; on a miss it would reveal the answer.
            explanation=question["explanation"] if is_correct else "",
            obstacle_action="pass" if is_correct else "retry",
        )

    return app


app = create_app()


if __name__ == "__main__":
    app.run(host=os.getenv("HOST", "0.0.0.0"), port=int(os.getenv("PORT", "5000")), debug=False)
