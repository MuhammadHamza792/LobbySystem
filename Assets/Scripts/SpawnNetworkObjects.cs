using System;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

    public class SpawnNetworkObjects : NetworkBehaviour
    {
        [SerializeField] private GameObject _networkManager;
        
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
            var objectSpawned = Instantiate(_networkManager);
            objectSpawned.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        }
    }
