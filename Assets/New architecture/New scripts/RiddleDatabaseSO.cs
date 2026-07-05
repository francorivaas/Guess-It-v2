using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RiddleDatabase",
    menuName = "Guess It/Riddle Database"
)]
public class RiddleDatabaseSO : ScriptableObject
{
    public List<RiddleSO> riddles = new List<RiddleSO>();
}