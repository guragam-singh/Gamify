"""Offline quiz generation used when no AI provider is reachable.

Builds fill-in-the-blank multiple-choice questions directly from the study
material, so a quiz is still about the uploaded notes even without an API key.
Answers are key terms, names, or numbers lifted from real sentences; the
distractors are other terms of the same kind from the same material.
"""

from __future__ import annotations

import hashlib
import random
import re
from collections import Counter
from typing import Any

BLANK = "_____"
MIN_QUESTIONS = 3
MAX_QUESTIONS = 5

STOPWORDS = set(
    """a about above after again against all also am an and any are as at be because been before being
    below between both but by can could did do does doing down during each few for from further had has
    have having he her here hers herself him himself his how i if in into is it its itself just let me
    more most my myself no nor not now of off on once only or other our ours ourselves out over own same
    she should so some such than that the their theirs them themselves then there these they this those
    through to too under until up very was we were what when where which while who whom why will with
    would you your yours yourself yourselves however therefore thus another called known example usually
    often many much every since within without around among inside outside large small mainly mostly across along upon onto toward towards
    something someone anything things thing people really quite rather always never sometimes""".split()
)

_SENTENCE_SPLIT = re.compile(r"(?<=[.!?])\s+(?=[A-Z0-9\"'(])|\n+")
_NUMBER = re.compile(r"(?<![\w.])(\d{1,3}(?:,\d{3})+|\d+(?:\.\d+)?)(%?)(?![\w.]*\w)")
_PROPER = re.compile(r"\b([A-Z][a-z]+(?:\s+(?:of\s+|the\s+)?[A-Z][a-z]+)*)\b")
_WORD = re.compile(r"\b[A-Za-z][a-z]{3,}\b")
_DEFINITION_CUES = re.compile(r"\b(is|are|was|were|called|known as|means|refers to|defined as|consists of|produces?)\b", re.I)


class OfflineGenerationError(RuntimeError):
    """Raised when the material does not contain enough usable sentences."""


def _sentences(material: str) -> list[str]:
    result = []
    for raw in _SENTENCE_SPLIT.split(material):
        sentence = " ".join(raw.split())
        words = sentence.split()
        if not 6 <= len(words) <= 45 or not 35 <= len(sentence) <= 300:
            continue
        if sum(ch.isalpha() for ch in sentence) / len(sentence) < 0.7:
            continue  # tables, references, OCR noise
        if sentence.endswith("?"):
            continue
        result.append(sentence)
    return result


def _term_frequencies(material: str) -> Counter:
    return Counter(word.lower() for word in _WORD.findall(material) if word.lower() not in STOPWORDS)


def _proper_nouns(sentence: str) -> list[str]:
    found = []
    for match in _PROPER.finditer(sentence):
        phrase = match.group(1)
        if match.start() == 0 and " " not in phrase:
            continue  # first word of a sentence is capitalised anyway
        if phrase.lower() in STOPWORDS:
            continue
        found.append(phrase)
    return found


def _candidates(sentence: str, frequencies: Counter) -> list[tuple[str, str]]:
    """Return (answer, kind) pairs ranked from most to least quiz-worthy."""

    candidates: list[tuple[str, str]] = []
    candidates += [(phrase, "proper") for phrase in _proper_nouns(sentence)]
    candidates += [(match.group(0), "number") for match in _NUMBER.finditer(sentence)]
    words = [word for word in _WORD.findall(sentence) if word.lower() not in STOPWORDS and len(word) >= 5]
    # Terms repeated across the notes are usually the concepts being taught.
    words.sort(key=lambda word: (frequencies[word.lower()], len(word)), reverse=True)
    candidates += [(word, "term") for word in words[:2] if frequencies[word.lower()] >= 2]
    return candidates


def _number_distractors(answer: str, rng: random.Random) -> list[str]:
    percent = answer.endswith("%")
    raw = answer.rstrip("%").replace(",", "")
    has_comma = "," in answer
    is_decimal = "." in raw
    value = float(raw)
    is_year = not is_decimal and 1000 <= value <= 2100
    options: set[str] = set()
    attempts = 0
    while len(options) < 3 and attempts < 50:
        attempts += 1
        if is_year:
            candidate = value + rng.choice([-1, 1]) * rng.randint(1, 25)
        elif value == 0:
            candidate = rng.randint(1, 10)
        else:
            candidate = value * rng.choice([0.5, 0.75, 0.8, 1.2, 1.25, 1.5, 2, 3]) + rng.choice([-1, 0, 1])
        if is_decimal:
            text = f"{candidate:.{len(raw.split('.')[1])}f}"
        else:
            candidate = int(round(candidate))
            if candidate < 0:
                continue
            text = f"{candidate:,}" if has_comma else str(candidate)
        text += "%" if percent else ""
        if text != answer:
            options.add(text)
    return list(options)


