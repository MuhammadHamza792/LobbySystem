using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace MHZ.LobbyScripts
{
    public class GameRelay : Singleton<GameRelay>
    {
        [SerializeField] private List<string> _regions;

        public ReadOnlyCollection<string> Regions => new (_regions);
    
        public string RelayCode { private set; get; }
    
        #region Events

        public static event Action OnCreatingRelay; 
        public static event Action OnRelayCreated; 
        public static event Action<string> OnRelayFailedToCreate; 
    
        public static event Action OnJoiningRelay; 
        public static event Action OnRelayJoined; 
        public static event Action<string> OnRelayFailedToJoined;
    
        #endregion
    
        /// <summary>
        /// Creates a Relay allocation.
        /// </summary>
        /// <param name="maxPlayer">Max players to allocate.</param>
        /// <param name="region">Region to connect.</param>
        /// <returns>Returns a string task so it can wait until it finishes and grabs relay code on completion.</returns>
        public async Task<string> CreateRelay(int maxPlayer, string region = null)
        {
            try
            {
                OnCreatingRelay?.Invoke();
            
                var relay = await Relay.Instance.CreateAllocationAsync(maxPlayer, region);

                RelayCode = await Relay.Instance.GetJoinCodeAsync(relay.AllocationId);
            
                var serverData = new RelayServerData(relay, "dtls");
            
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(serverData);
            
                OnRelayCreated?.Invoke();
            
                return RelayCode;
            }
            catch (RelayServiceException e)
            {
                OnRelayFailedToCreate?.Invoke(e.Message);
                Debug.Log(e);
                throw;
            }
        }
    
        /// <summary>
        /// Joins a Relay allocation.
        /// </summary>
        /// <param name="relayCode">Code to join Relay.</param>
        /// <returns>Returns a Allocation task so it can wait until it finishes and grabs allocation on completion.</returns>
        public async Task<JoinAllocation> JoinRelay(string relayCode)
        {
            try
            {
                OnJoiningRelay?.Invoke();
            
                var relayToJoin = await Relay.Instance.JoinAllocationAsync(relayCode);
            
                OnRelayJoined?.Invoke();
            
                return relayToJoin;
            }
            catch (RelayServiceException e)
            {
                OnRelayFailedToJoined?.Invoke(e.Message);
                Debug.Log(e);
                throw;
            }
        }
    }
}
