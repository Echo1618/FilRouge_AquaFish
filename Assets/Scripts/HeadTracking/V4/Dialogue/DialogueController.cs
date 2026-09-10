using System.Collections;
using TMPro;
using UnityEngine;

public sealed class DialogueController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject continueIndicator;

    [Header("Text animation")]
    [SerializeField, Min(0.001f)]
    private float characterInterval = 0.025f;

    private DialogueSequence currentSequence;
    private Coroutine typingCoroutine;
    private int currentLineIndex;
    private bool isTyping;
    private bool isDialogueActive;

    public bool IsDialogueActive => isDialogueActive;

    private void Awake()
    {
        dialoguePanel.SetActive(false);
    }

    public void Play(DialogueSequence sequence)
    {
        if (sequence == null ||
            sequence.Lines == null ||
            sequence.Lines.Length == 0)
        {
            return;
        }

        StopCurrentTyping();

        currentSequence = sequence;
        currentLineIndex = 0;
        isDialogueActive = true;

        dialoguePanel.SetActive(true);
        ShowCurrentLine();
    }

    public void Advance()
    {
        if (!isDialogueActive)
        {
            return;
        }

        if (isTyping)
        {
            CompleteCurrentLine();
            return;
        }

        currentLineIndex++;

        if (currentLineIndex >= currentSequence.Lines.Length)
        {
            Close();
            return;
        }

        ShowCurrentLine();
    }

    public void Close()
    {
        StopCurrentTyping();

        isDialogueActive = false;
        currentSequence = null;
        dialogueText.text = string.Empty;
        speakerNameText.text = string.Empty;

        dialoguePanel.SetActive(false);
    }

    private void ShowCurrentLine()
    {
        DialogueLine line = currentSequence.Lines[currentLineIndex];

        speakerNameText.text = line.SpeakerName;
        dialogueText.text = string.Empty;
        continueIndicator.SetActive(false);

        typingCoroutine = StartCoroutine(TypeLine(line.Text));
    }

    private IEnumerator TypeLine(string text)
    {
        isTyping = true;

        foreach (char character in text)
        {
            dialogueText.text += character;
            yield return new WaitForSecondsRealtime(characterInterval);
        }

        typingCoroutine = null;
        isTyping = false;
        continueIndicator.SetActive(true);
    }

    private void CompleteCurrentLine()
    {
        StopCurrentTyping();

        DialogueLine line = currentSequence.Lines[currentLineIndex];
        dialogueText.text = line.Text;
        continueIndicator.SetActive(true);
    }

    private void StopCurrentTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
    }
}