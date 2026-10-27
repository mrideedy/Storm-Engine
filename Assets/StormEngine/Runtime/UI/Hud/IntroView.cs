using StormEngine.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// Screen layer of the launch intro, which plays in the world itself (<see cref="WorldView.PlayIntro"/>):
    /// it fades in from the interface navy, shows the studio credit while the storm builds, turns a tap
    /// into "play the rest quickly", and keeps the menu out of reach until the camera has arrived.
    /// </summary>
    public sealed class IntroView : VisualElement
    {
        const float FadeIn = 0.55f;
        static readonly Color Navy = new Color(0.106f, 0.165f, 0.29f);

        readonly WorldView _world;
        readonly Label _credit;

        public bool Done => !_world.IntroPlaying;

        public IntroView(WorldView world)
        {
            _world = world;
            AddToClassList("se-intro");
            RegisterCallback<PointerDownEvent>(_ => _world.SkipIntro());

            // The studio name is a brand, the same in every language.
            _credit = Ui.Text("MOONBIT STUDIOS", "se-intro-credit");
            _credit.pickingMode = PickingMode.Ignore;
            Add(_credit);
            Tick();
        }

        public void Tick()
        {
            float t = Mathf.Max(0f, _world.IntroTime);
            float fade = 1f - Mathf.Clamp01(t / FadeIn);
            style.backgroundColor = new Color(Navy.r, Navy.g, Navy.b, fade * fade * (3f - 2f * fade));
            // The credit shows while the storm builds and leaves before the title lands.
            float into = Mathf.Clamp01((t - 0.25f) / 0.35f);
            float outOf = Mathf.Clamp01((t - (WorldView.IntroTitleAt - 0.5f)) / 0.3f);
            _credit.style.opacity = into * (1f - outOf) * 0.85f;
        }
    }
}
