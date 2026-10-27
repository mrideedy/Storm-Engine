using System;
using System.Collections.Generic;
using System.Text;

namespace StormEngine.Core
{
    /// <summary>
    /// Keeps offensive player names off the leaderboards and island visits. A name is checked when the
    /// player picks it and again wherever the game shows another player's name, so names set by modified
    /// clients stay hidden too. Matching ignores case, accents, look-alike digits (5h1t), spelled-out
    /// letters (f_u_c_k) and stretched letters (fuuuck). Words that hide inside innocent names
    /// (class, peacock, grape, computer, Pornchai) only count as whole words.
    /// </summary>
    public static class NameFilter
    {
        /// <summary>Blocked anywhere inside a word, after folding accents and look-alike digits.</summary>
        static readonly string[] Anywhere =
        {
            // English
            "fuck", "fvck", "fcuk", "phuck", "shit", "cunt", "nigg", "faggot", "whore", "slut", "bitch", "bastard",
            "asshole", "arsehole", "dickhead", "cocksuck", "wanker", "retard", "penis", "vagina", "pornhub", "pornstar",
            "hentai", "dildo", "blowjob", "handjob", "jizz", "orgasm", "masturbat", "molest", "pedophil", "paedophil",
            "hitler", "siegheil",
            // Spanish, Portuguese
            "mierda", "pendejo", "cabron", "maricon", "joder", "chingad", "gilipollas", "culero", "hijodeputa",
            "caralho", "buceta", "fodase", "foder", "viado", "arrombado", "cuzao", "piroca",
            // French, German, Italian
            "putain", "salope", "connard", "encule", "batard", "scheisse", "fotze", "hurensohn", "wichser",
            "schlampe", "arschloch", "ficken", "ficker", "missgeburt", "cazzo", "vaffanculo", "stronzo", "puttana",
            "minchia", "coglion", "frocio", "porcodio", "dioporco",
            // Turkish, Polish, Indonesian
            "orospu", "siktir", "sikis", "yarrak", "amcik", "pezevenk", "kahpe", "gotveren", "aminakoy", "ananisik",
            "kurw", "chuj", "pierdol", "pierdal", "jebac", "jeban", "pizda", "dziwka", "zajeb",
            "kontol", "memek", "ngentot", "bangsat", "bajingan", "jancok", "jancuk", "lonte", "pelacur",
            // Russian
            "хуй", "хуе", "пизд", "ебат", "ебан", "ебал", "сука", "мудак", "пидор", "пидар", "гандон", "шлюх",
            "залуп", "блять", "блядь", "жопа",
            // Vietnamese typed without accents
            "ditme", "ditconme",
        };

        /// <summary>Blocked only as a whole word ("big_ass", "BigAss", "a55"): inside other words they are innocent.</summary>
        static readonly HashSet<string> Words = new HashSet<string>(StringComparer.Ordinal)
        {
            "ass", "arse", "cock", "dick", "tits", "cum", "fag", "fags", "fck", "rape", "raped", "rapist", "sex", "anal",
            "anus", "pussy", "twat", "wank", "boob", "boobs", "porn", "porno", "pedo", "nazi", "nazis", "kkk",
            "puta", "puto", "verga", "polla", "zorra", "porra", "foda", "bosta", "merda", "merde", "pute", "nique",
            "chatte", "fick", "nutte", "hure", "troia", "figa", "culo", "amk", "aq", "ibne", "sik", "cipa", "cwel",
            "titit", "perek", "бля", "vcl", "vkl", "dcm", "dmm", "cmm", "clgt",
        };

        /// <summary>Matched with their accents (Vietnamese, Thai, Spanish): without them they are innocent words.</summary>
        static readonly string[] AnywhereExact = { "địt", "lồn", "cặc", "buồi", "đéo", "coño", "เย็ด", "ควย", "เหี้ย" };

        static readonly HashSet<string> WordsExact = new HashSet<string>(StringComparer.Ordinal) { "đụ", "đĩ" };

        static readonly string[] AnywhereSingle = Singles(Anywhere);
        static readonly HashSet<string> WordsSingle = new HashSet<string>(Singles(Words), StringComparer.Ordinal);
        static readonly Dictionary<char, string> Folds = BuildFolds();

        /// <summary>True if the name may be shown to other players. Empty names are allowed (the game shows a default).</summary>
        public static bool IsAllowed(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            // Unity adds "#1234" to keep names unique.
            int tag = name.LastIndexOf('#');
            if (tag > 0) name = name.Substring(0, tag);

            string lower = name.ToLowerInvariant();
            foreach (var bad in AnywhereExact)
                if (lower.IndexOf(bad, StringComparison.Ordinal) >= 0) return false;

            foreach (var part in Parts(name))
            {
                if (HasBadPiece(Fold(part, true))) return false;
                foreach (var word in CamelWords(part))
                    if (IsBadWord(word)) return false;
            }
            return true;
        }

