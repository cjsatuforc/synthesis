﻿using UnityEngine;
using System.Collections;
using BulletUnity;
using BulletSharp;
using System;
using System.Collections.Generic;
using BulletSharp.SoftBody;
using UnityEngine.SceneManagement;
using System.IO;
using Assets.Scripts.FEA;
using Assets.Scripts.FSM;
using System.Linq;
using Assets.Scripts.BUExtensions;
using Assets.Scripts;
using UnityEngine.UI;
using Assets.Scripts.Utils;

/// <summary>
/// This is the main class of the simulator; it handles all the initialization of robot and field objects within the simulator.
/// Handles replay tracking and loading
/// Handles interfaces between the SimUI and the active robot such as resetting, orienting, etc.
/// </summary>
public class MultiplayerState : SimState
{
    // TODO: Create a BaseRobot class and derive everything from that.

    private const int SolverIterations = 100;

    private BPhysicsWorld physicsWorld;

    private UnityPacket unityPacket;

    private List<RobotBase> robots;
    public RobotBase ActiveRobot { get; private set; }

    private DynamicCamera dynamicCamera;
    public GameObject DynamicCameraObject;

    private GameObject fieldObject;
    private UnityFieldDefinition fieldDefinition;

    private string fieldPath;
    private string robotPath;

    public List<RobotBase> SpawnedRobots { get; private set; }
    private const int MAX_ROBOTS = 6;

    public bool IsMetric;

    /// <summary>
    /// Called when the script instance is being initialized.
    /// Initializes the bullet physics environment
    /// </summary>
    public override void Awake()
    {
        Environment.SetEnvironmentVariable("MONO_REFLECTION_SERIALIZER", "yes");
        GImpactCollisionAlgorithm.RegisterAlgorithm((CollisionDispatcher)BPhysicsWorld.Get().world.Dispatcher);
        BPhysicsWorld.Get().DebugDrawMode = DebugDrawModes.DrawWireframe | DebugDrawModes.DrawConstraints | DebugDrawModes.DrawConstraintLimits;
        BPhysicsWorld.Get().DoDebugDraw = false;
        ((DynamicsWorld)BPhysicsWorld.Get().world).SolverInfo.NumIterations = SolverIterations;
    }

    /// <summary>
    /// Called after Awake() when the script instance is enabled.
    /// Initializes variables then loads the field and robot as well as setting up replay features.
    /// </summary>
    public override void Start()
    {
        AppModel.ClearError();

        //getting bullet physics information
        physicsWorld = BPhysicsWorld.Get();
        ((DynamicsWorld)physicsWorld.world).SetInternalTickCallback(BPhysicsTickListener.Instance.PhysicsTick);

        //setting up raycast robot tick callback
        BPhysicsTickListener.Instance.OnTick -= BRobotManager.Instance.UpdateRaycastRobots;
        BPhysicsTickListener.Instance.OnTick += BRobotManager.Instance.UpdateRaycastRobots;

        //starts a new instance of unity packet which receives packets from the driver station
        unityPacket = new UnityPacket();
        unityPacket.Start();

        SpawnedRobots = new List<RobotBase>();

        //loads all the controls
        Controls.Load();

        if (!LoadField(PlayerPrefs.GetString("simSelectedField")))
        {
            AppModel.ErrorToMenu("Could not load field: " + PlayerPrefs.GetString("simSelectedField") + "\nHas it been moved or deleted?)");
            return;
        }

        Debug.Log("Robot Type Manager isMixAndMatch:" + RobotTypeManager.IsMixAndMatch);
        if (!LoadRobot(PlayerPrefs.GetString("simSelectedRobot"), RobotTypeManager.IsMixAndMatch))
        {
            AppModel.ErrorToMenu("Could not load robot: " + PlayerPrefs.GetString("simSelectedRobot") + "\nHas it been moved or deleted?)");
            return;
        }

        //initializes the dynamic camera
        DynamicCameraObject = GameObject.Find("Main Camera");
        dynamicCamera = DynamicCameraObject.AddComponent<DynamicCamera>();
        DynamicCamera.MovingEnabled = true;

        IsMetric = PlayerPrefs.GetString("Measure").Equals("Metric") ? true : false;
    }

