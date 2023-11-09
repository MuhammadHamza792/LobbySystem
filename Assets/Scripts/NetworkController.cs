using System;
using System.Linq;
using UI;
using UI.Notify;
using Unity.Netcode;
using UnityEngine;

public class NetworkController : NetworkBehaviour
{
    public static event Action OnLeavingSession;
    public static event Action OnSessionLeft;

    public static event Action OnClientConnected; 
    //public static event Action OnSessionFailedToLeave;
    
    public override void OnNetworkSpawn()
    {
        if (IsHost && IsOwner)
        {
            GameRelay.OnGameStarted?.Invoke();
        }
        else if (IsClient && IsOwner)
        {
            GameRelay.OnGameStarted?.Invoke();
            OnClientConnected?.Invoke();
        }
        
        LobbyController.DoLeaveSession += LeaveGame;
    }

    public void LeaveGame()
    {
        Debug.Log("Leaving Session!");
        
        if (IsHost && IsOwner)
        {
            OnLeavingSession?.Invoke();
            StopSession(() =>
            {
                NetworkManager.Singleton.Shutdown();
                OnSessionLeft?.Invoke();
            });
            return;
        }
            
        if (IsClient && IsOwner)
        {
            GameLobby.Instance.LeaveLobby(() =>
            {
                NetworkManager.Singleton.Shutdown();
            });
        }
    }

    private void StopSession(Action onComplete) => GameRelay.Instance.StopGame(onComplete);

    [ClientRpc]
    private void DisconnectAllClientsFromLobbyClientRpc()
    {
        GameLobby.Instance.DestroyLobby();
    }

    [ServerRpc]
    private void DisconnectClientServerRpc(ulong clientId) => DisconnectClient(clientId);
    
    private void DisconnectClient(ulong clientId)
    {
        var player = NetworkManager.Singleton.ConnectedClientsList.FirstOrDefault(client => client.ClientId == clientId);
        if (player != null) player.PlayerObject.Despawn();
        NetworkManager.Singleton.DisconnectClient(clientId);
        //if (player != null) Destroy(player.PlayerObject);
    }

    [ClientRpc]
    private void DisconnectAllClientRpc() => NetworkManager.Singleton.Shutdown(); 

    public override async void OnNetworkDespawn()
    {
        if (!IsHost && IsOwner)
        {
            OnLeavingSession?.Invoke();
            await Helper.LoadSceneAsync(() =>
            {
                GameRelay.Instance.StopGame(() =>
                {
                    OnSessionLeft?.Invoke();
                });
                return true;
            }, "Lobby");
        }
        LobbyController.DoLeaveSession -= LeaveGame;
    }
}
