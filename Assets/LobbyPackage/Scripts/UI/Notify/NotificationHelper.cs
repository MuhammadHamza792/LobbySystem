namespace LobbyPackage.Scripts.UI.Notify
{
    public static class NotificationHelper
    {
        public static void SendNotification(NotificationType type, string context ,string msg, 
            INotifier notifier, NotifyCallType callType)
        {
            var nData = new NotificationData
            {
                NotifyType = type,
                Context = context,
                Text = msg,
                Notifier = notifier,
                NotifyCallType = callType
            };
            
            LobbyNotificationHandler.Instance.HandleNotificationPanel(nData);
        }
    }
}