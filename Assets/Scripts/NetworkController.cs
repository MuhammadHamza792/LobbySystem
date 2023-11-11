using System;
using System.Linq;
using System.Threading.Tasks;
using UI;
using UI.Notify;
using Unity.Netcode;
using UnityEngine;

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
        
        if (IsHost && IsOwner)
        {
            GameNetworkHandler.Instance.LeaveGame(IsHost);
            /*OnLeavingSession?.Invoke();
            StopSession(() =>
            {
                NetworkManager.Singleton.Shutdown();
                OnSessionLeft?.Invoke();
            });*/
            return;
        }
            
        if (IsClient && IsOwner)
        {
            GameNetworkHandler.Instance.LeaveGame(IsHost);
            /*GameLobby.Instance.LeaveLobby(() =>
            {
                NetworkManager.Singleton.Shutdown();
            });*/
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
