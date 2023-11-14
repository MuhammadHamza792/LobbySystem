using System;
using LobbyPackage.Scripts.UI;
using Unity.Netcode;
using UnityEngine;

namespace LobbyPackage.Scripts
{
    public class NetworkController : NetworkBehaviour
    {
        public static event Action OnClientConnected; 
        //public static event Action OnSessionFailedToLeave;
    
        public override void OnNetworkSpawn()
        {
            if (IsHost && IsOwner)
            {
                GameNetworkHandler.OnGameStarted?.Invoke();
            }
            else if (IsClient && IsOwner)
            {
                GameNetworkHandler.OnGameStarted?.Invoke();
                OnClientConnected?.Invoke();
            }
        
            LobbyController.DoLeaveSession += LeaveGame;
        }

        public void LeaveGame()
        {
            Debug.Log("Leaving Session!");

            if (IsOwner)
            {
                GameNetworkHandler.Instance.LeaveGame(IsHost);
            }
        }

        private void StopSession(Action onComplete) => GameNetworkHandler.Instance.StopGame(onComplete);

        [ClientRpc]
        private void DisconnectAllClientsFromLobbyClientRpc()
        {
            GameLobby.Instance.DestroyLobby();
        }
    
        [ClientRpc]
        private void DisconnectAllClientRpc() => NetworkManager.Singleton.Shutdown(); 

        public override async void OnNetworkDespawn() => LobbyController.DoLeaveSession -= LeaveGame;
    }
}
