using LobbyPackage.Scripts.UI.Notify;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyPackage.Scripts.UI.States
{
    public class LoginState : MonoBehaviour, IPanelState , INotifier
    {
        [SerializeField] private Button _login;

        private LobbyController _lobbyController;
        
        #region Events&Delegates

        private void OnEnable()
        {
            Initialization.OnInitializing += Initializing;
            Initialization.OnInitialized += Initialized;
            Initialization.OnFailedToInitialize += FailedToInitialize;

            Initialization.OnSigningIn += SigningIn;
            Initialization.OnSignedIn += SignedIn;
            Initialization.OnFailedToSignIn += FailedToSignIn;
        }
        
        private void OnDisable()
        {
            Initialization.OnInitializing -= Initializing;
            Initialization.OnInitialized -= Initialized;
            Initialization.OnFailedToInitialize -= FailedToInitialize;

            Initialization.OnSigningIn -= SigningIn;
            Initialization.OnSignedIn -= SignedIn;
            Initialization.OnFailedToSignIn -= FailedToSignIn;
        }

        #endregion
        
        private void Initializing()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Initialize","Initializing",
                this, NotifyCallType.Open);
        }

        private void Initialized()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Initialize","Initialized",
                this, NotifyCallType.Close);
        }

        private void FailedToInitialize(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Initialize","Failed To Initialize",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, "Initialize",msg, this, NotifyCallType.Open);
        }

        private void SigningIn()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Sign In","Signing Into Unity Services",
                this, NotifyCallType.Open);
        }

        private void SignedIn()
        {
            _lobbyController.CheckAndChangeState("MainLobby");
            NotificationHelper.SendNotification(NotificationType.Progress, "Sign In","Signed In",
                this, NotifyCallType.Close);
        }

        private void FailedToSignIn(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Sign In","Failed To SignIn",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, "Sign In",msg, this, NotifyCallType.Open);
        }
        
        public void HandleState(LobbyController lobbyController)
        {
            _lobbyController = lobbyController;
            
            _login.onClick.RemoveAllListeners();
            _login.onClick.AddListener(() =>
            {
                Initialization.Instance.SignIn();
            });
            
            if(Initialization.Instance.IsInitialized)
                lobbyController.CheckAndChangeState("MainLobby");
        }

        public bool PrerequisiteCheck()
        {
            return true;
        }

        public void ResetState(LobbyController lobbyController)
        {
            
        }

        public void Notify(string notifyData)
        {
            
        }
    }
}
