﻿using Synthesis.FSM;
using Synthesis.GUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Synthesis.Network
{
    [NetworkSettings(channel = 0, sendInterval = 0f)]
    public class MatchManager : NetworkBehaviour
    {
        /// <summary>
        /// The global <see cref="MatchManager"/> instance.
        /// </summary>
        public static MatchManager Instance { get; private set; }

        /// <summary>
        /// The name of the selected field.
        /// </summary>
        public string FieldName
        {
            get
            {
                return fieldName;
            }
            set
            {
                if (!isServer)
                    return;

                fieldName = value;
                fieldGuid = string.Empty;
            }
        }

        /// <summary>
        /// The GUID of the selected field.
        /// </summary>
        public string FieldGuid => fieldGuid;

        /// <summary>
        /// True if synchronization is occurring.
        /// </summary>
        [SyncVar]
        public bool syncing;

        [SyncVar]
        private string fieldName;

        [SyncVar]
        private string fieldGuid;

        /// <summary>
        /// Describes which resources are required to be transferred by which clients.
        /// </summary>
        private Dictionary<int, List<int>> dependencyMap;

        /// <summary>
        /// Indicates which dependencies have been resolved without transferring.
        /// </summary>
        private Dictionary<int, bool> resolvedDependencies;

        /// <summary>
        /// The <see cref="ServerToClientFileTransferer"/> associated with this instance.
        /// </summary>
        public ServerToClientFileTransferer FileTransferer { get; private set; }

        private Action generationComplete;

        private StateMachine uiStateMachine;

        private Dictionary<string, List<byte>> fileData;

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        private void Awake()
        {
            Instance = this;
            dependencyMap = new Dictionary<int, List<int>>();
            resolvedDependencies = new Dictionary<int, bool>();
            uiStateMachine = GameObject.Find("UserInterface").GetComponent<StateMachine>();
            fileData = new Dictionary<string, List<byte>>();

            FileTransferer = GetComponent<ServerToClientFileTransferer>();

            if (!isServer)
            {
                FileTransferer.OnDataFragmentReceived += DataFragmentReceived;
                FileTransferer.OnReceivingComplete += ReceivingComplete;
            }
        }

        /// <summary>
        /// Loads the selected field file and reads its GUID.
        /// </summary>
        [Server]
        public void UpdateFieldGuid()
        {
            if (fieldGuid.Length > 0)
                return;

            string fieldFile = PlayerPrefs.GetString("simSelectedField") + "\\definition.bxdf";

            if (!File.Exists(fieldFile))
            {
                CancelSync();
                return;
            }

            Task<FieldDefinition> loadingTask = new Task<FieldDefinition>(() => BXDFProperties.ReadProperties(fieldFile));

            loadingTask.ContinueWith(t =>
            {
                if (t.Result == null)
                {
                    CancelSync();
                    return;
                }

                fieldGuid = t.Result.GUID.ToString();
            });

            loadingTask.Start();
        }

        /// <summary>
        /// Generates a dependency map from all <see cref="PlayerIdentity"/> instances.
        /// </summary>
        [Server]
        public void GenerateDependencyMap(Action onGenerationComplete)
        {
            generationComplete = onGenerationComplete;

            dependencyMap.Clear();
            resolvedDependencies.Clear();

            foreach (PlayerIdentity p in FindObjectsOfType<PlayerIdentity>())
                resolvedDependencies.Add(p.id, false);

            RpcCheckDependencies();
        }

        /// <summary>
        /// Gathers resources from all <see cref="PlayerIdentity"/> instances.
        /// </summary>
        [Server]
        public void GatherResources()
        {
            HashSet<PlayerIdentity> remainingIdentities = new HashSet<PlayerIdentity>(FindObjectsOfType<PlayerIdentity>());

            foreach (KeyValuePair<int, List<int>> entry in dependencyMap.Where(e => e.Key >= 0))
            {
                PlayerIdentity identity = PlayerIdentity.FindById(entry.Key);
                remainingIdentities.Remove(identity);
                identity.TransferResources();
            }

            foreach (PlayerIdentity identity in remainingIdentities)
                identity.ready = true;
        }

        /// <summary>
        /// Distributes resources to the required <see cref="PlayerIdentity"/> instances.
        /// </summary>
        [Server]
        public void DistributeResources()
        {
            foreach (KeyValuePair<int, List<int>> entry in dependencyMap.Where(e => e.Key >= 0))
            {
                PlayerIdentity identity = PlayerIdentity.FindById(entry.Key);
                string transferPrefix = string.Join(",", entry.Value) + ".";

                foreach (KeyValuePair<string, List<byte>> file in identity.FileData)
                {
                    FileTransferer.SendFile(transferPrefix + file.Key, file.Value.ToArray());
                    Debug.Log("Send it!");
                }
                //foreach (int id in entry.Value)
                //{
                //    NetworkConnection clientConnection = PlayerIdentity.FindById(id).connectionToClient;

                //    foreach (KeyValuePair<string, List<byte>> file in identity.FileData)
                //        fileTransferer.SendFile(file.Key, file.Value.ToArray());
                //}
            }
        }

        /// <summary>
        /// Called when a fragment of data is received from the server.
        /// </summary>
        /// <param name="transferId"></param>
        /// <param name="data"></param>
        private void DataFragmentReceived(string transferId, byte[] data)
        {
        }

        /// <summary>
        /// Called when a file has been received completely by the server.
        /// </summary>
        /// <param name="transferId"></param>
        /// <param name="data"></param>
        private void ReceivingComplete(string transferId, byte[] data)
        {
            Debug.Log("Fraggy boi!");
        }

        /// <summary>
        /// Checks if the resources held by the other identity need to be transferred to
        /// this instance.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="otherIdentity"></param>
        [ClientRpc]
        private void RpcCheckDependencies()
        {
            PlayerIdentity.LocalInstance.CheckDependencies();
        }

        /// <summary>
        /// Adds the given dependency to the dependency map.
        /// </summary>
        /// <param name="ownerId"></param>
        /// <param name="dependantId"></param>
        public void AddDependencies(int dependantId, int[] ownerIds)
        {
            resolvedDependencies[dependantId] = true;

            foreach (int ownerId in ownerIds)
            {
                if (!dependencyMap.ContainsKey(ownerId))
                    dependencyMap[ownerId] = new List<int>();

                dependencyMap[ownerId].Add(dependantId);
            }

            if (!resolvedDependencies.ContainsValue(false))
                generationComplete();
        }

        /// <summary>
        /// Pushes the given state for all clients when all
        /// <see cref="PlayerIdentity"/> instances are ready.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        [Server]
        public void AwaitPushState<T>() where T : new()
        {
            StopAllCoroutines();
            StartCoroutine(StartWhenReady(() => RpcPushState(typeof(T).ToString())));
        }

        /// <summary>
        /// Changes to the given state for all clients when all
        /// <see cref="PlayerIdentity"/> instances are ready.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hardReset"></param>
        [Server]
        public void AwaitChangeState<T>(bool hardReset = true) where T : new()
        {
            StopAllCoroutines();
            StartCoroutine(StartWhenReady(() => RpcChangeState(typeof(T).ToString(), hardReset)));
        }

        /// <summary>
        /// Pops the current state on all clients.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        [Server]
        private void PopState(string msg = "")
        {
            StopAllCoroutines();
            RpcPopState(msg);
        }

        /// <summary>
        /// Waits for all <see cref="PlayerIdentity"/> instances to indicate
        /// readiness before executing the provided <see cref="Action"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [Server]
        private IEnumerator StartWhenReady(Action whenReady)
        {
            yield return new WaitUntil(() => FindObjectsOfType<PlayerIdentity>().All(p => p.ready));
            whenReady();
        }

        /// <summary>
        /// Cancels the synchronization process.
        /// </summary>
        [Server]
        public void CancelSync()
        {
            if (!syncing)
                return;

            syncing = false;
            PopState("Synchronization cancelled!");
        }

        /// <summary>
        /// Pushes the given state for all clients.
        /// </summary>
        /// <param name="stateType"></param>
        [ClientRpc]
        private void RpcPushState(string stateType)
        {
            State state = StringToState(stateType);

            if (state != null)
            {
                PlayerIdentity.LocalInstance.CmdSetReady(false);
                uiStateMachine.PushState(state);
            }
        }

        /// <summary>
        /// Changes to the given state for all clients.
        /// </summary>
        /// <param name="stateType"></param>
        /// <param name="hardReset"></param>
        [ClientRpc]
        private void RpcChangeState(string stateType, bool hardReset)
        {
            State state = StringToState(stateType);

            if (state != null)
            {
                PlayerIdentity.LocalInstance.CmdSetReady(false);
                uiStateMachine.ChangeState(state, hardReset);
            }
        }

        /// <summary>
        /// Pops the current state on all clients.
        /// </summary>
        [ClientRpc]
        private void RpcPopState(string msg)
        {
            FileTransferer.ResetTransferData();
            PlayerIdentity.LocalInstance.FileTransferer.ResetTransferData();

            PlayerIdentity.LocalInstance.CmdSetReady(false);

            uiStateMachine.PopState();

            if (!msg.Equals(string.Empty))
                UserMessageManager.Dispatch(msg, 8f);
        }

        /// <summary>
        /// Returns a <see cref="State"/> from the given string.
        /// </summary>
        /// <param name="stateType"></param>
        /// <returns></returns>
        private State StringToState(string stateType)
        {
            Type type = Type.GetType(stateType);

            if (type == null)
            {
                Debug.LogError("Could not create state from type \"" + stateType + "\"!");
                return null;
            }

            return Activator.CreateInstance(type) as State;
        }
    }
}