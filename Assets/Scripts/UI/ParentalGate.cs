using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.UI
{
    /// <summary>
    /// Asks a grown-up question before anything leaves the game (a website, a store page, a mail link), as the App
    /// Store Kids Category requires. Call <see cref="Request"/> with what to do once an adult answers correctly.
    /// One wrong answer closes the gate, so guessing doesn't work.
    /// </summary>
    /// <remarks>
    /// Setup: a full-screen panel with a CanvasGroup (hidden at start), a question label, a numeric input field and
    /// OK / Cancel buttons. The panel blocks touches only while it is open.
    /// </remarks>
    public class ParentalGate : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text questionLabel;
        [SerializeField] private TMP_InputField answerField;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Tooltip("Shown above the question so a child knows to hand the device over.")]
        [SerializeField] private string intro = "Ask a grown-up to answer:";

        private readonly System.Random random = new System.Random();
        private ParentalGateChallenge challenge;
        private Action onPassed;

        /// <summary>True while the gate is showing.</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (answerField != null)
            {
                answerField.contentType = TMP_InputField.ContentType.IntegerNumber;
            }

            Hide();
        }

        private void OnEnable()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        }

        private void OnDisable()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
        }

        /// <summary>Shows a new question. <paramref name="passed"/> runs only if an adult answers it correctly.</summary>
        public void Request(Action passed)
        {
            onPassed = passed;
            challenge = ParentalGateChallenge.CreateRandom(random);

            if (questionLabel != null)
            {
                questionLabel.text = $"{intro}\n{challenge.Question}";
            }

            if (answerField != null)
            {
                answerField.text = string.Empty;
                answerField.ActivateInputField();
            }

            SetVisible(true);
        }

        /// <summary>Checks the answer. Right: runs the action. Wrong: closes without running it.</summary>
        public void Confirm()
        {
            if (!IsOpen)
            {
                return;
            }

            bool passed = challenge != null && answerField != null && challenge.IsCorrect(answerField.text);
            Action action = onPassed;
            Hide();

            if (passed)
            {
                action?.Invoke();
            }
        }

        public void Cancel() => Hide();

        private void Hide()
        {
            onPassed = null;
            challenge = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            IsOpen = visible;
            if (panel == null)
            {
                return;
            }

            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }
    }
}
