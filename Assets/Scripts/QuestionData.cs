using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamify.Racing
{
    [Serializable]
    public class QuestionItem
    {
        public string id;
        public string question;
        public string[] options;
        public int correctIndex = -1;
        public string explanation;
        public string topic;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(question) &&
                   options != null &&
                   options.Length == 4;
        }

        public static QuestionItem FromQuizQuestion(Gamify.QuizQuestion q)
        {
            if (q == null) return null;
            return new QuestionItem
            {
                id = q.id,
                question = q.question,
                options = q.options,
                correctIndex = -1,
                explanation = "",
                topic = q.topic
            };
        }
    }

    [Serializable]
    public class QuestionListWrapper
    {
        public List<QuestionItem> questions;
    }

    public static class QuestionParser
    {
        public static List<QuestionItem> ParseJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<QuestionItem>();

            json = json.Trim();

            // Backend quizzes carry a quiz ID and are evaluated by the server.
            // Local files share the questions wrapper but include correctIndex;
            // converting those to QuizQuestion would discard their answer keys.
            try
            {
                var quiz = JsonUtility.FromJson<Gamify.Quiz>(json);
                if (quiz != null && !string.IsNullOrEmpty(quiz.quiz_id) &&
                    quiz.questions != null && quiz.questions.Length > 0)
                {
                    var list = new List<QuestionItem>();
                    foreach (var q in quiz.questions)
                    {
                        list.Add(QuestionItem.FromQuizQuestion(q));
                    }
                    return list;
                }
            }
            catch { }

            // Try object with "questions" array
            if (json.StartsWith("{"))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<QuestionListWrapper>(json);
                    if (wrapper != null && wrapper.questions != null && wrapper.questions.Count > 0)
                        return wrapper.questions;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Gamify] Failed parsing wrapper JSON: {ex.Message}");
                }
            }

            // Try raw array wrapped into object
            if (json.StartsWith("["))
            {
                try
                {
                    string wrapped = "{\"questions\":" + json + "}";
                    var wrapper = JsonUtility.FromJson<QuestionListWrapper>(wrapped);
                    if (wrapper != null && wrapper.questions != null && wrapper.questions.Count > 0)
                        return wrapper.questions;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Gamify] Failed parsing array JSON: {ex.Message}");
                }
            }

            return new List<QuestionItem>();
        }
    }
}
