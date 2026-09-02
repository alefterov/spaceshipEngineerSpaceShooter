using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single root object for the whole save file — everything the player has
/// (ship build, credits, resources) lives here so there's exactly one JSON file
/// to read/write. Uses JsonUtility, so every field must be public and every
/// nested type must be [System.Serializable] (no Dictionary — see ResourceEntry).
/// </summary>
[System.Serializable]
public class GameData
{
    [Tooltip("Bump this if the save format changes shape, to support migrations later.")]
    public int saveVersion = 1;

    [Tooltip("Soft currency — earned in-game, spent on building/repairing modules. Starting grant for a fresh save.")]
    public int credits = 1000;
    [Tooltip("Premium currency — purchasable with real money. Not spent on anything yet.")]
    public int coins;
    public List<ResourceEntry> resources = new();

    public ShipLayout playerShip = new();
}

[System.Serializable]
public class ResourceEntry
{
    public string id;
    public int amount;
}
