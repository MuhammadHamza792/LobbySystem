namespace MHZ.LobbyUI.Notify
{
    public interface INotification
    {
        public NotificationType NotificationType { get; }
        public bool NotifyOnClose { get; }
        public void ShowNotification(NotificationData nData);
    }
}