using System.Collections.Generic;
using UnityEngine;


public enum RoomType
{
    Start,
    Hall,
    Beginner,
    Intermediate,
    Advanced,
    Rest,
    NPC,
    Boss
}
[System.Serializable]
public class DungeonNode
{
    public int id;
    public RoomType type;
    public int depth;
    public bool isMainPath;
    [System.NonSerialized]
    public List<DungeonNode> connections = new List<DungeonNode>();
    [System.NonSerialized]
    public Transform roomTransform;

    public DungeonNode(int id,RoomType type,int depth,bool isMainPath)
    {
        this.id = id;
        this.type = type;
        this.depth = depth;
        this.isMainPath = isMainPath;
    }
}
