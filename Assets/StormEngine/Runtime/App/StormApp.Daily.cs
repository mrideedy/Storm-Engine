using StormEngine.Core;
using StormEngine.UI;
using UnityEngine.UIElements;

namespace StormEngine
{
    /// <summary>Daily Rewards: login gifts, the streak and daily missions.</summary>
    public sealed partial class StormApp
    {
        long _dailyShownDay = -1;
        bool _checkDaily;

        public void ShowDaily()
        {
            if (!Game.DailyUnlocked)
            {
                Sfx.Deny();
                Ui.Toast("lock", L.F("Daily Rewards unlock at Engine Level {0}.", Game.FeatureLevel("daily")));
                return;
            }
            _dailyShownDay = Game.Today;
            Ui.ShowPopup(new DailyPopup(this));
        }

        /// <summary>Opens Daily Rewards once per day when a gift is waiting (at launch and when coming back).</summary>
        void MaybeShowDaily()
        {
            _checkDaily = false;
            if (Ui == null || !Game.LoginRewardReady || _dailyShownDay == Game.Today || Game.IsStorming) return;
            ShowDaily();
        }

        public void ClaimLoginReward(VisualElement source = null)
        {
            int day = Game.TodayStreakDay;
            var reward = Game.ClaimLoginReward();
            if (reward == null)
            {
                Sfx.Deny();
                return;
            }
            Sfx.Discovery();
            Ui.PurchaseFx(source);
            Ui.Toast("gift", L.F("Day {0} gift: {1}!", day, DailyText.Describe(Game, reward)));
            SaveNow(false);
            MaybeAskForReminders();
        }

        /// <summary>
        /// After the first gift, asks in plain words whether the player wants reminders, and only then
        /// shows the system prompt (Android 13+ requires one).
        /// </summary>
        void MaybeAskForReminders()
        {
            var s = Save.settings;
            if (s.notifyAsked || !s.notifications || !Reminders.Supported || Reminders.Allowed) return;
            s.notifyAsked = true;
            Ui.ShowPopup(new ConfirmPopup(L.T("GET A REMINDER?"),
                L.T("Want a nudge when your next daily gift is ready or the weekly tournament is about to close? A few a week at most, never at night."),
                L.T("Remind me"), () => Reminders.RequestPermission(ok =>
                {
                    if (!ok) Ui.Toast("info", L.T("Reminders are off. You can turn them on in Settings."));
                }), "green", L.T("Not now")));
        }

        public void ClaimMission(int slot, VisualElement source = null)
        {
            var def = Game.MissionAt(slot);
            var reward = Game.ClaimMission(slot);
            if (reward == null)
            {
                Sfx.Deny();
                return;
            }
            Sfx.Goal();
            Ui.PurchaseFx(source);
            Ui.Toast(def?.Icon ?? "gift", L.F("Mission reward: {0}", DailyText.Describe(Game, reward)));
            SaveNow(false);
        }

        public void ClaimMissionBonus(VisualElement source = null)
        {
            var reward = Game.ClaimMissionBonus();
            if (reward == null)
            {
                Sfx.Deny();
                return;
            }
            Sfx.LevelUp();
            Ui.PurchaseFx(source);
            Ui.Toast("gift", L.F("All missions done! {0}", DailyText.Describe(Game, reward)));
            SaveNow(false);
        }

        /// <summary>Wins a lost streak back with a rewarded ad.</summary>
        public void RestoreStreakWithAd() => WatchAd("ad_streak");
    }
}
