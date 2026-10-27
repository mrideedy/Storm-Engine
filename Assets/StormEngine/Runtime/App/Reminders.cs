using System;
using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine
{
    /// <summary>A platform notification backend (the StormEngine.Notify assembly provides Android's).</summary>
    public interface INotifier
    {
        /// <summary>True when the system lets this app post notifications.</summary>
        bool Allowed { get; }
        /// <summary>Shows the system permission prompt where one is needed (Android 13+).</summary>
        void RequestPermission(Action<bool> done);
        /// <summary>Polled every frame so pending permission requests can finish.</summary>
        void Tick();
        void CancelAll();
        void Schedule(int id, string title, string text, DateTime fireTime);
    }

    /// <summary>
    /// Local reminders, scheduled when the game goes to the background and cleared when it comes
    /// back: the next daily gift, the weekly tournament closing, a new tournament week and a full
    /// Offline Generator. Nothing fires before 9:00 or after 21:00 local time.
    /// </summary>
    public sealed class Reminders
    {
        /// <summary>Set by a platform assembly at startup; null where notifications are unavailable (the Editor).</summary>
        public static Func<INotifier> Factory;

        public struct Reminder
        {
            public int Id;
            public string Title;
            public string Text;
            public DateTimeOffset Time;
        }

        readonly StormApp _app;
        readonly INotifier _notifier;

        public Reminders(StormApp app)
        {
            _app = app;
            try
            {
                _notifier = Factory?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Notifications are unavailable: " + e.Message);
            }
        }

        public bool Supported => _notifier != null;

        public bool Allowed
        {
            get
            {
                try
                {
                    return _notifier != null && _notifier.Allowed;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public void RequestPermission(Action<bool> done)
        {
            if (_notifier == null)
            {
                done?.Invoke(false);
                return;
            }
            try
            {
                _notifier.RequestPermission(done);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Notification permission request failed: " + e.Message);
                done?.Invoke(false);
            }
        }

        public void Tick()
        {
            try
            {
                _notifier?.Tick();
            }
            catch (Exception)
            {
                // A failed poll only means no answer yet.
            }
        }

        /// <summary>Reminders only matter while the player is away.</summary>
        public void Clear()
        {
            try
            {
                _notifier?.CancelAll();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Could not clear notifications: " + e.Message);
            }
        }

        public void ScheduleAll()
        {
            if (_notifier == null) return;
            try
            {
                _notifier.CancelAll();
                if (!_app.Save.settings.notifications || !_notifier.Allowed) return;
                foreach (var r in Plan(_app.Game, DateTimeOffset.UtcNow))
                    _notifier.Schedule(r.Id, r.Title, r.Text, r.Time.LocalDateTime);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Could not schedule reminders: " + e.Message);
            }
        }

        /// <summary>What to remind the player about, given the game state (pure, so it can be tested).</summary>
        public static List<Reminder> Plan(Game g, DateTimeOffset now)
        {
            var list = new List<Reminder>();
            long nowUnix = now.ToUnixTimeSeconds();
            var soonest = now.AddMinutes(20);

            if (g.DailyUnlocked)
            {
                var gift = new Reminder { Id = 1, Title = L.T("Your daily gift is ready") };
                if (g.LoginRewardReady)
                {
                    gift.Time = Friendly(now.AddHours(3));
                    gift.Text = L.T("Today's gift and three fresh missions are still waiting for you.");
                }
                else
                {
                    gift.Time = Friendly(DateTimeOffset.FromUnixTimeSeconds((Game.DayOf(nowUnix) + 1) * 86400));
                    int streak = g.Daily.Streak;
                    gift.Text = streak >= 2
                        ? L.F("Keep your {0}-day streak going! Your Day {1} gift is waiting.", streak, streak + 1)
                        : L.T("A new gift and three fresh missions are waiting.");
                }
                list.Add(gift);
            }

            if (g.OfflineUnlocked)
            {
                double hours = g.Snap.Stat[(int)StatId.OfflineCapHours];
                if (hours >= 1)
                    list.Add(new Reminder
                    {
                        Id = 2,
                        Title = L.T("Offline Generator full"),
                        Text = L.T("Your engine has stored all the Energy it can. Come back to collect it and keep it running!"),
                        Time = Friendly(now.AddHours(hours))
                    });
            }

            if (g.TournamentUnlocked && g.Week >= 0)
            {
                long end = WeekClock.WeekEnd(g.Week);
                // Closing reminder: four hours before the end, moved into the evening before if that is at night.
                var closing = DateTimeOffset.FromUnixTimeSeconds(end - 4 * 3600).ToLocalTime();
                if (closing.Hour < 9) closing = new DateTimeOffset(closing.Date.AddHours(-4), closing.Offset);
                else if (closing.Hour >= 21) closing = new DateTimeOffset(closing.Date.AddHours(20), closing.Offset);
                if (closing > soonest && closing.ToUnixTimeSeconds() < end)
                    list.Add(new Reminder
                    {
                        Id = 3,
                        Title = L.T("The tournament ends soon"),
                        Text = L.T("This week's tournament is almost over. One more storm could lift you up the leaderboard!"),
                        Time = closing
                    });

                int next = WeekClock.ConditionIndex(g.Week + 1, g.C.Weekly.Length);
                var week = new Reminder
                {
                    Id = 4,
                    Title = L.T("A new tournament week"),
                    Text = L.F("This week's condition: {0}. Collect last week's reward and enter!", g.C.Weekly[next].Name),
                    Time = Friendly(DateTimeOffset.FromUnixTimeSeconds(end))
                };
                // Monday is also a new day: one notification instead of two.
                int gift = list.FindIndex(r => r.Id == 1);
                if (gift >= 0 && Math.Abs((list[gift].Time - week.Time).TotalHours) < 2)
                {
                    week.Text += " " + L.T("Your daily gift is ready too.");
                    list.RemoveAt(gift);
                }
                list.Add(week);
            }

            list.RemoveAll(r => r.Time <= soonest);
            return list;
        }

        /// <summary>Moves a time out of the quiet hours (21:00 to 9:00 local) to the next 9:00.</summary>
        public static DateTimeOffset Friendly(DateTimeOffset t)
        {
            var local = t.ToLocalTime();
            if (local.Hour < 9) return new DateTimeOffset(local.Date.AddHours(9), local.Offset);
            if (local.Hour >= 21) return new DateTimeOffset(local.Date.AddDays(1).AddHours(9), local.Offset);
            return local;
        }
    }
}
