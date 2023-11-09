using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

    public class SpawnNetworkObjects : NetworkBehaviour
    {
        [SerializeField] private GameObject _networkManager;

        public override void OnNetworkSpawn() => NetworkManager.OnClientConnectedCallback += DoSpawnNetworkController;

        public override void OnNetworkDespawn() => NetworkManager.OnClientConnectedCallback -= DoSpawnNetworkController;

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
