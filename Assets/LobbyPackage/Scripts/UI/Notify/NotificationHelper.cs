namespace LobbyPackage.Scripts.UI.Notify
{
    public static class NotificationHelper
    {
        public static void SendNotification(NotificationType type,string msg, 
            INotifier notifier, NotifyCallType callType)
        {
            var nData = new NotificationData
            {
                NotifyType = type,
                Text = msg,
                Notifier = notifier,
                NotifyCallType = callType
            };
            
            LobbyNotificationHandler.Instance.HandleNotificationPanel(nData);
        }
    }
}