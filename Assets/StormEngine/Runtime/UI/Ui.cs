using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Small builder helpers so views read like a layout description.</summary>
    public static class Ui
    {
        public static VisualElement Div(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("se-btn");
            foreach (var c in classes) b.AddToClassList(c);
            b.focusable = false;
            return b;
        }

        /// <summary>Button with an icon to the left of its label.</summary>
        public static Button IconBtn(string icon, string text, Action onClick, params string[] classes)
        {
            var b = Btn(string.Empty, onClick, classes);
            b.AddToClassList("se-iconbtn");
            b.Add(new Icon(icon, 22));
            if (!string.IsNullOrEmpty(text)) b.Add(Text(text, "se-iconbtn-label"));
            return b;
        }

        public static T Children<T>(this T parent, params VisualElement[] children) where T : VisualElement
        {
            foreach (var c in children)
                if (c != null) parent.Add(c);
            return parent;
        }

        public static T Classes<T>(this T e, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static void Show(this VisualElement e, bool show)
        {
            var d = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (e.style.display != d) e.style.display = d;
        }

        public static bool IsShown(this VisualElement e) => e.resolvedStyle.display != DisplayStyle.None;

        public static void Set(this Label l, string text)
        {
            if (l.text != text) l.text = text;
        }

        public static void SetButton(this Button b, string text)
        {
            if (b.text != text) b.text = text;
        }

        public static void SetEnabledState(this VisualElement e, bool enabled)
        {
            e.EnableInClassList("is-disabled", !enabled);
        }

        /// <summary>
        /// Phone-friendly scrolling: momentum with a soft bounce, and the content moved as one group
        /// on the GPU instead of re-transforming every row on the CPU each frame.
        /// </summary>
        public static ScrollView Smooth(this ScrollView s)
        {
            s.contentContainer.usageHints = UsageHints.GroupTransform;
            s.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            s.elasticity = 0.08f;
            s.scrollDecelerationRate = 0.12f;
            // Lists only scroll up and down; a pixel of extra width must not add a sideways scrollbar.
            if (s.mode == ScrollViewMode.Vertical) s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            return s;
        }

        /// <summary>
        /// Shrinks a one-line label's font just enough to fit its parent's width (long single words
        /// such as "Investigación" on narrow tabs), never below <paramref name="minSize"/>.
        /// </summary>
        public static Label FitWidth(this Label label, float minSize = 8f, float margin = 2f)
        {
            var state = new FitState { Min = minSize, Margin = margin };
            label.userData = state;
            label.RegisterCallback<GeometryChangedEvent>(_ => Fit(label, state));
            return label;
        }

        /// <summary>Fits again after the text changed (a label stretched to its parent gets no geometry event).</summary>
        public static void Refit(this Label label)
        {
            if (label.userData is FitState state) label.schedule.Execute(() => Fit(label, state));
        }

        sealed class FitState
        {
            public float Min, Margin, Base;
        }

        static void Fit(Label label, FitState s)
        {
            var parent = label.parent;
            if (parent == null || string.IsNullOrEmpty(label.text)) return;
            float available = parent.contentRect.width - s.Margin;
            float current = label.resolvedStyle.fontSize;
            if (!(available > 4f) || !(current > 0f)) return;
            // The stylesheet size is the upper limit; remember it before the first override.
            if (s.Base <= 0f) s.Base = current;
            float width = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
            if (!(width > 0f)) return;
            // Text width scales with the font size, so one step lands on the fitting size.
            float fit = Mathf.Clamp(current * available / width, s.Min, s.Base);
            if (Mathf.Abs(fit - current) > 0.25f) label.style.fontSize = fit;
        }

        public static VisualElement Spacer()
        {
            var s = new VisualElement();
            s.style.flexGrow = 1;
            s.pickingMode = PickingMode.Ignore;
            return s;
        }

        public static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.top = 0;
            e.style.right = 0;
            e.style.bottom = 0;
        }
    }

    /// <summary>Rounded progress bar.</summary>
    public sealed class Bar : VisualElement
    {
        readonly VisualElement _fill;
        float _value = -1;

        public Bar(Color color, float height = 12)
        {
            AddToClassList("se-bar");
            style.height = height;
            _fill = Ui.Div("se-bar-fill");
            _fill.style.backgroundColor = color;
            Add(_fill);
            pickingMode = PickingMode.Ignore;
        }

        public Color Color
        {
            set => _fill.style.backgroundColor = value;
        }

        public float Value
        {
            get => _value;
            set
            {
                value = Mathf.Clamp01(float.IsNaN(value) ? 0 : value);
                if (Mathf.Abs(value - _value) < 0.001f) return;
                _value = value;
                _fill.style.width = Length.Percent(value * 100f);
            }
        }
    }
}
