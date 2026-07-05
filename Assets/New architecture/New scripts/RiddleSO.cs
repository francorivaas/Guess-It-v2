using UnityEngine;

[CreateAssetMenu(
    fileName = "Riddle",
    menuName = "Guess It/Riddle"
)]
public class RiddleSO : ScriptableObject
{
    public string id;
    public string category;
    public RiddleDifficulty difficulty;
    public string answer;
    public string[] acceptedAnswers;

    [TextArea]
    public string[] hints = new string[5];
}