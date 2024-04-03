using System;
using System.Collections;
using System.Collections.Generic;
using AI.MazeSolver.Types;
using UnityEngine;

namespace AI.MazeSolver.Services
{
    public class MazeBrain
    {
        public GameMazeManager MazeManager { get; set; }
        public static MazeBrain Instance => _instance ??= new MazeBrain();
        
        private static MazeBrain _instance;
        private static readonly Vector2Int[] Directions =
            { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        private Coroutine updateCoroutine = null;
        
        List<Vector2Int> cachedResults;
        private int cachedMazeFloorIndex = -1;
        private int currentActionIndex;
        
        public Vector2Int SolveState(MazeState mazeState)
        {
            var state = CloneState(mazeState);
            if (cachedMazeFloorIndex == state.currentFloorIdx && cachedResults != null) 
                return currentActionIndex < cachedResults.Count ? cachedResults[currentActionIndex++] : Vector2Int.zero;
            
            var map = state.floors[state.currentFloorIdx].map;
            var items = state.floors[state.currentFloorIdx].itemStates;
            var doors = state.floors[state.currentFloorIdx].doorStates;
            var source = new Vector2Int(state.axie.mapX, state.axie.mapY);
            var destination = GetEndPoint(map);
            var ownedItems = state.axie.consumableItems;
            Debug.Log($"Solve on floor {state.currentFloorIdx}, source={source.x},{source.y}");
            List<Vector2Int> path = Bfs(map, items, doors, ownedItems, source, destination);

            #region Debug log
            string log = "";
            foreach (var pos in path) log += $"({pos.x},{pos.y}) -> ";
            Debug.Log(log);

            var actions = ConvertPathToActions(mazeState.floors[mazeState.currentFloorIdx].map, path);
            log = "";
            foreach (var d in actions) log += $"({d.x},{d.y}) -> ";
            Debug.Log(log);
            #endregion

            cachedResults = actions;
            cachedMazeFloorIndex = state.currentFloorIdx;
            currentActionIndex = 1;

            return actions[0];
        }

        private List<Vector2Int> ConvertPathToActions(List<List<int>> map, List<Vector2Int> path)
        {
            List<Vector2Int> result = new List<Vector2Int>();   
            for (int i = 0; i < path.Count - 1; ++i)
            {
                // (-1, -1) is after axie picked a key
                if (path[i + 1].Equals(-Vector2Int.one) || path[i].Equals(-Vector2Int.one)) continue;
                Vector2Int delta = path[i + 1] - path[i];
                result.Add(delta);
                
                // If axie go through a door, he needs to move 2 time (one to use key, one to move)
                var moveResult = CheckMoveResult(map, path[i], delta);
                if (moveResult == MoveResult.Require_Key_A || moveResult == MoveResult.Require_Key_B)
                {
                    Debug.Log($"Double move due to door: {path[i].x},{path[i].y} -> {path[i + 1].y},{path[i + 1].y}");
                    result.Add(delta);
                }
            }

            return result;
        }

        private Vector2Int GetEndPoint(List<List<int>> map)
        {
            for (var y = 0; y < MazeState.MAP_SIZE; y++)
            {
                for (var x = 0; x < MazeState.MAP_SIZE; x++)
                {
                    var roomVal = GetRoomValue(map, new Vector2Int(x, y));
                    if (roomVal == MazeState.MAP_CODE_END)
                        return new Vector2Int(x, y);
                }
            }

            return Vector2Int.zero;
        }

        public void ScheduleUpdate(MonoBehaviour monoBehaviour)
        {
            if (updateCoroutine != null) return;
            updateCoroutine = monoBehaviour.StartCoroutine(UpdateSimulation());
        }

        private void ResetCache()
        {
            cachedMazeFloorIndex = -1;
        }
        
        private void Update()
        {
            if (Input.anyKeyDown)
            {
                Debug.Log("[Maze Brain]: User takes control, discard all computation");
                ResetCache();
            }
        }
        
        private IEnumerator UpdateSimulation()
        {
            while (true)
            {
                Update();
                yield return null;
            }
        }

        private List<Vector2Int> TracePath(List<List<Vector2Int>> trace, Vector2Int source, Vector2Int destination)
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
        
        private List<Vector2Int> Bfs(
            List<List<int>> map, 
            List<ItemState> items, 
            List<DoorState> doors, 
            Dictionary<string, int> ownedItems,
            Vector2Int source, 
            Vector2Int destination)
        {
            Debug.Log($"Start from {source.x},{source.y}");
            var visited = new List<List<bool>>();
            for (int y = 0; y < MazeState.MAP_SIZE; ++y)
            {
                var row = new List<bool>();
                for (int x = 0; x < MazeState.MAP_SIZE; ++x)
                {
                    row.Add(false);
                }
                visited.Add(row);
            }

            var trace = new List<List<Vector2Int>>();
            for (int y = 0; y < MazeState.MAP_SIZE; ++y)
            {
                var row = new List<Vector2Int>();
                for (int x = 0; x < MazeState.MAP_SIZE; ++x)
                {
                    row.Add(new Vector2Int(-1, -1));
                }
                trace.Add(row);
            }

            Queue<MazeCell> queue = new Queue<MazeCell>();
            queue.Enqueue(new MazeCell() { Position = source });
            List<Vector2Int> path = new List<Vector2Int>();
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                var pos = cell.Position;
                if (!IsValid(map, pos)) continue;
                if (visited[pos.y][pos.x]) continue;
                if (pos.Equals(destination))
                {
                    Debug.Log("Escaped");
                    path = TracePath(trace, source, destination);
                    return path;
                }
                
                visited[pos.y][pos.x] = true;
                cell.Path.Add(pos);
                
                // If there is a key
                int cellCode = GetRoomValue(map, pos);
                if (cellCode == MazeState.MAP_CODE_KEY_A || cellCode == MazeState.MAP_CODE_KEY_B)
                {
                    // Pick the key, then try another path, then backtrack to here and conclude
                    Debug.Log($"Pick key {cellCode}");
                    var keyObject = items.Find(item =>
                        item.code == cellCode &&
                        item.mapX == pos.x &&
                        item.mapY == pos.y);
                    
                    // Try the key on each matching door and find path
                    foreach (var door in doors)
                    {
                        // Door is already unlocked
                        if (!door.locked) continue;
                        
                        // Not match
                        var doorCode = door.level + MazeState.MAP_CODE_DOOR_A;
                        if (doorCode == MazeState.MAP_CODE_DOOR_A && cellCode != MazeState.MAP_CODE_KEY_A) continue;
                        if (doorCode == MazeState.MAP_CODE_DOOR_B && cellCode != MazeState.MAP_CODE_KEY_B) continue;
                        
                        // Match key and door, unlock door
                        keyObject.available = false;
                        door.locked = false;

                        var keyRoomPos = ToRoomPosition(keyObject.mapX, keyObject.mapY);
                        map[keyRoomPos.y][keyRoomPos.x] = MazeState.MAP_CODE_CLEAR;
                        map[door.colMapY][door.colMapX] = MazeState.MAP_CODE_CLEAR;
                        
                        Debug.Log($"Try key {keyObject.code} on door {doorCode}");
                        
                        // Try to find way from here
                        var subPath = Bfs(map, items, doors, new Dictionary<string, int>(ownedItems), pos, destination);
                        
                        // If this way can reach the final destination
                        if (subPath.Count > 0)
                        {
                            // Concat path from first source to this position and path from this position to final destination
                            path = TracePath(trace, source, pos);
                            path.Add(new Vector2Int(-1, -1));
                            path.AddRange(subPath);
                            return path;
                        }
                        
                        // Backtracking, rewind everything
                        keyObject.available = true;
                        door.locked = true;
                        map[keyRoomPos.y][keyRoomPos.x] = keyObject.code;
                        map[door.colMapY][door.colMapX] = doorCode;
                    }
                }

                foreach (var d in Directions)
                {
                    var newPos = pos + d;
                    if (!IsValid(map, newPos)) continue;
                    if (visited[newPos.y][newPos.x]) continue;
                    var checkMove = CheckMoveResult(map, pos, d);
                    if (checkMove == MoveResult.Invalid) continue;

                    // Collide with doors, try using current own keys
                    var doorPos = GetDoorPosition(map, pos, d);
                    if (checkMove == MoveResult.Require_Key_A)
                    {
                        if (!ownedItems.ContainsKey("key-a") || ownedItems["key-a"] <= 0) continue;
                        map[doorPos.y][doorPos.x] = MazeState.MAP_CODE_CLEAR;
                        ownedItems["key-a"]--;
                    }
                    else if (checkMove == MoveResult.Require_Key_B)
                    {
                        if (!ownedItems.ContainsKey("key-b") || ownedItems["key-b"] <= 0) continue;
                        map[doorPos.y][doorPos.x] = MazeState.MAP_CODE_CLEAR;
                        ownedItems["key-b"]--;
                    }

                    trace[newPos.y][newPos.x] = pos;
                    // Debug.Log($"Source {source.x},{source.y}, From {pos.x},{pos.y}: Visit {newPos.x},{newPos.y}");
                    queue.Enqueue(new MazeCell() { Position = newPos, Path = cell.Path });
                }
            }

            return path;
        }

