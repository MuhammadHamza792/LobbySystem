using System;
using System.Threading.Tasks;
using LobbyPackage.Scripts.UI.Notify;
#if UNITY_EDITOR
using ParrelSync;
#endif
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;


namespace LobbyPackage.Scripts
{
    public class Initialization : Singleton<Initialization> , INotifier
    {
        public bool IsInitialized { private set; get; }

        [SerializeField] private bool _askForPlayersName;
    
        public static event Action OnInitializing;
        public static event Action OnInitialized;
        public static event Action<string> OnFailedToInitialize;
        public static event Action OnSigningIn;
        public static event Action OnSignedIn;
        public static event Action<string> OnFailedToSignIn;
    
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
                NotificationHelper.SendNotification(NotificationType.RequiredField, "Sign In",
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
                OnSigningIn?.Invoke();
            
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            
                OnSignedIn?.Invoke();
                _signingIn = false;
            
                Debug.Log("Sign in anonymously succeeded!");
                IsInitialized = true;
            
                // Shows how to get the playerID
                Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}"); 

            }
            catch (AuthenticationException ex)
            {
                // Compare error code to AuthenticationErrorCodes
                // Notify the player with the proper error message
                _signingIn = false;
                IsInitialized = false;
                OnFailedToSignIn?.Invoke(ex.Message);
                Debug.LogException(ex);
                return;
            }
            catch (RequestFailedException ex)
            {
                // Compare error code to CommonErrorCodes
                // Notify the player with the proper error message
                IsInitialized = false;
                _signingIn = false;
            
                OnFailedToSignIn?.Invoke(ex.Message);
                Debug.LogException(ex);
                return;
            }
        }
    
        private async Task<bool> InitializingServices()
        {
            try
            {
                OnInitializing?.Invoke();
            
                await UnityServices.InitializeAsync();

                OnInitialized?.Invoke();
            }
            catch (Exception ex)
            {
                _signingIn = false;
                IsInitialized = false;

                OnFailedToInitialize?.Invoke(ex.Message);
                return false;
            }

            return true;
        }
    
        //private async void OnDisable() => await DisconnectPLayer();

        private async void OnApplicationQuit() => await DisconnectPLayer();

        private async Task DisconnectPLayer()
        {
            if (!Instance.IsInitialized) return;

            if (GameLobby.Instance == null)
            {
                AuthenticationService.Instance.SignOut();
                Debug.Log("Signed Out");
                return;
            }

            if (GameLobby.Instance.LobbyInstance == null)
            {
                AuthenticationService.Instance.SignOut();
                Debug.Log("Signed Out");
                return;
            }

            await GameLobby.Instance.LeaveLobbyIfExits();

            AuthenticationService.Instance.SignOut();
            Debug.Log("Signed Out");
        }

        public void Notify(string notifyData)
        {
            PlayerName = notifyData;
            SignInAsync();
        }
    }
}
