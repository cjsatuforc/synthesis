﻿using Assets.Scripts.BUExtensions;
using Assets.Scripts.FSM;
using BulletSharp;
using BulletUnity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[NetworkSettings(channel = 0, sendInterval = 0.025f)]
public class NetworkRobot : RobotBase
{
    const float CorrectionPositionThreshold = 0.05f;
    const float CorrectionRotationThreshold = 15.0f;

    BRigidBody[] rigidBodies;
    NetworkMesh[] networkMeshes;
    bool correctionEnabled = true;
    bool canSendUpdate = true;

    MultiplayerState state;

    /// <summary>
    /// Loads the Robot and initializes it on the network.
    /// </summary>
    private void Start()
    {
        string directory = PlayerPrefs.GetString("simSelectedRobot");

        if (!string.IsNullOrEmpty(directory))
        {
            state = StateMachine.Instance.FindState<MultiplayerState>();
            state.LoadRobot(this, directory, isLocalPlayer);
            rigidBodies = GetComponentsInChildren<BRigidBody>();
            networkMeshes = new NetworkMesh[rigidBodies.Length];

            for (int i = 0; i < rigidBodies.Length; i++)
                networkMeshes[i] = rigidBodies[i].gameObject.AddComponent<NetworkMesh>();

            UpdateRobotInfo();
        }
    }

    /// <summary>
    /// Ensures that the robot is awake and enables sending the next packet to the server.
    /// </summary>
    private void Update()
    {
        if (Input.GetKey(KeyCode.E))
            correctionEnabled = true;
        else if (Input.GetKey(KeyCode.D))
            correctionEnabled = false;

        BRigidBody rigidBody = GetComponentInChildren<BRigidBody>();

        if (rigidBody == null)
            return;

        UpdateRobotPhysics();

        canSendUpdate = true;
    }

    /// <summary>
    /// Updates pwm information and sends robot information to the server.
    /// </summary>
    private void FixedUpdate()
    {
        BRigidBody rigidBody = GetComponentInChildren<BRigidBody>();

        if (rigidBody == null)
            return;

        if (isLocalPlayer)
        {
            float[] pwm = DriveJoints.GetPwmValues(Packet == null ? emptyDIO : Packet.dio, ControlIndex, IsMecanum);

            UpdateRobotInfo(pwm);

            if (correctionEnabled)
            {
                if (isServer)
                    RpcUpdateRobotInfo(pwm);
                else
                    CmdUpdateRobotInfo(pwm);
            }
            
            if (canSendUpdate && correctionEnabled)
            {
                float[] transforms = new float[rigidBodies.Length * 13];

                int i = 0;
                foreach (BRigidBody rb in rigidBodies)
                {
                    float[] currentTransform = SerializeTransform(rb.GetCollisionObject().WorldTransform);

                    for (int j = 0; j < currentTransform.Length; j++)
                        transforms[i * 13 + j] = currentTransform[j];

                    float[] currentLinearVelocity = rb.GetCollisionObject().InterpolationLinearVelocity.ToArray();

                    for (int j = 0; j < currentLinearVelocity.Length; j++)
                        transforms[i * 13 + currentTransform.Length + j] = currentLinearVelocity[j];

                    float[] currentAngularVelocity = rb.GetCollisionObject().InterpolationAngularVelocity.ToArray();

                    for (int j = 0; j < currentAngularVelocity.Length; j++)
                        transforms[i * 13 + currentTransform.Length + currentLinearVelocity.Length + j] = currentAngularVelocity[j];

                    i++;
                }

                if (isServer)
                    RpcUpdateTransforms(transforms);
                else
                    CmdUpdateTransforms(transforms);
            }

            canSendUpdate = false;
        }
    }

    /// <summary>
    /// Updates pwm information on the server.
    /// </summary>
    /// <param name="pwm"></param>
    [Command]
    private void CmdUpdateRobotInfo(float[] pwm)
    {
        RemoteUpdateRobotInfo(pwm);
        RpcUpdateRobotInfo(pwm);
    }

    /// <summary>
    /// Updates pwm information on the client.
    /// </summary>
    /// <param name="pwm"></param>
    [ClientRpc]
    private void RpcUpdateRobotInfo(float[] pwm)
    {
        if (!isLocalPlayer)
            RemoteUpdateRobotInfo(pwm);
    }

    /// <summary>
    /// Sends the pwm information of this robot to the server and other clients.
    /// </summary>
    /// <param name="pwm"></param>
    private void RemoteUpdateRobotInfo(float[] pwm)
    {
        if (rootNode != null && ControlsEnabled)
            DriveJoints.UpdateAllMotors(rootNode, pwm);

        UpdateStats();
    }

