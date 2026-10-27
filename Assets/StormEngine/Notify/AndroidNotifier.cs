using System;
using Unity.Notifications.Android;
using UnityEngine;

namespace StormEngine.Notify
{
    /// <summary>
    /// Android reminders through com.unity.mobile.notifications. Registered on device builds only;
    /// the icons are set up by Storm Engine > Set Up Notification Icons.
    /// </summary>
    sealed class AndroidNotifier : INotifier
    {
        const string Channel = "storm_reminders";

        PermissionRequest _request;
        Action<bool> _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Reminders.Factory = () => new AndroidNotifier();
#endif
        }

        AndroidNotifier()
        {
            if (!AndroidNotificationCenter.Initialize()) throw new InvalidOperationException("Android notifications could not start.");
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(Channel, "Reminders",
                "Daily gifts, the weekly tournament closing and a full Offline Generator.", Importance.Default));
        }

        public bool Allowed => AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed;

        public void RequestPermission(Action<bool> done)
        {
            _done = done;
            _request = new PermissionRequest();
            Tick();
        }

        public void Tick()
        {
            if (_request == null || _request.Status == PermissionStatus.RequestPending) return;
            bool ok = _request.Status == PermissionStatus.Allowed;
            var done = _done;
            _request = null;
            _done = null;
            done?.Invoke(ok);
        }

        public void CancelAll()
        {
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
        }

        public void Schedule(int id, string title, string text, DateTime fireTime)
        {
            var notification = new AndroidNotification(title, text, fireTime)
            {
                SmallIcon = "storm_small",
                LargeIcon = "storm_large",
                Color = new Color(0.25f, 0.66f, 0.93f),
                ShowTimestamp = true
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, Channel, id);
        }
    }
}
