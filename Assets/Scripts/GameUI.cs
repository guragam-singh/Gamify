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

        public void SetHudVisible(bool visible)
        {
            if (hudRoot != null) hudRoot.SetActive(visible);
        }

        public void UpdateQuestion(QuestionItem item, int index, int total)
        {
            if (item == null) return;

            if (topicText != null)
                topicText.text = $"[ TOPIC: {item.topic.ToUpper()} ]";

            if (questionProgressText != null)
                questionProgressText.text = $"QUESTION {index + 1} / {total}";

            if (questionBodyText != null)
                questionBodyText.text = item.question;
        }

        public void UpdateStats(int lives, int maxLives, int score, int secondsLeft)
        {
            if (livesText != null)
            {
                string hearts = "";
                for (int i = 0; i < maxLives; i++)
                {
                    hearts += (i < lives) ? "█ " : "░ ";
                }
                livesText.text = $"LIVES: {hearts.TrimEnd()}";
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
                selectedDoorPreviewText.text = $"AIMING AT DOOR [{badge}]: \"{optionText}\"";
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
                explanationTitleText.text = "CRASH! WRONG DOOR";
            }

            if (explanationBodyText != null)
            {
                explanationBodyText.text = explanation;
            }

            if (explanationPromptText != null)
            {
                explanationPromptText.text = "PRESS [ SPACE ] TO RETRY QUESTION";
            }
        }

        public void HideExplanation()
        {
            if (explanationPanel != null)
            {
                explanationPanel.SetActive(false);
            }
        }

        public void ShowResults(bool victory, int finalScore, int correct, int total, float accuracy)
        {
            if (resultsPanel != null)
            {
                resultsPanel.SetActive(true);
            }

            if (resultsTitleText != null)
            {
                resultsTitleText.text = victory ? "★ RACE FINISHED ★" : "✖ CRASHED OUT - OUT OF LIVES ✖";
            }

            if (resultsScoreText != null)
            {
                resultsScoreText.text = $"FINAL SCORE: {finalScore}";
            }

            if (resultsCorrectText != null)
            {
                resultsCorrectText.text = $"CORRECT ANSWERS: {correct} / {total}";
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
