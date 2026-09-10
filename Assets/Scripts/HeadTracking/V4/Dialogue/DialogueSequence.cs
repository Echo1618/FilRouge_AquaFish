using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewDialogue",
    menuName = "Dialogue/Dialogue Sequence")]
public sealed class DialogueSequence : ScriptableObject
{
    [SerializeField] private DialogueLine[] lines;

    public DialogueLine[] Lines => lines;
}

[Serializable]
public sealed class DialogueLine
{
    [SerializeField] private string speakerName;

    [TextArea(2, 6)]
    [SerializeField] private string text;

    public string SpeakerName => speakerName;
    public string Text => text;
}