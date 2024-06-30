namespace MHZ.LobbyUI
{
    public class PanelContext
    {
        public IPanelState CurrentState { set; get; }
        public IPanelState PreviousState { set; get; }

        private LobbyController _lobbyController;

        private string _stateName;

        public PanelContext(LobbyController lobbyController)
        {
            _lobbyController = lobbyController;
        }
        
        public void Transition()
        {
            CurrentState.HandleState(_lobbyController);
        }

        public void ChangeState(IPanelState panelState)
        {
            CurrentState = panelState;
            CurrentState.HandleState(_lobbyController);
            PreviousState = CurrentState;
        }
    }
}
