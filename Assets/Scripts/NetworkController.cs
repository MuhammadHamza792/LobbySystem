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
    //public static event Action OnSessionFailedToLeave;
    
    public override void OnNetworkSpawn() => LobbyController.DoLeaveSession += LeaveGame;

    public void LeaveGame()
    {
        Debug.Log("Leaving Session!");
        
        if (IsHost && IsOwner)
        {
            OnLeavingSession?.Invoke();
            StopSession(() =>
            {
                DisconnectAllClients();
                OnSessionLeft?.Invoke();
            });
            return;
        }
            
        if (IsClient && IsOwner)
        {
            OnLeavingSession?.Invoke();
            DisconnectFromLobby();
            DisconnectClientServerRpc(OwnerClientId);
            OnSessionLeft?.Invoke();
        }
    }

    private void StopSession(Action onComplete) => GameRelay.Instance.StopGame(onComplete);

    [ClientRpc]
    private void DisconnectAllClientsFromLobbyClientRpc() => GameLobby.Instance.DestroyLobby();
        
    [ServerRpc]
    private void DisconnectClientServerRpc(ulong clientId) => DisconnectClient(clientId);

    private void DisconnectFromLobby() => GameLobby.Instance.LeaveLobby();
    
    private void DisconnectClient(ulong clientId)
    {
        var player = NetworkManager.Singleton.ConnectedClientsList.FirstOrDefault(client => client.ClientId == clientId);
        NetworkManager.Singleton.DisconnectClient(clientId);
        if (player != null) Destroy(player.PlayerObject);
    }

    private void DisconnectAllClients() => NetworkManager.Singleton.Shutdown();

    public override void OnNetworkDespawn() => LobbyController.DoLeaveSession -= LeaveGame;
}