    /// <summary>
    /// Called every step of the program to listen to input commands for various features
    /// </summary>
    public override void Update()
    {
        if (ActiveRobot == null)
        {
            AppModel.ErrorToMenu("Robot instance not valid.");
            return;
        }

        // Toggles between the different camera states if the camera toggle button is pressed
        if ((InputControl.GetButtonDown(Controls.buttons[0].cameraToggle)))
        {
            if (DynamicCameraObject.activeSelf && DynamicCamera.MovingEnabled)
            {
                dynamicCamera.ToggleCameraState(dynamicCamera.cameraState);
            }
        }
    }

    /// <summary>
    /// Called at a fixed rate - updates robot packet information.
    /// </summary>
    public override void FixedUpdate()
    {
        //This line is essential for the reset to work accurately
        //robotCameraObject.transform.position = activeRobot.transform.GetChild(0).transform.position;
        if (ActiveRobot == null)
        {
            AppModel.ErrorToMenu("Robot instance not valid.");
            return;
        }

        SendRobotPackets();
    }

    /// <summary>
    /// Loads the field from a given directory
    /// </summary>
    /// <param name="directory">field directory</param>
    /// <returns>whether the process was successful</returns>
    bool LoadField(string directory)
    {
        fieldPath = directory;

        fieldObject = new GameObject("Field");

        FieldDefinition.Factory = delegate (Guid guid, string name)
        {
            return new UnityFieldDefinition(guid, name);
        };

        if (!File.Exists(directory + "\\definition.bxdf"))
            return false;

        string loadResult;
        fieldDefinition = (UnityFieldDefinition)BXDFProperties.ReadProperties(directory + "\\definition.bxdf", out loadResult);
        Debug.Log(loadResult);
        fieldDefinition.CreateTransform(fieldObject.transform);
        return fieldDefinition.CreateMesh(directory + "\\mesh.bxda");
    }

    /// <summary>
    /// Loads a new robot from a given directory
    /// </summary>
    /// <param name="directory">robot directory</param>
    /// <returns>whether the process was successful</returns>
    public bool LoadRobot(string directory, bool isMixAndMatch)
    {
        if (SpawnedRobots.Count < MAX_ROBOTS)
        {
            if (isMixAndMatch)
            {
                robotPath = RobotTypeManager.RobotPath;
            }
            else
            {
                robotPath = directory;
            }

            GameObject robotObject = new GameObject("Robot");
            RobotBase robot = robotObject.AddComponent<RobotBase>();

            //Initialiezs the physical robot based off of robot directory. Returns false if not sucessful
            if (!robot.InitializeRobot(robotPath)) return false;

            //If this is the first robot spawned, then set it to be the active robot and initialize the robot camera on it
            if (ActiveRobot == null)
            {
                ActiveRobot = robot;
            }

            SpawnedRobots.Add(robot);

            return true;
        }
        return false;
    }

    /// <summary>
    /// Resumes the normal simulation and exits the replay mode, showing all UI elements again
    /// </summary>
    public override void Resume()
    {
    }

    /// <summary>
    /// Pauses the normal simulation for rpelay mode by disabling tracking of physics objects and disabling UI elements
    /// </summary>
    public override void Pause()
    {
    }

    /// <summary>
    /// Sends the received packets to the active robot
    /// </summary>
    private void SendRobotPackets()
    {
        ActiveRobot.Packet = unityPacket.GetLastPacket();
        foreach (RobotBase robot in SpawnedRobots)
        {
            if (robot != ActiveRobot) robot.Packet = null;
        }
    }
}