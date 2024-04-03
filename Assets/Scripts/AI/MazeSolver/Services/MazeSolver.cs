using System;
using System.Collections.Generic;
using System.Linq;
using AI.MazeSolver.Types;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;

namespace AI.MazeSolver.Services
{
    public class MazeSolver
    {
        public class MazeStateComparer : IEqualityComparer<MazeState>
        {
            public bool Equals(MazeState x, MazeState y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                if (x.currentFloorIdx != y.currentFloorIdx) return false;
                for (int i = 0; i < x.floors[x.currentFloorIdx].map.Count; ++i)
                {
                    for (int j = 0; j < x.floors[x.currentFloorIdx].map.Count; ++j)
                    {
                        if (x.floors[x.currentFloorIdx].map[i][j] != y.floors[y.currentFloorIdx].map[i][j])
                            return false;
                    }
                }

                return true;
            }

            public int GetHashCode(MazeState obj)
            {
                return HashCode.Combine(obj.floors, obj.axie, obj.currentFloorIdx, obj.isWon);
            }
        }
        
        public static MazeSolver Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MazeSolver();
                return _instance;
            }
        }

        private static MazeSolver _instance;
        private static readonly (int, int)[] Directions = { (0, 1), (1, 0), (0, -1), (-1, 0) }; // Right, Down, Left, Up
        private int[,] map;
        private List<ItemState> itemStates;
        private HashSet<(int, int)> beingSearchedKeys = new();
        
        private Dictionary<ItemColor, int> collectedKeys = new Dictionary<ItemColor, int>();
        private Dictionary<ItemColor, int> requiredKeys = new Dictionary<ItemColor, int>();
        private Dictionary<(int, int), MazeInfo> mazeInfo;

        private List<MazeState> cachedState = new List<MazeState>();
        
        public Vector2Int SolveMazeState(MazeState state)
        {
            // if (cachedState.Exists(s => IsTwoStatesEqual(s, state)))
            //     return Vector2Int.zero;
            
            foreach (var (dx, dy) in Directions)
            {
                MazeState newState = GenerateNewState(state);
                if (newState.TestMove(dx, dy) != MoveResult.Invalid)
                {
                    newState.OnMove(dx, dy);
                    if (DFS(newState) != Vector2Int.zero && 
                        !cachedState.Exists(s => IsTwoStatesEqual(s, newState))) 
                        return new Vector2Int(dx, dy);
                }
            }

            return Vector2Int.zero;
        }

        private bool IsTwoStatesEqual(MazeState x, MazeState y)
        {
            if (x.currentFloorIdx != y.currentFloorIdx) return false;
            for (int i = 0; i < x.floors[x.currentFloorIdx].map.Count; ++i)
            {
                for (int j = 0; j < x.floors[x.currentFloorIdx].map.Count; ++j)
                {
                    if (x.floors[x.currentFloorIdx].map[i][j] != y.floors[y.currentFloorIdx].map[i][j])
                        return false;
                }
            }

            var curIdx = x.currentFloorIdx;
            return x.axie.mapX == y.axie.mapX && x.axie.mapY == y.axie.mapY;
        }

        private MazeState GenerateNewState(MazeState state)
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
        
        private Vector2Int DFS(MazeState state)
        {
            List<MazeState> visited = new List<MazeState>();
            Stack<MazeState> stack = new Stack<MazeState>();
            stack.Push(state);
            visited.Add(state);

            while (stack.Count > 0)
            {
                MazeState currentState = stack.Pop();
                foreach (var (dx, dy) in Directions)
                {
                    if (currentState.TestMove(dx, dy) == MoveResult.Invalid) continue;

                    var newState = GenerateNewState(currentState);
                    Debug.Log("Test move: " + newState.TestMove(dx, dy));
                    
                    var logs = newState.OnMove(dx, dy);
                    if (!logs.ContainsKey("action")) continue;
                    
                    string action = logs["action"];
                    Debug.Log("Action: " + action);
                    switch (action)
                    {
                        case "move":
                        case "unlockDoor":
                        case "gainKey":
                        {
                            if (!visited.Exists(st => IsTwoStatesEqual(st, newState)))
                            {
                                Debug.Log("Visit this state: " + newState.axie.mapX + "," + newState.axie.mapY);
                                stack.Push(newState);
                                visited.Add(newState);
                            }
                            break;
                        }
                        case "enterFloor":
                            return new Vector2Int(dx, dy);
                    }
                }

                if (stack.Count > 1000)
                {
                    Debug.LogError("Stack overflow");
                    break;
                }
            }

            return Vector2Int.zero;
        }

        private void Move(Vector2Int position)
        {
            if (position.Equals(GetEndPoint()))
                return;
            
            if (!mazeInfo.ContainsKey((position.x, position.y))) 
                mazeInfo.Add((position.x, position.y), new MazeInfo()
                {
                    Distance = mazeInfo[(position.x, position.y)].Distance + 1,
                    PreviousPosition = position
                });

            foreach (var (dx, dy) in Directions)
            {
                var move = CheckMoveResult(position, new Vector2Int(dx, dy));
                var nextPosition = position + new Vector2Int(dx, dy);
                // if (mazeInfo.ContainsKey((nextPosition.x, nextPosition.y))) continue;
                if (move == MoveResult.Invalid) continue;
                if (move == MoveResult.Require_Key_A)
                {
                    if (collectedKeys[ItemColor.ColorA] <= 0) continue;
                    
                    collectedKeys[ItemColor.ColorA]--;
                    mazeInfo.Add((nextPosition.x, nextPosition.y), new MazeInfo()
                    {
                        Distance = mazeInfo[(position.x, position.y)].Distance + 1,
                        PreviousPosition = position
                    });
                    Move(nextPosition);
                    collectedKeys[ItemColor.ColorA]++;
                }
                if (move == MoveResult.Require_Key_B)
                {
                    if (collectedKeys[ItemColor.ColorB] <= 0) continue;
                    
                    collectedKeys[ItemColor.ColorB]--;
                    mazeInfo.Add((nextPosition.x, nextPosition.y), new MazeInfo()
                    {
                        Distance = mazeInfo[(position.x, position.y)].Distance + 1,
                        PreviousPosition = position
                    });
                    Move(nextPosition);
                    collectedKeys[ItemColor.ColorB]++;
                }

                var itemVal = GetRoomValue(nextPosition.x, nextPosition.y);
                if (itemVal == 0) continue;
                
                var cell = CellParser.Parse(itemVal);
                if (cell.Type == CellType.Key)
                    collectedKeys[cell.Color]++;
                var lastCode = map[nextPosition.x * 2 + 1, nextPosition.y * 2 + 1];
                map[nextPosition.x * 2 + 1, nextPosition.y * 2 + 1] = MazeState.MAP_CODE_CLEAR;
                mazeInfo.Add((nextPosition.x, nextPosition.y), new MazeInfo()
                {
                    Distance = mazeInfo[(position.x, position.y)].Distance + 1,
                    PreviousPosition = position
                });
                Move(nextPosition);
                map[nextPosition.x * 2 + 1, nextPosition.y * 2 + 1] = lastCode;
            }
        }
        
        private Vector2Int MakeDecision(Vector2Int source)
        {
            mazeInfo = new Dictionary<(int, int), MazeInfo>()
            {
                {
                    (source.x, source.y), new MazeInfo()
                    {
                        Distance = 0,
                        PreviousPosition = Vector2Int.zero
                    }
                }
            };
            collectedKeys = new Dictionary<ItemColor, int>()
            {
                { ItemColor.ColorA, 0 },
                { ItemColor.ColorB, 0 }
            };
            
            // Move(source);
            
            var endPoint = GetEndPoint();
            var result = FloodFill(endPoint);
            requiredKeys = result.MazeInfo[(source.x, source.y)].RequiredKeys;
            if (requiredKeys.Count == 0)
                return result.MazeInfo[(source.x, source.y)].PreviousPosition;
            
            return Vector2Int.one;
        }
        
        private List<KeyPosition> GetRequiredKeyRequests(Dictionary<ItemColor, int> requiredKeys)
        {
            List<KeyPosition> result = new List<KeyPosition>();
            var keyPositions = GetAvailableKeys(itemStates);
            foreach (var requiredKey in requiredKeys)
            {
                ItemColor color = requiredKey.Key;
                int quantity = requiredKey.Value;
                foreach (var key in keyPositions)
                {
                    if (key.Color == color)
                    {
                        result.Add(key);
                        quantity--;
                    }

                    if (quantity <= 0) 
                        break;
                }
            }

            return result;
        }

        private List<KeyPosition> GetAvailableKeys(IEnumerable<ItemState> items)
        {
            return items
                .Where(item => item.available && !beingSearchedKeys.Contains((item.mapX, item.mapY)) &&
                               (item.code == MazeState.MAP_CODE_KEY_A || item.code == MazeState.MAP_CODE_KEY_B))
                .Select(item => new KeyPosition()
                {
                    Position = new Vector2Int(item.mapX, item.mapY),
                    Color = CellParser.Parse(item.code).Color
                })
                .ToList();
        }

        private void ImportState(MazeState state)
        {
            var floorMap = state.floors[state.currentFloorIdx].map;
            map = new int[floorMap[0].Count, floorMap.Count];
            for (int y = 0; y < floorMap.Count; ++y)
            {
                for (int x = 0; x < floorMap[0].Count; ++x)
                {
                    map[x, y] = floorMap[x][y];
                }
            }

            itemStates = new List<ItemState>(state.floors[state.currentFloorIdx].itemStates);
        }
        
        private FloodFillInfo FloodFill(Vector2Int source, bool blockedByDoors = false)
        {
            var keyPositions = new HashSet<KeyPosition>();
            var queue = new Queue<(Vector2Int, MazeInfo)>();
            var mazeInfo = new Dictionary<(int, int), MazeInfo>();
            var infoKey = (source.x, source.y);
            mazeInfo[infoKey] = new MazeInfo { Distance = 0 };
            queue.Enqueue((source, mazeInfo[infoKey]));
            
            while (queue.Count > 0)
            {
                var (currentPosition, currentInfo) = queue.Dequeue();
                foreach (var (dx, dy) in Directions)
                {
                    var moveResult = CheckMoveResult(currentPosition, new Vector2Int(dx, dy));
                    if (moveResult == MoveResult.Invalid) 
                        continue;
                    
                    var nextPosition = new Vector2Int(currentPosition.x + dx, currentPosition.y + dy);
                    var nextInfo = new MazeInfo
                    {
                        Distance = currentInfo.Distance + 1,
                        RequiredKeys = new Dictionary<ItemColor, int>(currentInfo.RequiredKeys),
                        PreviousPosition = currentPosition
                    };

                    switch (moveResult)
                    {
                        // Handle doors and keys
                        case MoveResult.Require_Key_A:
                            nextInfo.AddRequiredKey(ItemColor.ColorA); // Add key requirement for a door
                            if (blockedByDoors) continue;
                            break;
                        case MoveResult.Require_Key_B:
                            nextInfo.AddRequiredKey(ItemColor.ColorB); // Add key requirement for a door
                            if (blockedByDoors) continue;
                            break;
                        default:
                        {
                            var val = GetRoomValue(nextPosition.x, nextPosition.y);
                            var cell = CellParser.Parse(val);
                            if (cell.Type == CellType.Key)
                                keyPositions.Add(new KeyPosition()
                                {
                                    Color = cell.Color,
                                    Position = nextPosition
                                });
                            break;
                        }
                    }
                    
                    // Update mazeInfo if this path is better
                    (int, int) nextPositionKey = (nextPosition.x, nextPosition.y);
                    if (!mazeInfo.ContainsKey(nextPositionKey) || 
                        nextInfo.Distance < mazeInfo[nextPositionKey].Distance)
                    {
                        mazeInfo[nextPositionKey] = nextInfo;
                        queue.Enqueue((nextPosition, nextInfo));
                    }
                }
            }

            return new FloodFillInfo() { KeyPositions = keyPositions, MazeInfo = mazeInfo };
        }
        
        private int GetRoomValue(int x, int y)
        {
            if (x < 0 || x >= MazeState.MAP_SIZE || y < 0 || y >= MazeState.MAP_SIZE) 
                return 0;
            int roomVal = map[y * 2 + 1, x * 2 + 1];
            return roomVal;
        }
        
        private Vector2Int GetEndPoint()
        {
            for (var y = 0; y < MazeState.MAP_SIZE; y++)
            {
                for (var x = 0; x < MazeState.MAP_SIZE; x++)
                {
                    var roomVal = GetRoomValue(x, y);
                    if (roomVal == MazeState.MAP_CODE_END)
                        return new Vector2Int(x, y);
                }
            }

            return Vector2Int.zero;
        }

        private int GetDoorValue(Vector2Int currentPosition, Vector2Int delta, out Vector2Int doorPosition)
        {
            int dx = delta.x;
            int dy = delta.y;
            int wallVal;
            int colMapX, colMapY;
            if (dx != 0)
            {
                colMapX = (currentPosition.x + (dx == 1 ? 1 : 0)) * 2;
                colMapY = currentPosition.y * 2 + 1;
                wallVal = map[colMapY, colMapX];
            }
            else
            {
                colMapX = currentPosition.x * 2 + 1;
                colMapY = (currentPosition.y + (dy == 1 ? 1 : 0)) * 2;
                wallVal = map[colMapY, colMapX];
            }
            
            doorPosition = new Vector2Int(colMapX, colMapY);
            return wallVal;
        }

        private MoveResult CheckMoveResult(Vector2Int currentPosition, Vector2Int delta)
        {
            int dx = delta.x;
            int dy = delta.y;
            if ((Mathf.Abs(dx) + Mathf.Abs(dy) != 1)) return MoveResult.Invalid;
            
            int nx = currentPosition.x + dx;
            int ny = currentPosition.y + dy;
            if (nx < 0 || nx >= MazeState.MAP_SIZE || ny < 0 || ny >= MazeState.MAP_SIZE) 
                return MoveResult.Invalid;

            var wallVal = GetDoorValue(currentPosition, delta, out var doorPosition);
            if (wallVal == MazeState.MAP_CODE_CLEAR) return MoveResult.Valid;
            return wallVal switch
            {
                MazeState.MAP_CODE_DOOR_A => MoveResult.Require_Key_A,
                MazeState.MAP_CODE_DOOR_B => MoveResult.Require_Key_B,
                _ => MoveResult.Invalid
            };
        }

        private bool IsValidForFloodFill((int, int) position, Cell[,] maze)
        {
            // Implement checks for boundaries and walls
            return true;
        }
    
        public List<Vector2Int> TracePath(Dictionary<(int, int), MazeInfo> mazeInfo, Vector2Int start)
        {
            var path = new List<Vector2Int>();
            Vector2Int? position = start;
            
            var currentPositionKey = (start.x, start.y);
            if (!mazeInfo.ContainsKey(currentPositionKey))
            {
                throw new Exception("Start cell is not in maze info.");
            }
            
            while (true)
            {
                if (!mazeInfo.ContainsKey((position.Value.x, position.Value.y))) break;
                path.Add(position.Value);
                if (mazeInfo[(position.Value.x, position.Value.y)].Distance <= 0) break;
                currentPositionKey = (position.Value.x, position.Value.y);
                position = mazeInfo[currentPositionKey].PreviousPosition; // Move to the previous cell
            }
            
            return path;
        }
    }
}