        private MoveResult CheckMoveResult(List<List<int>> map, Vector2Int currentPosition, Vector2Int delta)
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
        
        private int GetDoorValue(List<List<int>> map, Vector2Int currentPosition, Vector2Int delta)
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
        
        private Vector2Int GetDoorPosition(List<List<int>> map, Vector2Int currentPosition, Vector2Int delta)
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

        private Vector2Int ToRoomPosition(int x, int y)
        {
            return new Vector2Int(x * 2 + 1, y * 2 + 1);
        }
        
        private Vector2Int ToRoomPosition(Vector2Int pos)
        {
            return ToRoomPosition(pos.x, pos.y);
        }

        private int GetRoomValue(List<List<int>> map, Vector2Int pos)
        {
            var (x, y) = (pos.x, pos.y);
            if (x < 0 || x >= MazeState.MAP_SIZE || y < 0 || y >= MazeState.MAP_SIZE) return 0;
            return map[y * 2 + 1][x * 2 + 1];
        }

        private bool IsValid(List<List<int>> map, Vector2Int position)
        {
            var (x, y) = (position.x, position.y);
            if (x is < 0 or >= MazeState.MAP_SIZE) return false;
            if (y is < 0 or >= MazeState.MAP_SIZE) return false;
            return true;
        }
        
        private MazeState CloneState(MazeState state)
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
    }
}
