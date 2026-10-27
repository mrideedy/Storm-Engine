using StormEngine.Core;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Base for the tabbed panels. Content lives in a scroll view; Refresh runs a few times per second.</summary>
    public abstract class PanelView : VisualElement
    {
        protected readonly StormApp App;
        protected readonly ScrollView Scroll;
        protected readonly VisualElement Body;
        protected readonly VisualElement Header;
        readonly Label _title;

        public string Id { get; }
        public string IconKind { get; }
        public string TabLabel { get; }

        protected Game G => App.Game;
        protected Content C => App.Content;

        protected PanelView(StormApp app, string id, string title, string icon, string tabLabel)
        {
            App = app;
            Id = id;
            IconKind = icon;
            TabLabel = tabLabel;
            style.flexGrow = 1;

            Header = Ui.Div("se-panel-header");
            Header.Add(new Icon(icon, 30));
            _title = Ui.Text(title, "se-panel-title");
            Header.Add(_title);
            Add(Header);

            Scroll = new ScrollView(ScrollViewMode.Vertical).Smooth();
            Scroll.AddToClassList("se-panel-scroll");
            Scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Add(Scroll);
            Body = Ui.Div("se-panel-body");
            Scroll.Add(Body);
        }

        /// <summary>
        /// Adds a balance chip on the right of the header, so the currency this panel spends stays in view
        /// while the list scrolls. Returns the value label to update in <see cref="Refresh"/>.
        /// </summary>
        protected Label HeaderBalance(string icon)
        {
            var chip = Ui.Div("se-panel-balance");
            chip.pickingMode = PickingMode.Ignore;
            chip.Add(new Icon(icon, 24));
            var value = Ui.Text("0", "se-panel-balance-value");
            chip.Add(value);
            Header.Add(chip);
            return value;
        }

        /// <summary>Called when the tab becomes visible.</summary>
        public virtual void OnShow() { }

        /// <summary>Called several times per second while visible.</summary>
        public abstract void Refresh();

        /// <summary>True when something in this panel deserves a badge on its tab.</summary>
        public virtual bool HasAttention() => false;

        protected static Label Section(string text) => Ui.Text(text.ToUpperInvariant(), "se-section");
    }
}
