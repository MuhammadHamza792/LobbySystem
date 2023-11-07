using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace UI.Notify
{
    public class LobbyNotification : MonoBehaviour
    {
        [SerializeField] protected NotificationType _type;
        
        protected LobbyNotificationHandler Handler;
        
        public INotification Notification { protected set; get; }
        
        public void SetHandler(LobbyNotificationHandler notificationHandler)
        {
            Handler = notificationHandler;
        }
    }
}
