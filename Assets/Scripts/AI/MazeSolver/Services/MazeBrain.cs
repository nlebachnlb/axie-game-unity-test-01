using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI.MazeSolver.Utils;

namespace AI.MazeSolver.Services
{
    public class MazeBrain
    {
        public static MazeBrain Instance => _instance ??= new MazeBrain();
        
        private static MazeBrain _instance;
        private static readonly Vector2Int[] Directions =
            { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        private Coroutine updateCoroutine = null;

        private List<Vector2Int> cachedResults;
        private int cachedMazeFloorIndex = -1;
        private int currentActionIndex;
        
        public Vector2Int SolveState(MazeState mazeState)
        {
            var state = MazeSolverUtils.CloneState(mazeState);
            if (cachedMazeFloorIndex == state.currentFloorIdx && cachedResults != null) 
                return currentActionIndex < cachedResults.Count ? cachedResults[currentActionIndex++] : Vector2Int.zero;
            
            var map = state.floors[state.currentFloorIdx].map;
            var items = state.floors[state.currentFloorIdx].itemStates;
            var doors = state.floors[state.currentFloorIdx].doorStates;
            var source = new Vector2Int(state.axie.mapX, state.axie.mapY);
            var destination = MazeSolverUtils.GetEndPoint(map);
            var ownedItems = state.axie.consumableItems;
            Debug.Log($"Solve on floor {state.currentFloorIdx}, source={source.x},{source.y}");
            var path = Bfs(map, items, doors, ownedItems, source, destination);
            var actions = MazeSolverUtils.ConvertPathToActions(mazeState.floors[mazeState.currentFloorIdx].map, path);

            #region Debug log
            string log = "";
            foreach (var pos in path) log += $"({pos.x},{pos.y}) -> ";
            Debug.Log(log);

            log = "";
            foreach (var d in actions) log += $"({d.x},{d.y}) -> ";
            Debug.Log(log);
            #endregion

            cachedResults = actions;
            cachedMazeFloorIndex = state.currentFloorIdx;
            currentActionIndex = 0;

            return currentActionIndex < cachedResults.Count ? cachedResults[currentActionIndex++] : Vector2Int.zero;
        }

        #region Update Simulation
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
        #endregion
        
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

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(source);
            List<Vector2Int> path = new List<Vector2Int>();
            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                if (!MazeSolverUtils.IsValid(map, pos)) continue;
                if (visited[pos.y][pos.x]) continue;
                if (pos.Equals(destination))
                {
                    Debug.Log("Escaped");
                    path = MazeSolverUtils.TracePath(trace, source, destination);
                    return path;
                }
                
                visited[pos.y][pos.x] = true;
                
                // If there is a key
                int cellCode = MazeSolverUtils.GetRoomValue(map, pos);
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

                        var keyRoomPos = MazeSolverUtils.ToRoomPosition(keyObject.mapX, keyObject.mapY);
                        map[keyRoomPos.y][keyRoomPos.x] = MazeState.MAP_CODE_CLEAR;
                        map[door.colMapY][door.colMapX] = MazeState.MAP_CODE_CLEAR;
                        
                        Debug.Log($"Try key {keyObject.code} on door {doorCode}");
                        
                        // Try to find way from here
                        var subPath = Bfs(map, items, doors, new Dictionary<string, int>(ownedItems), pos, destination);
                        
                        // If this way can reach the final destination
                        if (subPath.Count > 0)
                        {
                            // Concat path from first source to this position and path from this position to final destination
                            path = MazeSolverUtils.TracePath(trace, source, pos);
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
                    if (!MazeSolverUtils.IsValid(map, newPos)) continue;
                    if (visited[newPos.y][newPos.x]) continue;
                    var checkMove = MazeSolverUtils.CheckMoveResult(map, pos, d);
                    if (checkMove == MoveResult.Invalid) continue;

                    // Collide with doors, try using current own keys
                    var doorPos = MazeSolverUtils.GetDoorPosition(pos, d);
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
                    queue.Enqueue(newPos);
                }
            }

            return path;
        }
    }
}
