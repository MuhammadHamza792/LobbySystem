using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

public class GameRelay : Singleton<GameRelay>
{
    [SerializeField] private bool _setCustomRelaySize;
    [SerializeField] private int _relaySize;
    
    [SerializeField] private List<string> _regions;

    public ReadOnlyCollection<string> Regions => new (_regions);
    
    public string RelayCode { private set; get; }
    
    public bool SessionStarted { private set; get; }
    
    #region Events

    public static event Action OnRelayCreated; 
    public static event Action OnRelayFailedToCreate; 
    
    public static event Action OnRelayJoined; 
    public static event Action OnRelayFailedToJoined;
    
    public static event Action OnGameStarted;
    public static event Action OnGameFailedToStart;
    
    #endregion
    
    public async Task<(string, bool)> CreateRelay(int maxPlayer, string region = null)
    {
        try
        {
            var relay = await Relay.Instance.CreateAllocationAsync(maxPlayer, region);

            RelayCode = await Relay.Instance.GetJoinCodeAsync(relay.AllocationId);

            OnRelayCreated?.Invoke();
            
            var serverData = new RelayServerData(relay, "dtls");
                
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(serverData);
            var serverHasStarted = NetworkManager.Singleton.StartHost();
                
            return (RelayCode, serverHasStarted);
        }
        catch (RelayServiceException e)
        {
            OnRelayFailedToCreate?.Invoke();
            Debug.Log(e);
            throw;
        }
    }
    
    public async Task JoinRelay(string relayCode)
    {
        try
        {
            var relayToJoin = await Relay.Instance.JoinAllocationAsync(relayCode);
            
            OnRelayJoined?.Invoke();
            
            var serverData = new RelayServerData(relayToJoin, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(serverData);
            
            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            OnRelayFailedToJoined?.Invoke();
            Debug.Log(e);
            throw;
        }
    }
    
    #region StartSession

    private bool _isSessionStarting;
    
    public async void StartGame(string reg = null)
    {
        if(_isSessionStarting) return;
        _isSessionStarting = true;

        var gameLobby = GameLobby.Instance;
        
        if(!gameLobby.IsLobbyHost()) return;
        
        string relayCode;
        bool serverStarted;

        _setCustomRelaySize = gameLobby.HasCustomConnection;
        _relaySize = gameLobby.NumbersOfCustomConnections;
        
        try
        {
            var selectedRegion = _regions.FirstOrDefault(region => region == reg);
            var serverInfo = await CreateRelay(_setCustomRelaySize ? _relaySize :
                gameLobby.LobbyInstance.Players.Count - 1, selectedRegion);
            relayCode = serverInfo.Item1;
            serverStarted = serverInfo.Item2;
            OnGameStarted?.Invoke();
            SessionStarted = true;
        }
    
        catch (RelayServiceException e)
        {
            OnGameFailedToStart?.Invoke();
            _isSessionStarting = false;
            SessionStarted = false;
            Debug.Log(e);
            return;
        }

        try
        {
            gameLobby.UpdateLobby(gameLobby.LobbyInstance.Id, new UpdateLobbyOptions 
            {
                Data = new Dictionary<string, DataObject>
                {
                    {"START_GAME", new DataObject(DataObject.VisibilityOptions.Member, relayCode)},
                    {"PLAYER_COUNT", new DataObject(DataObject.VisibilityOptions.Member, gameLobby.LobbyInstance.Players.Count.ToString())}
                }
            }, () => _isSessionStarting = false);
        }
        catch (LobbyServiceException e)
        {
            if (serverStarted)
                NetworkManager.Singleton.Shutdown();
            OnGameFailedToStart?.Invoke();
            _isSessionStarting = false;
            SessionStarted = false;
            Debug.Log(e);
            throw;
        }
        
    }

    private bool _isJoiningSession;
    
    public async void JoinGame()
    {
        if(_isJoiningSession) return;
        _isJoiningSession = true;

        var gameLobby = GameLobby.Instance;
        
        var relayCode = gameLobby.LobbyInstance.Data["START_GAME"].Value;

        try
        {
            await JoinRelay(relayCode);
            OnGameStarted?.Invoke();
            SessionStarted = true;
            _isJoiningSession = false;
        }
        catch (RelayServiceException e)
        {
            OnGameFailedToStart?.Invoke();
            SessionStarted = false;
            _isJoiningSession = false;
            Console.WriteLine(e);
            throw;
        }
        
    }
    
    #endregion
}
