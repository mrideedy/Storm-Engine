using System;
using System.Collections.Generic;
using System.Globalization;

namespace StormEngine.Core
{
    /// <summary>
    /// Translations. The English text is the key: <see cref="T"/> returns the current language's
    /// text, or the English original when there is no translation. <see cref="F"/> translates a
    /// pattern with {0}-style placeholders, then fills them in. Content names and descriptions are
    /// translated once at startup (<see cref="ContentText"/>).
    /// </summary>
    public static class L
    {
        static Dictionary<string, string> _table;

        /// <summary>Language code in use ("en", "ja", "zh-Hans"...).</summary>
        public static string Language { get; private set; } = "en";

        public static void Use(string code, Dictionary<string, string> table)
        {
            Language = string.IsNullOrEmpty(code) ? "en" : code;
            _table = table;
        }

        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || _table == null) return english;
            return _table.TryGetValue(english, out var t) && !string.IsNullOrEmpty(t) ? t : english;
        }

        /// <summary>
        /// A text that needs its own translation in one place, such as a short tab name: looks up
        /// "context:English" first and falls back to the normal translation. Mark the key with
        /// <see cref="N"/> so the export lists it.
        /// </summary>
        public static string T(string english, string context)
        {
            if (_table != null && !string.IsNullOrEmpty(context) && _table.TryGetValue(context + ":" + english, out var t) && !string.IsNullOrEmpty(t)) return t;
            return T(english);
        }

        /// <summary>Marks text for the translation export without translating it (for static tables; translate with T where shown).</summary>
        public static string N(string english) => english;

        public static string F(string english, params object[] args)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, T(english), args);
            }
            catch (FormatException)
            {
                // A broken translation never breaks the game: fall back to English.
                return string.Format(CultureInfo.InvariantCulture, english, args);
            }
        }
    }

    /// <summary>Every translatable text in <see cref="Content"/>, for translating it and for the key export.</summary>
    public static class ContentText
    {
        /// <summary>Replaces each text with <paramref name="map"/>(text).</summary>
        public static void Visit(Content c, Func<string, string> map)
        {
            string M(string s) => string.IsNullOrEmpty(s) ? s : map(s);
            string[] All(string[] a)
            {
                if (a == null) return a;
                for (int i = 0; i < a.Length; i++) a[i] = M(a[i]);
                return a;
            }

            foreach (var s in c.Stats) s.Name = M(s.Name);
            foreach (var m in c.Machines)
            {
                m.Name = M(m.Name);
                m.Desc = M(m.Desc);
            }
            foreach (var b in c.Boosts)
            {
                b.Name = M(b.Name);
                b.Desc = M(b.Desc);
            }
            foreach (var p in c.Phenomena)
            {
                p.Name = M(p.Name);
                p.Desc = M(p.Desc);
                p.PerkText = M(p.PerkText);
            }
            foreach (var m in c.Modules)
            {
                m.Name = M(m.Name);
                m.Short = M(m.Short);
                m.Desc = M(m.Desc);
            }
            foreach (var r in c.Research)
            {
                r.Name = M(r.Name);
                r.Desc = M(r.Desc);
            }
            foreach (var w in c.Weekly)
            {
                w.Name = M(w.Name);
                All(w.Lines);
            }
            foreach (var g in c.Goals) g.Text = M(g.Text);
            foreach (var f in c.Features)
            {
                f.Name = M(f.Name);
                f.Desc = M(f.Desc);
            }
            foreach (var u in c.Upgrades)
            {
                u.Name = M(u.Name);
                u.Desc = M(u.Desc);
            }
            foreach (var cos in c.Cosmetics)
            {
                cos.Name = M(cos.Name);
                cos.Desc = M(cos.Desc);
            }
            foreach (var b in c.Bundles)
            {
                b.Name = M(b.Name);
                b.Desc = M(b.Desc);
            }
            foreach (var d in c.Decorations)
            {
                d.Name = M(d.Name);
                d.Desc = M(d.Desc);
            }
            foreach (var e in c.Expansions)
            {
                e.Name = M(e.Name);
                e.Desc = M(e.Desc);
            }
            foreach (var a in c.AdPlacements)
            {
                a.Name = M(a.Name);
                a.Desc = M(a.Desc);
            }
            foreach (var p in c.Products)
            {
                p.Name = M(p.Name);
                p.Desc = M(p.Desc);
                p.Tag = M(p.Tag);
            }
            foreach (var m in c.Missions) m.Text = M(m.Text);
        }

        /// <summary>Translates the content into the current language (<see cref="L"/>).</summary>
        public static void Localize(Content c) => Visit(c, L.T);

        /// <summary>
        /// Translates content that was already translated into another language: the English
        /// originals come from a fresh copy, field by field in the same order.
        /// </summary>
        public static void Relocalize(Content c)
        {
            var english = new List<string>();
            Visit(Content.CreateDefault(), s =>
            {
                english.Add(s);
                return s;
            });
            int i = 0;
            Visit(c, s => i < english.Count ? L.T(english[i++]) : s);
        }

        /// <summary>All distinct content texts, in order.</summary>
        public static List<string> Collect(Content c)
        {
            var list = new List<string>();
            var seen = new HashSet<string>();
            Visit(c, s =>
            {
                if (seen.Add(s)) list.Add(s);
                return s;
            });
            return list;
        }
    }
}
