using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamify.Racing
{
    [Serializable]
    public class QuestionItem
    {
        public string question;
        public string[] options;
        public int correctIndex;
        public string explanation;
        public string topic;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(question) &&
                   options != null &&
                   options.Length == 4 &&
                   correctIndex >= 0 &&
                   correctIndex < 4;
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
