using System;
using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// Word of mouth: the system share sheet (so players can brag about a storm) and store ratings
    /// through Google Play's in-app review card, asked at most twice and only after a good moment.
    /// </summary>
    public static class Community
    {
        /// <summary>Opens the phone's share sheet with a message. Elsewhere the text is copied instead.</summary>
        public static bool Share(string subject, string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
                    intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject).Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, subject))
                        UnityEngine.Android.AndroidApplication.currentActivity.Call("startActivity", chooser);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Share failed: " + e.Message);
            }
#endif
            GUIUtility.systemCopyBuffer = text;
            return false;
        }

        /// <summary>Opens the game's store page (the Play Store app when installed).</summary>
        public static void OpenStorePage()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Application.OpenURL("market://details?id=" + Application.identifier);
                return;
            }
            catch (Exception)
            {
                // Falls through to the web page.
            }
#endif
            Application.OpenURL(ReleaseConfig.Instance.StoreUrl);
        }

        /// <summary>
        /// Shows Google Play's rating card without leaving the game. Play decides whether it really
        /// appears (it has its own quota), so this never nags. Returns false where it is unavailable.
        /// </summary>
        public static bool RequestReview()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                var factory = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory");
                var manager = factory.CallStatic<AndroidJavaObject>("create", activity);
                var request = manager.Call<AndroidJavaObject>("requestReviewFlow");
                request.Call<AndroidJavaObject>("addOnCompleteListener", new OnComplete(task =>
                {
                    try
                    {
                        if (!task.Call<bool>("isSuccessful")) return;
                        var info = task.Call<AndroidJavaObject>("getResult");
                        manager.Call<AndroidJavaObject>("launchReviewFlow", activity, info);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[StormEngine] Review card failed: " + e.Message);
                    }
                }));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] In-app review unavailable: " + e.Message);
            }
#endif
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Java OnCompleteListener for Play's Task API (runs on Android's thread; touches no Unity objects).</summary>
        sealed class OnComplete : AndroidJavaProxy
        {
            readonly Action<AndroidJavaObject> _done;

            public OnComplete(Action<AndroidJavaObject> done) : base("com.google.android.gms.tasks.OnCompleteListener")
            {
                _done = done;
            }

            // Called from Java by name.
            void onComplete(AndroidJavaObject task) => _done(task);
        }
#endif
    }
}
