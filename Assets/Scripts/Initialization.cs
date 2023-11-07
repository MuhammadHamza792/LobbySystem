using System;
using System.Threading.Tasks;
#if UNITY_EDITOR
using ParrelSync;
#endif
using UI;
using UI.Notify;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using UnityEngine;

public class Initialization : Singleton<Initialization> , INotifier
{
    public bool IsInitialized { private set; get; }

    [SerializeField] private bool _askForPlayersName;
    [SerializeField] private CanvasToggler _loginCanvas;
    [SerializeField] private CanvasToggler _lobbyCanvas;
    
    public string PlayerName { private set; get; }
    
    private bool _signingIn;

    private void Start()
    {
        var options = new InitializationOptions();
#if UNITY_EDITOR
        options.SetProfile(ClonesManager.IsClone() ? ClonesManager.GetArgument() : "Primary");
#endif
    }
    
    public async void SignIn()
    {
        if (_askForPlayersName)
        {
            NotificationHelper.SendNotification(NotificationType.RequiredField, 
                "Please Enter Your Name", this, NotifyCallType.Open);
            return;
        }

        await  SignInAnonymouslyAsync();
    }

    private async void SignInAsync()
    {
        await SignInAnonymouslyAsync();
    }

    async Task SignInAnonymouslyAsync()
    {
        if(_signingIn) return;
        _signingIn = true;
        
        if (!await InitializingServices()) return;
        
        try
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Signing Into Unity Services",
                this, NotifyCallType.Open);
            
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            NotificationHelper.SendNotification(NotificationType.Progress, "Signed In",
                this, NotifyCallType.Close);
            
            _signingIn = false;
            Debug.Log("Sign in anonymously succeeded!");
            IsInitialized = true;
            ToggleLobbyCanvas(true);
            // Shows how to get the playerID
            Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}"); 

        }
        catch (AuthenticationException ex)
        {
            // Compare error code to AuthenticationErrorCodes
            // Notify the player with the proper error message
            _signingIn = false;
            IsInitialized = false;

            NotificationHelper.SendNotification(NotificationType.Error, ex.Message, this, NotifyCallType.Open);
            
            ToggleLobbyCanvas(false);
            Debug.LogException(ex);
            return;
        }
        catch (RequestFailedException ex)
        {
            // Compare error code to CommonErrorCodes
            // Notify the player with the proper error message
            IsInitialized = false;
            _signingIn = false;
            
            NotificationHelper.SendNotification(NotificationType.Error, ex.Message, this, NotifyCallType.Open);
            
            ToggleLobbyCanvas(false);
            Debug.LogException(ex);
            return;
        }
    }

    private void ToggleLobbyCanvas(bool active)
    {
        _loginCanvas.ToggleCanvas(!active);
        _lobbyCanvas.ToggleCanvas(active);
    }

    private async Task<bool> InitializingServices()
    {
        try
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Initializing",
                this, NotifyCallType.Open);

            await UnityServices.InitializeAsync();

            NotificationHelper.SendNotification(NotificationType.Progress, "Initialized",
                this, NotifyCallType.Close);
        }
        catch (Exception ex)
        {
            _signingIn = false;
            IsInitialized = false;

            NotificationHelper.SendNotification(NotificationType.Error, ex.Message, this, NotifyCallType.Open);
            
            return false;
        }

        return true;
    }
    
    private async void OnDisable()
    {
        if(!Instance.IsInitialized) return;
        
        if(GameLobby.Instance.LobbyInstance == null) return;

        if (GameLobby.Instance.IsLobbyHost())
        {
            await LobbyService.Instance.DeleteLobbyAsync(GameLobby.Instance.LobbyInstance.Id);
            Debug.Log("Lobby Destroyed");
        }
        else
        {
            await LobbyService.Instance.RemovePlayerAsync(GameLobby.Instance.LobbyInstance.Id, AuthenticationService.Instance.PlayerId);
            Debug.Log("Left Lobby");
        }
        
        AuthenticationService.Instance.SignOut();
        Debug.Log("Signed Out");
    }
    
    public void Notify(string notifyData)
    {
        PlayerName = notifyData;
        SignInAsync();
    }
}