        /// <summary>The name itself, or <paramref name="fallback"/> when it should not be shown.</summary>
        public static string Display(string name, string fallback) => IsAllowed(name) ? name : fallback;

        static bool HasBadPiece(string folded)
        {
            if (folded.Length == 0) return false;
            string squeezed = Squeeze(folded, 1);
            foreach (var bad in Anywhere)
                if (folded.IndexOf(bad, StringComparison.Ordinal) >= 0) return true;
            // "fuuuck", "shiiit": compare without repeated letters (only for words that have none).
            foreach (var bad in AnywhereSingle)
                if (squeezed.IndexOf(bad, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        static bool IsBadWord(string word)
        {
            if (WordsExact.Contains(word.ToLowerInvariant())) return true;
            return IsListed(Fold(word, true)) || IsListed(Fold(word, false));
        }

        static bool IsListed(string w) =>
            w.Length > 0 && (Words.Contains(w) || Words.Contains(Squeeze(w, 2)) || WordsSingle.Contains(Squeeze(w, 1)));

        /// <summary>
        /// Splits at separators (_, -, spaces, dots...). Runs of one- and two-letter pieces are also joined,
        /// which catches spelled-out words (f_u_c_k, sh-it) without gluing normal words together.
        /// </summary>
        static List<string> Parts(string name)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            foreach (char ch in name)
            {
                if (IsWordChar(ch)) current.Append(ch);
                else if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            if (current.Length > 0) parts.Add(current.ToString());

            int count = parts.Count;
            var run = new StringBuilder();
            int runLength = 0;
            for (int i = 0; i <= count; i++)
            {
                if (i < count && parts[i].Length <= 2)
                {
                    run.Append(parts[i]);
                    runLength++;
                    continue;
                }
                if (runLength >= 2) parts.Add(run.ToString());
                run.Clear();
                runLength = 0;
            }
            return parts;
        }

        static bool IsWordChar(char ch)
        {
            if (char.IsLetterOrDigit(ch)) return true;
            switch (char.GetUnicodeCategory(ch))
            {
                case System.Globalization.UnicodeCategory.NonSpacingMark:
                case System.Globalization.UnicodeCategory.SpacingCombiningMark:
                    return true;
            }
            return Leet(ch) != ch;
        }

        /// <summary>Splits "BigAss99" into "Big" and "Ass99".</summary>
        static List<string> CamelWords(string part)
        {
            var words = new List<string>();
            int start = 0;
            for (int i = 1; i < part.Length; i++)
                if (char.IsUpper(part[i]) && char.IsLower(part[i - 1]))
                {
                    words.Add(part.Substring(start, i - start));
                    start = i;
                }
            words.Add(part.Substring(start));
            return words;
        }

        /// <summary>Lower case letters only: accents removed, look-alike digits read as letters when <paramref name="leet"/>.</summary>
        static string Fold(string s, bool leet)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char raw in s.ToLowerInvariant())
            {
                char ch = leet ? Leet(raw) : raw;
                if (Folds.TryGetValue(ch, out string folded)) sb.Append(folded);
                else if (char.IsLetter(ch)) sb.Append(ch);
            }
            return sb.ToString();
        }

        static char Leet(char ch)
        {
            switch (ch)
            {
                case '0': return 'o';
                case '1': case '!': return 'i';
                case '3': case '€': return 'e';
                case '4': case '@': return 'a';
                case '5': case '$': return 's';
                case '7': return 't';
                case '8': return 'b';
                case '9': return 'g';
                case '|': return 'l';
                default: return ch;
            }
        }

        /// <summary>Collapses runs of the same letter to at most <paramref name="max"/>.</summary>
        static string Squeeze(string s, int max)
        {
            var sb = new StringBuilder(s.Length);
            int run = 0;
            for (int i = 0; i < s.Length; i++)
            {
                run = i > 0 && s[i] == s[i - 1] ? run + 1 : 1;
                if (run <= max) sb.Append(s[i]);
            }
            return sb.ToString();
        }

        static string[] Singles(IEnumerable<string> words)
        {
            var list = new List<string>();
            foreach (var w in words)
                if (Squeeze(w, 1) == w) list.Add(w);
            return list.ToArray();
        }

        static Dictionary<char, string> BuildFolds()
        {
            var map = new Dictionary<char, string> { { 'ß', "ss" }, { 'æ', "ae" }, { 'œ', "oe" }, { 'ё', "е" } };
            string[] groups =
            {
                "aàáâãäåāăąạảấầẩẫậắằẳẵặ", "cçćč", "dďđ", "eèéêëēėęěẹẻẽếềểễệ", "gğ", "iìíîïīįıỉị", "lł", "nñńň",
                "oòóôõöøōőọỏốồổỗộớờởỡợơ", "rŕř", "sśšş", "tťţ", "uùúûüūůűụủứừửữựư", "yýÿỳỵỷỹ", "zźżž",
            };
            foreach (var group in groups)
                for (int i = 1; i < group.Length; i++)
                    map[group[i]] = group[0].ToString();
            return map;
        }
    }
}
