using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamify.Racing
{
    public class GameUI : MonoBehaviour
    {
        [Header("HUD Header")]
        public GameObject hudRoot;
        public TextMeshProUGUI topicText;
        public TextMeshProUGUI questionProgressText;
        public TextMeshProUGUI questionBodyText;

        [Header("HUD Stats")]
        public TextMeshProUGUI livesText;
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI timerText;

        [Header("HUD Bottom")]
        public TextMeshProUGUI controlsHintText;
        public TextMeshProUGUI selectedDoorPreviewText;

        [Header("Explanation Screen (Wrong Answer)")]
        public GameObject explanationPanel;
        public TextMeshProUGUI explanationTitleText;
        public TextMeshProUGUI explanationBodyText;
        public TextMeshProUGUI explanationPromptText;

        [Header("Results Screen")]
        public GameObject resultsPanel;
        public TextMeshProUGUI resultsTitleText;
        public TextMeshProUGUI resultsScoreText;
        public TextMeshProUGUI resultsCorrectText;
        public TextMeshProUGUI resultsAccuracyText;
        public TextMeshProUGUI resultsPromptText;

        [Header("Feedback Flash")]
        public Image screenFlashImage;
        public TextMeshProUGUI speedText;
        public TextMeshProUGUI raceStatusText;
        public Image courseProgressFill;
        private float _collisionNoticeUntil;

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.carController == null) return;

            if (speedText != null)
            {
                speedText.text = Mathf.RoundToInt(Mathf.Abs(gm.carController.CurrentSpeed) * 3.6f) + " <size=60%>km/h</size>";
            }

            if (raceStatusText != null)
            {
                string codePrefix = (gm.questionManager != null && !string.IsNullOrEmpty(gm.questionManager.QuizCode))
                    ? $"[CODE: {gm.questionManager.QuizCode}] "
                    : "";

                if (Time.time < _collisionNoticeUntil)
                {
                    raceStatusText.text = "BUMP! -1 LIFE -- KEEP GOING";
                }
                else if (gm.CurrentState == GameState.WaitingForAnswer)
                {
                    raceStatusText.text = $"{codePrefix}CHOOSE A GATE -- SPACE TO RAM DOOR";
                }
                else if (gm.CurrentState == GameState.ShowingExplanation)
                {
                    raceStatusText.text = "YOU CAN TRY AGAIN";
                }
                else
                {
                    raceStatusText.text = $"{codePrefix}LET'S RACE! W TO ACCELERATE";
                }
            }

            if (courseProgressFill != null && gm.questionManager.Questions.Count > 0 && gm.checkpoints.Count >= gm.questionManager.Questions.Count)
            {
                courseProgressFill.fillAmount = Mathf.Clamp01(gm.carController.transform.position.z / gm.checkpoints[gm.questionManager.Questions.Count - 1].CheckpointZ);
            }

            if (gm.gameAudio != null)
            {
                gm.gameAudio.SetEnginePitch(Mathf.Lerp(0.75f, 1.7f, Mathf.Abs(gm.carController.CurrentSpeed) / gm.carController.boostedCruiseSpeed));
            }
        }

        public void ShowCollisionFeedback()
        {
            _collisionNoticeUntil = Time.time + 1.8f;
            FlashScreen(new Color(1f, 0.32f, 0.08f, 0.24f), 0.3f);
        }

        public void SetHudVisible(bool visible)
        {
            if (hudRoot != null) hudRoot.SetActive(visible);
        }

        public void UpdateQuestion(QuestionItem item, int index, int total, string quizCode = "")
        {
            if (item == null) return;

            if (topicText != null)
            {
                string codeBadge = string.IsNullOrEmpty(quizCode) ? "" : $"  [ QUIZ CODE: {quizCode} ]";
                topicText.text = $"[ TOPIC: {item.topic.ToUpper()} ]{codeBadge}";
            }

            if (questionProgressText != null)
            {
                questionProgressText.text = $"GATE {index + 1} / {total}";
            }

            if (questionBodyText != null)
            {
                questionBodyText.text = item.question;
            }
        }

        public void UpdateStats(int lives, int maxLives, int score, int secondsLeft)
        {
            if (livesText != null)
            {
                livesText.text = $"LIVES: {lives} / {maxLives}";
            }

            if (scoreText != null)
            {
                scoreText.text = $"SCORE: {score:D4}";
            }

            if (timerText != null)
            {
                timerText.text = $"TIME: {secondsLeft:D2}s";
                timerText.color = secondsLeft <= 5 ? new Color(1f, 0.3f, 0.3f) : Color.white;
            }
        }

        public void UpdateSelectedDoorPreview(int doorIndex, string optionText)
        {
            string[] badges = { "A", "B", "C", "D" };
            string badge = (doorIndex >= 0 && doorIndex < badges.Length) ? badges[doorIndex] : doorIndex.ToString();

            if (selectedDoorPreviewText != null)
            {
                selectedDoorPreviewText.text = $"AIMING AT GATE [{badge}]: \"{optionText}\"";
            }
        }

        public void ShowExplanation(string explanation)
        {
            if (explanationPanel != null)
            {
                explanationPanel.SetActive(true);
            }

            if (explanationTitleText != null)
            {
                explanationTitleText.text = "CRASH! WRONG GATE";
            }

            if (explanationBodyText != null)
            {
                explanationBodyText.text = explanation;
            }

            if (explanationPromptText != null)
            {
                explanationPromptText.text = "PRESS [ SPACE ] TO RETRY GATE";
            }
        }

        public void HideExplanation()
        {
            if (explanationPanel != null)
            {
                explanationPanel.SetActive(false);
            }
        }

        public void ShowResults(bool victory, int finalScore, int correct, int total, float accuracy, string quizCode = "")
        {
            if (resultsPanel != null)
            {
                resultsPanel.SetActive(true);
            }

            if (resultsTitleText != null)
            {
                resultsTitleText.text = victory ? "AMAZING! RACE COMPLETED" : "GOOD TRY! RACE OVER";
            }

            if (resultsScoreText != null)
            {
                string codeSuffix = string.IsNullOrEmpty(quizCode) ? "" : $"  (QUIZ: {quizCode})";
                resultsScoreText.text = $"FINAL SCORE: {finalScore}{codeSuffix}";
            }

            if (resultsCorrectText != null)
            {
                resultsCorrectText.text = $"GATES CLEARED: {correct} / {total}";
            }

            if (resultsAccuracyText != null)
            {
                resultsAccuracyText.text = $"ACCURACY: {accuracy:F1}%";
            }

            if (resultsPromptText != null)
            {
                resultsPromptText.text = "PRESS [ SPACE ] TO RESTART RACE";
            }
        }

        public void HideResults()
        {
            if (resultsPanel != null)
            {
                resultsPanel.SetActive(false);
            }
        }

        public void FlashScreen(Color flashColor, float duration = 0.3f)
        {
            if (screenFlashImage != null)
            {
                StartCoroutine(FlashRoutine(flashColor, duration));
            }
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            screenFlashImage.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(color.a, 0f, elapsed / duration);
                screenFlashImage.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }
            screenFlashImage.gameObject.SetActive(false);
        }
    }
}
