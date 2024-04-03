using System;
using System.Collections.Generic;
using UnityEngine;

namespace AI.MazeSolver.Types
{
    public enum CellType
    {
        Empty,
        Wall,
        Door,
        Key,
        Escape
    }

    public enum ItemColor
    {
        NoFill,
        ColorA,
        ColorB
    }

    public static class CellParser
    {
        public static Cell Parse(int mapCode)
        {
            return mapCode switch
            {
                MazeState.MAP_CODE_CLEAR => new Cell() { Type = CellType.Empty, Color = ItemColor.NoFill },
                MazeState.MAP_CODE_WALL => new Cell() { Type = CellType.Wall, Color = ItemColor.NoFill },
                MazeState.MAP_CODE_DOOR_A => new Cell() { Type = CellType.Door, Color = ItemColor.ColorA },
                MazeState.MAP_CODE_DOOR_B => new Cell() { Type = CellType.Door, Color = ItemColor.ColorB },
                MazeState.MAP_CODE_KEY_A => new Cell() { Type = CellType.Key, Color = ItemColor.ColorA },
                MazeState.MAP_CODE_KEY_B => new Cell() { Type = CellType.Key, Color = ItemColor.ColorB },
                MazeState.MAP_CODE_START => new Cell() { Type = CellType.Empty, Color = ItemColor.NoFill },
                MazeState.MAP_CODE_END => new Cell() { Type = CellType.Escape, Color = ItemColor.NoFill },
                _ => throw new ArgumentException("No CellType for map code " + mapCode)
            };
        }
    }
    
    public class Cell
    {
        public CellType Type { get; set; }
        public ItemColor Color { get; set; } // Represents the color of the door or key
    }
    
    public class MazeInfo
    {
        public int Distance { get; set; } = int.MaxValue; // Distance to the exit
        public Dictionary<ItemColor, int> RequiredKeys { get; set; } = new(); // Keys required to reach the exit
        public Vector2Int PreviousPosition { get; set; }

        public void AddRequiredKey(ItemColor color, int quantity = 1)
        {
            if (!RequiredKeys.ContainsKey(color))
                RequiredKeys.Add(color, 0);
            RequiredKeys[color] += quantity;
        }
    }

    public class KeyPosition
    {
        public ItemColor Color { get; set; }
        public Vector2Int Position { get; set; }
    }

    public class FloodFillInfo
    {
        public HashSet<KeyPosition> KeyPositions { get; set; } = new HashSet<KeyPosition>();
        public Dictionary<(int, int), MazeInfo> MazeInfo { get; set; }
    }

    public enum Action
    {
        Move,
        UnlockDoor
    }
    
    public class AxieAction
    {
        public Action Action { get; set; } = Action.Move;
        public Vector2Int Delta { get; set; } = Vector2Int.zero;
    }
    
    public class MazeCell
    {
        public Vector2Int Position { get; set; }
        public List<Vector2Int> Path { get; set; } = new List<Vector2Int>();
    }
}

