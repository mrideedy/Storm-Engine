using System;
using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>One thing that opens up at an Engine Level.</summary>
    public struct UnlockItem
    {
        public string Icon;
        public string Kind;
        public string Name;
        public string Desc;
    }

    public static class Unlocks
    {
        /// <summary>Everything that becomes available at exactly this Engine Level, most important first.</summary>
        public static List<UnlockItem> At(Game g, int level)
        {
            var c = g.C;
            var list = new List<UnlockItem>();
            foreach (var f in c.Features)
                if (f.Level == level && level > 1) list.Add(new UnlockItem { Icon = f.Icon, Kind = L.T("NEW FEATURE"), Name = f.Name, Desc = f.Desc });
            foreach (var m in c.Machines)
                if (m.UnlockLevel == level && level > 1) list.Add(new UnlockItem { Icon = m.Icon, Kind = L.T("NEW MACHINE"), Name = m.Name, Desc = m.Desc });
            foreach (var m in c.Modules)
                if (m.UnlockLevel == level && m.RequiresDiscovery == null && m.RequiresPrestiges == 0)
                    list.Add(new UnlockItem { Icon = m.Icon, Kind = L.T("MODULE"), Name = m.Name, Desc = m.Desc });
            foreach (var r in c.Research)
                if (r.UnlockLevel == level && level > g.FeatureLevel("research"))
                    list.Add(new UnlockItem { Icon = r.Icon, Kind = L.T("RESEARCH"), Name = r.Name, Desc = r.Desc });
            foreach (var cos in c.Cosmetics)
                if (!cos.Default && cos.Bundle == null && cos.UnlockLevel == level && level > 1 && cos.RequiresDiscovery == null && cos.RequiresPrestiges == 0)
                    list.Add(new UnlockItem
                    {
                        Icon = "brush", Kind = (cos.Exclusive ? L.T("EXCLUSIVE") : L.T("STYLE")) + " · " + SlotName(cos.Slot).ToUpperInvariant(), Name = cos.Name,
                        Desc = (StyleText.Bonus(c, cos) ?? "") + "  ·  " + (cos.CostGems > 0 ? L.F("{0} Gems", Num.Int(cos.CostGems)) : L.T("Free"))
                    });
            foreach (var b in c.Bundles)
                if (b.UnlockLevel == level && level > 1)
                    list.Add(new UnlockItem { Icon = "play", Kind = L.T("AD BUNDLE"), Name = b.Name, Desc = L.F("A complete environment for {0} rewarded ads.", b.AdsRequired) });
            foreach (var d in c.Decorations)
                if (d.UnlockLevel == level && level > 1 && d.Bundle == null && !d.Limited && level >= g.FeatureLevel("island"))
                    list.Add(new UnlockItem { Icon = "island", Kind = L.T("DECORATION"), Name = d.Name, Desc = d.Desc });
            foreach (var e in c.Expansions)
                if (e.UnlockLevel == level)
                    list.Add(new UnlockItem { Icon = "island", Kind = L.T("TERRAIN EXPANSION"), Name = e.Name, Desc = e.Desc });
            return list;
        }

        public static string SlotName(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Skin: return L.T("Machine Skin");
                case CosmeticSlot.Cloud: return L.T("Storm Cloud");
                case CosmeticSlot.Bolt: return L.T("Lightning");
                case CosmeticSlot.World: return L.T("World");
                default: return L.T("Title");
            }
        }

        public static VisualElement Row(UnlockItem u, bool locked = false)
        {
            var row = Ui.Div("se-unlock");
            row.EnableInClassList("locked", locked);
            var box = Ui.Div("se-iconbox", "small");
            box.Add(new Icon(u.Icon, 28));
            row.Add(box);
            var col = Ui.Div("se-grow");
            col.Add(Ui.Text(u.Kind, "se-unlock-kind"));
            col.Add(Ui.Text(u.Name, "se-h3"));
            if (!string.IsNullOrEmpty(u.Desc)) col.Add(Ui.Text(u.Desc, "se-small"));
            row.Add(col);
            return row;
        }
    }

    /// <summary>Engine Level badge with an XP ring. Opens the level road.</summary>
    public sealed class LevelPill : VisualElement
    {
        readonly StormApp _app;
        readonly Label _level;
        readonly VisualElement _ring;
        int _shownLevel = -1;
        float _progress = -1;
        float _pop;

        public event Action Clicked;

        public LevelPill(StormApp app)
        {
            _app = app;
            AddToClassList("se-levelpill");
            _ring = new VisualElement { pickingMode = PickingMode.Ignore };
            _ring.AddToClassList("se-levelring");
            _ring.generateVisualContent += PaintRing;
            Add(_ring);
            var badge = Ui.Div("se-levelbadge");
            badge.pickingMode = PickingMode.Ignore;
            _level = Ui.Text("1", "se-levelbadge-num");
            badge.Add(_level);
            Add(badge);
            RegisterCallback<ClickEvent>(_ => Clicked?.Invoke());
        }

        void PaintRing(MeshGenerationContext ctx)
        {
            var r = _ring.contentRect;
            if (r.width < 4) return;
            var p = ctx.painter2D;
            float rad = Mathf.Min(r.width, r.height) * 0.5f - 3f;
            Draw.ArcStroke(p, r.center, rad, 0, 360, new Color(0.08f, 0.12f, 0.23f, 0.85f), 6f);
            if (_progress > 0.002f) Draw.ArcStroke(p, r.center, rad, -90, -90 + 360 * _progress, Palette.Yellow, 4.5f);
        }

        public void Refresh(float dt)
        {
            var g = _app.Game;
            if (g.Level != _shownLevel)
            {
                if (_shownLevel > 0) _pop = 1f;
                _shownLevel = g.Level;
                _level.Set(g.Level.ToString());
            }
            float progress = (float)g.LevelProgress;
            if (Mathf.Abs(progress - _progress) > 0.003f)
            {
                _progress = progress;
                _ring.MarkDirtyRepaint();
            }
            if (_pop > 0)
            {
                _pop = Mathf.Max(0, _pop - dt * 2f);
                float s = 1f + Mathf.Sin(_pop * Mathf.PI) * 0.18f;
                style.scale = new Scale(new Vector2(s, s));
            }
        }
    }

    /// <summary>Cartoon sunburst: alternating rays around a soft centre.</summary>
    public sealed class Sunburst : VisualElement
    {
        readonly Color _color;
        readonly int _rays;

        public Sunburst(Color color, int rays = 14)
        {
            _color = color;
            _rays = rays;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 4) return;
            var p = ctx.painter2D;
            var c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            float step = Mathf.PI * 2f / _rays;
            p.fillColor = _color;
            for (int i = 0; i < _rays; i++)
            {
                float a = i * step;
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(c + new Vector2(Mathf.Cos(a - step * 0.25f), Mathf.Sin(a - step * 0.25f)) * radius);
                p.LineTo(c + new Vector2(Mathf.Cos(a + step * 0.25f), Mathf.Sin(a + step * 0.25f)) * radius);
                p.ClosePath();
                p.Fill();
            }
        }
    }

    public sealed class LevelUpPopup : Popup
    {
        float _age;
        readonly VisualElement _burst;

        public LevelUpPopup(StormApp app, int level)
            : base(L.F("LEVEL {0}!", level), L.T("Your storm engine grew stronger"), "star")
        {
            AddToClassList("se-levelup");
            _burst = new Sunburst(new Color(1f, 0.85f, 0.35f, 0.16f));
            _burst.AddToClassList("se-levelup-burst");
            HeadEl.Insert(0, _burst);
            var items = Unlocks.At(app.Game, level);
            if (items.Count == 0)
                BodyEl.Add(Ui.Text(L.T("Keep building! New machines, modules and styles unlock as your Engine Level rises."), "se-body"));
            else
            {
                BodyEl.Add(Ui.Text(L.T("UNLOCKED"), "se-section"));
                foreach (var u in items) BodyEl.Add(Unlocks.Row(u));
            }
            int next = app.Game.NextUnlockLevel();
            if (next > 0)
            {
                var upcoming = Unlocks.At(app.Game, next);
                if (upcoming.Count > 0)
                {
                    var hint = Ui.Text(L.F("Next at Lv {0}: {1}", next, upcoming[0].Name) + (upcoming.Count > 1 ? " " + L.F("and {0} more", upcoming.Count - 1) : ""), "se-small");
                    hint.style.marginTop = 8;
                    BodyEl.Add(hint);
                }
            }
            // A level-up is a happy moment: the right time for Play's (rare) rating card.
            Action(L.T("Awesome!"), () => app.MaybeAskForReview(level), "green");
        }

        public override void Tick(float dt)
        {
            _age += dt;
            _burst.style.rotate = new Rotate(Angle.Degrees(_age * 25f));
        }
    }

    /// <summary>Roadmap of upcoming Engine Level rewards.</summary>
    public sealed class LevelRoadPopup : Popup
    {
        public LevelRoadPopup(StormApp app)
            : base(L.F("ENGINE LEVEL {0}", app.Game.Level), BuildSub(app), "star")
        {
            var g = app.Game;
            BodyEl.Add(Ui.Text(L.T("Earn XP by building machines, unleashing storms, discovering phenomena and completing goals."), "se-small"));
            int shown = 0;
            for (int level = g.Level + 1; level <= app.Content.B.MaxEngineLevel && shown < 10; level++)
            {
                var items = Unlocks.At(g, level);
                if (items.Count == 0) continue;
                shown++;
                var head = Ui.Div("se-road-level");
                head.Add(Ui.Text(L.F("LV {0}", level), "se-road-num"));
                head.Add(Ui.Text(level == g.Level + 1 ? L.T("NEXT") : "", "se-road-next"));
                BodyEl.Add(head);
                foreach (var u in items) BodyEl.Add(Unlocks.Row(u, true));
            }
            if (shown == 0) BodyEl.Add(Ui.Text(L.T("Everything is unlocked. You are a true storm engineer!"), "se-body"));
            Action(L.T("Close"), null, "green");
        }

        static string BuildSub(StormApp app)
        {
            var g = app.Game;
            if (g.Level >= app.Content.B.MaxEngineLevel) return L.T("Max level reached");
            return L.F("{0} / {1} XP to level {2}", Num.Int((long)g.Xp), Num.Int((long)g.XpToNext), g.Level + 1);
        }
    }
}
