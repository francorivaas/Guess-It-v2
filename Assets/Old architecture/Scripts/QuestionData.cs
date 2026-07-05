using UnityEngine;

[CreateAssetMenu(fileName = "Question", menuName = "GuessIt/Question")]
public class QuestionData : ScriptableObject
{
    public string answer;

    [TextArea]
    public string hint1;

    [TextArea]
    public string hint2;

    [TextArea]
    public string hint3;

    [TextArea]
    public string hint4;

    [TextArea]
    public string hint5;
}