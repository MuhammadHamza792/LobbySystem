﻿using System;
using LobbyPackage.Scripts.UI;
using Unity.Netcode;
using UnityEngine;

namespace LobbyPackage.Scripts
{
    public class NetworkController : NetworkBehaviour
    {
        public static event Action<bool> OnClientConnected; 
        //public static event Action OnSessionFailedToLeave;
    
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                GameNetworkHandler.OnGameStarted?.Invoke();
                GameNetworkHandler.Instance.InSession = true;
                OnClientConnected?.Invoke(IsHost);
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

        public override async void OnNetworkDespawn() => LobbyController.DoLeaveSession -= LeaveGame;
    }
}
