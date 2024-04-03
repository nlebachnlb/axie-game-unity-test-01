# Axie Game Unity Test 1
## Code Structure
The supplement codebase is wrapped as a module named MazeSolver and placed in namespace AI.MazeSolver
### Namespaces
There are currently 2 namespaces:
#### AI.MazeSolver.Core
Contains all the logic and algorithm to solve a maze state:
* **`MazeBrain`**: a singleton that runs algorithm to solve maze state. 

To avoid touching existing things, it is designed as a separated class (not MonoBehaviour) that can simulate the Update method by starting Coroutine.
#### AI.MazeSolver.Utils
Contains all the things to serve algorithm, make it more readable than just a heap of expressions and number.

### Workflow
MazeBrain supply a public method to get calculated result `SolveState(MazeState state)`. This will be called from `OnSimulated()` method.

The result is computed and cached at the first call of `OnSimulated()` after user releases control. So at next calls of `SolveState`, it returns result immediately.

Cache is discarded whenever user take back the control so result can be re-computed.