    /// <summary>
    /// Updates the robot position on the server.
    /// </summary>
    /// <param name="transforms"></param>
    [Command]
    void CmdUpdateTransforms(float[] transforms)
    {
        UpdateTransforms(transforms);
        RpcUpdateTransforms(transforms);
    }

    /// <summary>
    /// Updates the robot position on the client.
    /// </summary>
    /// <param name="transforms"></param>
    [ClientRpc]
    void RpcUpdateTransforms(float[] transforms)
    {
        UpdateTransforms(transforms);
    }

    /// <summary>
    /// Updates the robot's transform from the given array of transform information.
    /// </summary>
    /// <param name="transforms"></param>
    void UpdateTransforms(float[] transforms)
    {
        if (isLocalPlayer || !correctionEnabled)
            return;

        BulletSharp.Math.Matrix[] bmTransforms = new BulletSharp.Math.Matrix[rigidBodies.Length];

        bool correctionRequired = false;

        for (int i = 0; i < bmTransforms.Length; i++)
        {
            float[] rawTransform = new float[7];

            for (int j = 0; j < rawTransform.Length; j++)
                rawTransform[j] = transforms[i * 13 + j];

            bmTransforms[i] = DeserializeTransform(rawTransform);

            BulletSharp.Math.Matrix rbTransform = rigidBodies[i].GetCollisionObject().WorldTransform;

            if (!correctionRequired &&
                ((bmTransforms[i].Origin - rbTransform.Origin).Length > CorrectionPositionThreshold) ||
                Math.Abs(Quaternion.Angle(bmTransforms[i].Orientation.ToUnity(), rbTransform.Orientation.ToUnity())) > CorrectionRotationThreshold)
                correctionRequired = true;
        }

        if (!correctionRequired)
            return;
        
        for (int i = 0; i < bmTransforms.Length; i++)
        {
            float[] rawLinearVelocity = new float[3];

            for (int j = 0; j < rawLinearVelocity.Length; j++)
                rawLinearVelocity[j] = transforms[i * 13 + 7 + j];

            float[] rawAngularVelocity = new float[3];

            for (int j = 0; j < rawAngularVelocity.Length; j++)
                rawAngularVelocity[j] = transforms[i * 13 + 7 + rawLinearVelocity.Length + j];

            BulletSharp.Math.Vector3 linearVelocity = new BulletSharp.Math.Vector3(rawLinearVelocity);
            BulletSharp.Math.Vector3 angularVelocity = new BulletSharp.Math.Vector3(rawAngularVelocity);

            networkMeshes[i].UpdateMeshTransform(bmTransforms[i].Origin.ToUnity(), bmTransforms[i].Orientation.ToUnity());

            RigidBody rbCo = (RigidBody)rigidBodies[i].GetCollisionObject();

            rbCo.WorldTransform = bmTransforms[i];  
            rbCo.LinearVelocity = linearVelocity;
            rbCo.InterpolationAngularVelocity = angularVelocity;
        }
    }

    /// <summary>
    /// Serializes the given Matrix into an array of floats.
    /// </summary>
    /// <param name="matrix"></param>
    /// <returns></returns>
    private float[] SerializeTransform(BulletSharp.Math.Matrix matrix)
    {
        return new float[]
        {
            matrix.Origin.X,
            matrix.Origin.Y,
            matrix.Origin.Z,
            matrix.Orientation.X,
            matrix.Orientation.Y,
            matrix.Orientation.Z,
            matrix.Orientation.W
        };
    }

    /// <summary>
    /// Deserializes the given array of floats in to a Matrix.
    /// </summary>
    /// <param name="transform"></param>
    /// <returns></returns>
    private BulletSharp.Math.Matrix DeserializeTransform(float[] transform)
    {
        if (transform.Length != 7)
            return BulletSharp.Math.Matrix.Identity;

        return new BulletSharp.Math.Matrix
        {
            Origin = new BulletSharp.Math.Vector3(transform[0], transform[1], transform[2]),
            Orientation = new BulletSharp.Math.Quaternion(transform[3], transform[4], transform[5], transform[6])
        };
    }

    /// <summary>
    /// Serializes the given RigidBody linear velocity into an array of floats.
    /// </summary>
    /// <param name="rigidBody"></param>
    /// <returns></returns>
    private float[] SerializeVector3(BulletSharp.Math.Vector3 vec)
    {
        return new float[]
        {
            vec.X,
            vec.Y,
            vec.Z
        };
    }
}
