using System;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// A coach mark: dims the screen except one element, circles it and explains it in a speech
    /// bubble from Nimbus, the little storm cloud. Steps that need an action let taps through to
    /// the highlighted element only.
    /// </summary>
    public sealed class Coach : VisualElement
    {
        const float Pad = 6f;
        const float Margin = 12f;

        readonly VisualElement[] _dim = new VisualElement[4];
        readonly VisualElement _ring, _bubble;
        readonly Label _title, _text, _hint, _counter;
        readonly Button _next, _skip;
        float _pulse;
        bool _needsAction;

        public event Action NextClicked;
        public event Action SkipClicked;

        /// <summary>Safe-area insets (left, top, right, bottom) the bubble must stay inside.</summary>
        public Vector4 Insets;

        public Coach()
        {
            AddToClassList("se-coach");
            pickingMode = PickingMode.Ignore;
            for (int i = 0; i < _dim.Length; i++)
            {
                _dim[i] = Ui.Div("se-coach-dim");
                // Swallow taps outside the highlighted element.
                _dim[i].RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                Add(_dim[i]);
            }
            _ring = Ui.Div("se-coach-ring");
            _ring.pickingMode = PickingMode.Ignore;
            Add(_ring);

            _bubble = Ui.Div("se-coach-bubble");
            var head = Ui.Div("se-row");
            head.pickingMode = PickingMode.Ignore;
            var face = new Icon("nimbus", 46);
            face.AddToClassList("se-coach-face");
            head.Add(face);
            var titles = Ui.Div("se-grow");
            titles.pickingMode = PickingMode.Ignore;
            _title = Ui.Text("", "se-coach-title");
            _counter = Ui.Text("", "se-coach-counter");
            titles.Children(_counter, _title);
            head.Add(titles);
            _bubble.Add(head);
            _text = Ui.Text("", "se-coach-text");
            _bubble.Add(_text);
            _hint = Ui.Text(L.T("Tap the glowing spot to continue."), "se-coach-hint");
            _bubble.Add(_hint);
            var actions = Ui.Div("se-row", "se-coach-actions");
            _skip = Ui.Btn(L.T("Skip tips"), () => SkipClicked?.Invoke(), "ghost", "small");
            _next = Ui.Btn(L.T("Next"), () => NextClicked?.Invoke(), "green", "small");
            actions.Children(_skip, Ui.Spacer(), _next);
            _bubble.Add(actions);
            Add(_bubble);
            this.Show(false);
        }

        /// <summary>Sets the step's text. <paramref name="needsAction"/> hides Next until the player acts.</summary>
        public void SetStep(string title, string text, bool needsAction, bool last, int index, int count)
        {
            _needsAction = needsAction;
            _title.Set(title);
            _text.Set(text);
            _counter.Set(count > 1 ? L.F("Tip {0} of {1}", index + 1, count) : L.T("New!"));
            _hint.Show(needsAction);
            _next.Show(!needsAction);
            _next.SetButton(last ? L.T("Got it!") : L.T("Next"));
            _bubble.style.visibility = Visibility.Hidden;
        }

        /// <summary>Positions the dim, ring and bubble around <paramref name="target"/> (coach-local), or centres the bubble when null.</summary>
        public void Place(Rect? target, float dt)
        {
            var size = contentRect.size;
            if (!(size.x > 10) || !(size.y > 10)) return;

            if (target is Rect t)
            {
                t = new Rect(t.x - Pad, t.y - Pad, t.width + Pad * 2, t.height + Pad * 2);
                SetRect(_dim[0], 0, 0, size.x, Mathf.Max(0, t.yMin));
                SetRect(_dim[1], 0, t.yMax, size.x, Mathf.Max(0, size.y - t.yMax));
                SetRect(_dim[2], 0, t.yMin, Mathf.Max(0, t.xMin), t.height);
                SetRect(_dim[3], t.xMax, t.yMin, Mathf.Max(0, size.x - t.xMax), t.height);
                for (int i = 1; i < _dim.Length; i++) _dim[i].Show(true);
                _ring.Show(true);
                SetRect(_ring, t.x, t.y, t.width, t.height);
                _pulse += dt;
                if (_pulse > 0.7f)
                {
                    _pulse = 0;
                    _ring.ToggleInClassList("pulse");
                }
            }
            else
            {
                SetRect(_dim[0], 0, 0, size.x, size.y);
                for (int i = 1; i < _dim.Length; i++) _dim[i].Show(false);
                _ring.Show(false);
            }

            var b = _bubble.layout;
            if (!(b.height > 1)) return;
            float width = Mathf.Min(380f, size.x - Margin * 2 - Insets.x - Insets.z);
            _bubble.style.width = width;
            float left, top;
            if (target is Rect r)
            {
                float below = size.y - Insets.w - r.yMax;
                float above = r.yMin - Insets.y;
                top = below >= b.height + 24 || below >= above ? r.yMax + 16 : r.yMin - b.height - 16;
                left = r.center.x - width * 0.5f;
            }
            else
            {
                top = (size.y - b.height) * 0.45f;
                left = (size.x - width) * 0.5f;
            }
            left = Mathf.Clamp(left, Margin + Insets.x, Mathf.Max(Margin + Insets.x, size.x - width - Margin - Insets.z));
            top = Mathf.Clamp(top, Margin + Insets.y, Mathf.Max(Margin + Insets.y, size.y - b.height - Margin - Insets.w));
            _bubble.style.left = left;
            _bubble.style.top = top;
            _bubble.style.visibility = Visibility.Visible;
        }

        public bool NeedsAction => _needsAction;

        static void SetRect(VisualElement e, float x, float y, float w, float h)
        {
            e.style.left = x;
            e.style.top = y;
            e.style.width = Mathf.Max(0, w);
            e.style.height = Mathf.Max(0, h);
        }
    }
}
