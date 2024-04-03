using System.Collections;
using System.Collections.Generic;
using AI.MazeSolver.Services;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    [SerializeField] private GameMazeManager maze;
    
    // Start is called before the first frame update
    void Start()
    {
        MazeBrain.Instance.ScheduleUpdate(this);
    }
}