def _matches_case(word: str, reference: str) -> str:
    return word.capitalize() if reference[:1].isupper() else word.lower()


def _word_shape(word: str) -> str:
    """Crude part-of-speech hint so distractors look grammatically plausible."""

    lower = word.lower()
    for suffix in ("ing", "ed", "ly"):
        if lower.endswith(suffix):
            return suffix
    return "noun"


def _distractors(answer: str, kind: str, pools: dict[str, list[str]], rng: random.Random) -> list[str]:
    if kind == "number":
        return _number_distractors(answer, rng)
    pool = [item for item in pools[kind] if item.lower() != answer.lower() and answer.lower() not in item.lower() and item.lower() not in answer.lower()]
    if kind == "term":
        shape = _word_shape(answer)
        pool = [_matches_case(item, answer) for item in pool if _word_shape(item) == shape]
        # Similar-length words make the blank less guessable.
        pool.sort(key=lambda item: abs(len(item) - len(answer)))
        pool = pool[:8]
    rng.shuffle(pool)
    unique: list[str] = []
    for item in pool:
        if item not in unique:
            unique.append(item)
    return unique[:3]


def _topic(material: str) -> str:
    proper = Counter(phrase for sentence in _sentences(material) for phrase in _proper_nouns(sentence))
    if proper and proper.most_common(1)[0][1] >= 3:
        return proper.most_common(1)[0][0]
    frequencies = _term_frequencies(material)
    long_terms = [(word, count) for word, count in frequencies.most_common(20) if len(word) >= 5]
    if long_terms:
        return long_terms[0][0].capitalize()
    return "Your notes"


def generate_offline_questions(material: str, max_questions: int = MAX_QUESTIONS) -> list[dict[str, Any]]:
    """Return between three and *max_questions* questions built from *material*."""

    seed = int(hashlib.sha256(material.encode("utf-8")).hexdigest()[:12], 16)
    rng = random.Random(seed)  # same notes -> same quiz, which keeps tests deterministic
    sentences = _sentences(material)
    frequencies = _term_frequencies(material)

    pools: dict[str, list[str]] = {"proper": [], "term": []}
    for sentence in sentences:
        for phrase in _proper_nouns(sentence):
            if phrase not in pools["proper"]:
                pools["proper"].append(phrase)
    terms = [word for word, _count in frequencies.most_common(80) if len(word) >= 5]
    repeated = [word for word in terms if frequencies[word] >= 2]
    # Prefer repeated concepts as distractors; fall back to all words for short notes.
    pools["term"] = repeated if len(repeated) >= 8 else terms

    def sentence_score(sentence: str) -> float:
        score = sum(frequencies[word.lower()] for word in _WORD.findall(sentence)) / max(len(sentence.split()), 1)
        if _DEFINITION_CUES.search(sentence):
            score += 2
        return score

    ranked = sorted(sentences, key=sentence_score, reverse=True)
    topic = _topic(material)
    questions: list[dict[str, Any]] = []
    used_answers: set[str] = set()
    for sentence in ranked:
        if len(questions) >= max_questions:
            break
        for answer, kind in _candidates(sentence, frequencies):
            if answer.lower() in used_answers:
                continue
            distractors = _distractors(answer, kind, pools, rng)
            if len(distractors) < 3:
                continue
            pattern = re.compile(r"(?<!\w)" + re.escape(answer) + r"(?!\w)")
            if not pattern.search(sentence):
                continue
            blanked = pattern.sub(BLANK, sentence, count=1)
            if answer.lower() in blanked.lower():
                continue  # answer still visible elsewhere in the sentence
            options = [answer, *distractors[:3]]
            rng.shuffle(options)
            questions.append(
                {
                    "id": f"q{len(questions) + 1}",
                    "question": f"Fill in the blank: {blanked}",
                    "options": options,
                    "correct_index": options.index(answer),
                    "explanation": sentence,
                    "topic": topic,
                    "answered": False,
                }
            )
            used_answers.add(answer.lower())
            break

    if len(questions) < MIN_QUESTIONS:
        raise OfflineGenerationError("The material is too short to build a quiz from.")
    return questions
