using System;
using System.Collections.Generic;
using AI.MazeSolver.Types;
using UnityEngine;

namespace AI.MazeSolver.Services
{
    public class MazeBrain : MonoBehaviour
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        private MazeState mazeState;

        public void SolveState(MazeState state)
        {
            mazeState = CloneState(state);
            var map = state.floors[state.currentFloorIdx].map;
            var items = state.floors[state.currentFloorIdx].itemStates;
            var doors = state.floors[state.currentFloorIdx].doorStates;
            var source = new Vector2Int(state.axie.mapX, state.axie.mapY);
            var destination = GetEndPoint(map);
            Debug.Log($"Solve on floor {state.currentFloorIdx}, source={source.x},{source.y}");
            var path = Bfs(map, items, doors, source, destination);
            string log = "";
            foreach (var pos in path) log += $"({pos.x},{pos.y}) -> ";
            Debug.Log(log);
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
        
        private void Start()
        {
            var sampleState = new MazeState();
            sampleState.LoadMaps(MapPool.FLOOR_MAPS);
            sampleState.currentFloorIdx = 2;
            sampleState.axie.mapX = 0;
            sampleState.axie.mapY = 3;
            SolveState(sampleState);
        }

        private List<Vector2Int> TracePath(List<List<Vector2Int>> trace, Vector2Int source, Vector2Int destination)
        {
            Vector2Int pos = destination;
            List<Vector2Int> path = new List<Vector2Int>();
            while (pos.x != source.x && pos.y != source.y)
            {
                path.Add(pos);
                pos = trace[pos.y][pos.x];
            }
            path.Add(source);

            return path;
        }
        
        private List<Vector2Int> Bfs(
            List<List<int>> map, 
            List<ItemState> items, 
            List<DoorState> doors, 
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
                    cell.Path.Add(pos);
                    var logP = "";
                    foreach (var p in cell.Path) logP += $"{p.x},{p.y} -> ";
                    Debug.Log(logP);
                    path = cell.Path;
                    break;
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
                        var subPath = Bfs(map, items, doors, pos, destination);
                        // path.AddRange(subPath);
                        
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
                    // Debug.Log("Check move result: " + checkMove);
                    if (checkMove != MoveResult.Valid) continue;
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
