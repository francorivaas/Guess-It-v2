using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestionDatabase", menuName = "GuessIt/Database")]
public class QuestionDatabase : ScriptableObject
{
    public List<QuestionData> questions;
}