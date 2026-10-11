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
        public bool NetworkError;  // timeout, refused, DNS, insecure-HTTP blocked...
        public string Error;       // server "error" message or network error text
        public T Data;
    }
}
