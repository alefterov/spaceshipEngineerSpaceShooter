using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ShipBuilder/Task Database", fileName = "TaskDatabase")]
public class TaskDatabase : ScriptableObject
{
    public List<TaskDefinition> tasks = new();
}
