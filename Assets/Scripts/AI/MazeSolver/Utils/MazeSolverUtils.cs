using System.Collections.Generic;
using UnityEngine;

namespace AI.MazeSolver.Utils
{
    public static class MazeSolverUtils
    {
        public static int GetDoorValue(List<List<int>> map, Vector2Int currentPosition, Vector2Int delta)
        {
            int dx = delta.x;
            int dy = delta.y;
            int wallVal;
            int colMapX, colMapY;
            if (dx != 0)
            {
                colMapX = (currentPosition.x + (dx == 1 ? 1 : 0)) * 2;
                colMapY = currentPosition.y * 2 + 1;
                wallVal = map[colMapY][colMapX];
            }
            else
            {
                colMapX = currentPosition.x * 2 + 1;
                colMapY = (currentPosition.y + (dy == 1 ? 1 : 0)) * 2;
                wallVal = map[colMapY][colMapX];
            }
            
            return wallVal;
        }
        
        public static List<Vector2Int> ConvertPathToActions(List<List<int>> map, List<Vector2Int> path)
        {
            List<Vector2Int> result = new List<Vector2Int>();   
            for (int i = 0; i < path.Count - 1; ++i)
            {
                // (-1, -1) is after axie picked a key
                if (path[i + 1].Equals(-Vector2Int.one) || path[i].Equals(-Vector2Int.one)) continue;
                Vector2Int delta = path[i + 1] - path[i];
                result.Add(delta);
                
                // If axie go through a door, he needs to move 2 time (one to use key, one to move)
                var moveResult = MazeSolverUtils.CheckMoveResult(map, path[i], delta);
                if (moveResult == MoveResult.Require_Key_A || moveResult == MoveResult.Require_Key_B)
                {
                    Debug.Log($"Double move due to door: {path[i].x},{path[i].y} -> {path[i + 1].y},{path[i + 1].y}");
                    result.Add(delta);
                }
            }

            return result;
        }
        
        public static List<Vector2Int> TracePath(List<List<Vector2Int>> trace, Vector2Int source, Vector2Int destination)
        {
            Vector2Int pos = destination;
            List<Vector2Int> path = new List<Vector2Int>();
            while (pos.x != source.x || pos.y != source.y)
            {
                path.Add(pos);
                pos = trace[pos.y][pos.x];
            }
            path.Add(source);
            path.Reverse();

            return path;
        }
        
        public static Vector2Int GetDoorPosition(Vector2Int currentPosition, Vector2Int delta)
        {
            int dx = delta.x;
            int dy = delta.y;
            int colMapX, colMapY;
            if (dx != 0)
            {
                colMapX = (currentPosition.x + (dx == 1 ? 1 : 0)) * 2;
                colMapY = currentPosition.y * 2 + 1;
            }
            else
            {
                colMapX = currentPosition.x * 2 + 1;
                colMapY = (currentPosition.y + (dy == 1 ? 1 : 0)) * 2;
            }

            return new Vector2Int(colMapX, colMapY);
        }

        public static Vector2Int ToRoomPosition(int x, int y)
        {
            return new Vector2Int(x * 2 + 1, y * 2 + 1);
        }
        
        public static Vector2Int ToRoomPosition(Vector2Int pos)
        {
            return ToRoomPosition(pos.x, pos.y);
        }

        public static int GetRoomValue(List<List<int>> map, Vector2Int pos)
        {
            var (x, y) = (pos.x, pos.y);
            if (x < 0 || x >= MazeState.MAP_SIZE || y < 0 || y >= MazeState.MAP_SIZE) return 0;
            return map[y * 2 + 1][x * 2 + 1];
        }

        public static bool IsValid(List<List<int>> map, Vector2Int position)
        {
            var (x, y) = (position.x, position.y);
            if (x is < 0 or >= MazeState.MAP_SIZE) return false;
            if (y is < 0 or >= MazeState.MAP_SIZE) return false;
            return true;
        }
        
        public static MoveResult CheckMoveResult(List<List<int>> map, Vector2Int currentPosition, Vector2Int delta)
        {
            int dx = delta.x;
            int dy = delta.y;
            if ((Mathf.Abs(dx) + Mathf.Abs(dy) != 1)) return MoveResult.Invalid;
            
            int nx = currentPosition.x + dx;
            int ny = currentPosition.y + dy;
            if (nx < 0 || nx >= MazeState.MAP_SIZE || ny < 0 || ny >= MazeState.MAP_SIZE) 
                return MoveResult.Invalid;

            var wallVal = GetDoorValue(map, currentPosition, delta);
            if (wallVal == MazeState.MAP_CODE_CLEAR) return MoveResult.Valid;
            return wallVal switch
            {
                MazeState.MAP_CODE_DOOR_A => MoveResult.Require_Key_A,
                MazeState.MAP_CODE_DOOR_B => MoveResult.Require_Key_B,
                _ => MoveResult.Invalid
            };
        }
        
        public static MazeState CloneState(MazeState state)
        {
            List<FloorState> copied = new List<FloorState>();
            foreach (var floorState in state.floors)
            {
                FloorState newFloorState = new FloorState
                {
                    itemStates = new List<ItemState>(),
                    doorStates = new List<DoorState>(),
                    map = new List<List<int>>()
                };

                foreach (var itemState in floorState.itemStates)
                {
                    newFloorState.itemStates.Add(new ItemState()
                    {
                        available = itemState.available,
                        code = itemState.code,
                        mapX = itemState.mapX,
                        mapY = itemState.mapY
                    });
                }

                foreach (var doorState in floorState.doorStates)
                {
                    newFloorState.doorStates.Add(new DoorState()
                    {
                        colMapX = doorState.colMapX,
                        colMapY = doorState.colMapY,
                        level = doorState.level,
                        locked = doorState.locked
                    });
                }

                foreach (var row in floorState.map)
                {
                    List<int> newRow = new List<int>(row);
                    newFloorState.map.Add(newRow);
                }

                copied.Add(newFloorState);
            }

            var newState = new MazeState()
            {
                axie = new AxieState()
                {
                    consumableItems = new Dictionary<string, int>(state.axie.consumableItems),
                    mapX = state.axie.mapX,
                    mapY = state.axie.mapY
                },
                currentFloorIdx = state.currentFloorIdx,
                floors = copied,
                isWon = state.isWon
            };

            return newState;
        }
        
        public static Vector2Int GetEndPoint(List<List<int>> map)
        {
            for (var y = 0; y < MazeState.MAP_SIZE; y++)
            {
                for (var x = 0; x < MazeState.MAP_SIZE; x++)
                {
                    var roomVal = MazeSolverUtils.GetRoomValue(map, new Vector2Int(x, y));
                    if (roomVal == MazeState.MAP_CODE_END)
                        return new Vector2Int(x, y);
                }
            }

            return Vector2Int.zero;
        }
    }
}
