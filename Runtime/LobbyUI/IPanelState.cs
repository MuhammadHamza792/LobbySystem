namespace MHZ.LobbyUI
{
    public interface IPanelState
    {
        public void HandleState(LobbyController lobbyController);

        public bool PrerequisiteCheck();

        public void ResetState(LobbyController lobbyController);
    }
}
