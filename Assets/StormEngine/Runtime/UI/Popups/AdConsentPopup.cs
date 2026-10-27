using StormEngine.Core;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// One-time ad privacy question, asked before the ad network starts. Either answer keeps every
    /// rewarded ad; it only decides whether the ads may be tailored to the player's interests.
    /// </summary>
    public sealed class AdConsentPopup : Popup
    {
        public AdConsentPopup(StormApp app)
            : base(L.T("PERSONALIZED ADS?"), L.T("Your choice, your data"), "eye")
        {
            DismissOnScrim = false;
            BodyEl.Add(Ui.Text(L.T("Storm Engine is free. Ads only play when you choose to watch one for a reward."), "se-body"));
            var more = Ui.Text(L.T("Our ad partner can use your device's advertising ID to show ads that match your interests. Without it you still get every reward, just with less relevant ads. You can change this any time in Settings."), "se-body");
            more.style.marginTop = 8;
            BodyEl.Add(more);
            if (!string.IsNullOrWhiteSpace(ReleaseConfig.Instance.privacyPolicyUrl))
            {
                var link = Ui.Btn(L.T("Privacy policy"), app.OpenPrivacyPolicy, "white", "small");
                link.style.alignSelf = Align.Center;
                link.style.marginTop = 10;
                BodyEl.Add(link);
            }
            Action(L.T("No, thanks"), () => app.SetAdConsent(false), "white");
            Action(L.T("Allow"), () => app.SetAdConsent(true), "green");
        }
    }
}
