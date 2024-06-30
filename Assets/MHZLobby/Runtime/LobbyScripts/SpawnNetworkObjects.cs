using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MHZ.LobbyScripts
{
    public class SpawnNetworkObjects : NetworkBehaviour
    {
        [SerializeField] private List<NetworkObjectData> _networkObjects;
        [SerializeField] private bool _destroySpawnObjectsWithSpawner;
        private List<NetworkObject> _spawnedNetworkObjects;
        
        private void Start() => NetworkManager.OnClientConnectedCallback += DoSpawnNetworkController;

        public void OnDisable() => NetworkManager.OnClientConnectedCallback -= DoSpawnNetworkController;

        private void DoSpawnNetworkController(ulong clientId)
        {
            if (IsHost && IsOwner)
            {
                InstantiateObject(clientId);
                return;
            }

            if (IsClient && IsOwner)
            {
                SpawnObjectServerRpc(clientId);
            }
        }

        [ServerRpc]
        private void SpawnObjectServerRpc(ulong clientId) => InstantiateObject(clientId);

        private void InstantiateObject(ulong clientId)
        {
            _spawnedNetworkObjects ??= new List<NetworkObject>();
            foreach (var networkObject in _networkObjects)
            {
                var objectSpawned = Instantiate(networkObject.GameObject);
                Debug.Log(objectSpawned.name);
                var networkObjectRef = objectSpawned.GetComponent<NetworkObject>();
                if (networkObject.SpawnWithOwnerShip)
                    networkObjectRef.SpawnWithOwnership(clientId, networkObject.DestroyWithScene);
                else if (networkObject.SpawnAsPlayerObject)
                    networkObjectRef.SpawnAsPlayerObject(clientId, networkObject.DestroyWithScene);
                else
                    networkObjectRef.Spawn(networkObject.DestroyWithScene);
                _spawnedNetworkObjects.Add(networkObjectRef);
            }
        }

        public override void OnNetworkDespawn()
        {
            if(!IsServer && _destroySpawnObjectsWithSpawner) return;
            foreach (var spawnedNetworkObject in _spawnedNetworkObjects)
            {
                if(spawnedNetworkObject.IsSpawned)
                    spawnedNetworkObject.Despawn();
            }
            base.OnNetworkDespawn();
        }
    }

    [Serializable]
    public struct NetworkObjectData
    {
        public GameObject GameObject;
        public bool SpawnWithOwnerShip;
        public bool SpawnAsPlayerObject;
        public bool DestroyWithScene;
    }
}
