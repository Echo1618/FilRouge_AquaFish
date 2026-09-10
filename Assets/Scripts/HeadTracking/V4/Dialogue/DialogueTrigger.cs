using UnityEngine;

public sealed class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private DialogueController dialogueController;
    [SerializeField] private DialogueSequence dialogue;

    public void StartDialogue()
    {
        if (dialogueController == null)
        {
            Debug.LogWarning(
                $"No DialogueController assigned to {name}.",
                this);

            return;
        }

        dialogueController.Play(dialogue);
    }
}