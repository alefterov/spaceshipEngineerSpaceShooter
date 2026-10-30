using System.Collections.Generic;
using UnityEngine;

/// <summary>The list of all tasks of the rewards panel, in the order they are meant to be shown (the panel puts the ready-to-claim ones
/// first and the claimed ones last). Assigned to GameDataManager.</summary>
[CreateAssetMenu(menuName = "ShipBuilder/Task Database", fileName = "TaskDatabase")]
public class TaskDatabase : ScriptableObject
{
    public List<TaskDefinition> tasks = new();
}
