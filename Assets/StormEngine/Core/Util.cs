using System;
using System.Globalization;
using System.Text;

namespace StormEngine.Core
{
    /// <summary>UTC week clock. Weeks start on Monday 00:00 UTC; week 0 began on 1970-01-05.</summary>
    public static class WeekClock
    {
        public static int WeekOf(long unix)
        {
            long days = FloorDiv(unix, 86400);
            return (int)FloorDiv(days - 4, 7);
        }

        public static long WeekStart(int week) => ((long)week * 7 + 4) * 86400;
        public static long WeekEnd(int week) => WeekStart(week + 1);
        public static int ConditionIndex(int week, int count) => count <= 0 ? -1 : ((week % count) + count) % count;

        static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }

    /// <summary>Number formatting shared by UI and reports.</summary>
    public static class Num
    {
        static readonly string[] Suffixes =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc",
            "UDc", "DDc", "TDc", "QaDc", "QiDc", "SxDc", "SpDc", "OcDc", "NoDc", "Vg"
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>0 = suffixes (12.4M), 1 = scientific (1.24e7).</summary>
        public static int Notation;

        public static string Format(double v)
        {
            if (double.IsNaN(v)) return "0";
            if (double.IsInfinity(v)) return v > 0 ? "Inf" : "-Inf";
            if (v < 0) return "-" + Format(-v);
            if (v < 1000)
            {
                if (v < 10 && v != Math.Floor(v)) return v.ToString("0.##", Inv);
                if (v < 100 && v != Math.Floor(v)) return v.ToString("0.#", Inv);
                return Math.Floor(v).ToString("0", Inv);
            }

            int exp = (int)Math.Floor(Math.Log10(v));
            if (Notation == 1 || exp / 3 >= Suffixes.Length)
            {
                double mantissa = v / Math.Pow(10, exp);
                if (mantissa >= 9.995)
                {
                    mantissa /= 10;
                    exp++;
                }
                return mantissa.ToString("0.00", Inv) + "e" + exp.ToString(Inv);
            }

            int tier = exp / 3;
            double scaled = v / Math.Pow(1000, tier);
            if (scaled >= 999.5 && tier + 1 < Suffixes.Length)
            {
                tier++;
                scaled /= 1000;
            }
            string digits = scaled < 10 ? scaled.ToString("0.00", Inv) : scaled < 100 ? scaled.ToString("0.0", Inv) : scaled.ToString("0", Inv);
            return digits + Suffixes[tier];
        }

        public static string Mult(double v)
        {
            if (v < 1000) return "x" + (v < 10 ? v.ToString("0.##", Inv) : v < 100 ? v.ToString("0.#", Inv) : v.ToString("0", Inv));
            return "x" + Format(v);
        }

        public static string Percent(double v) => (v * 100).ToString(Math.Abs(v) < 0.1 ? "0.#" : "0", Inv) + "%";

        public static string Signed(double v) => (v >= 0 ? "+" : "") + v.ToString("0.##", Inv);

        public static string Fixed(double v, int decimals) => v.ToString("F" + decimals, Inv);

        public static string Int(long v) => v.ToString("#,0", Inv);

        public static string Time(double seconds)
        {
            if (seconds < 0) seconds = 0;
            long s = (long)Math.Floor(seconds);
            long d = s / 86400;
            long h = s % 86400 / 3600;
            long m = s % 3600 / 60;
            long sec = s % 60;
            if (d > 0) return L.F("{0}d {1}h", d, h);
            if (h > 0) return L.F("{0}h {1}m", h, m);
            if (m > 0) return L.F("{0}m {1}s", m, sec);
            return L.F("{0}s", sec);
        }

        public static string Seconds(double seconds) => L.F("{0}s", seconds < 10 ? seconds.ToString("0.0", Inv) : Math.Ceiling(seconds).ToString("0", Inv));
    }

    /// <summary>Minimal JSON writer used for submissions and the Cloud Code content export.</summary>
    public sealed class JsonWriter
    {
        readonly StringBuilder _sb = new StringBuilder();
        bool _needComma;

        public override string ToString() => _sb.ToString();

        public JsonWriter BeginObject() { Sep(); _sb.Append('{'); _needComma = false; return this; }
        public JsonWriter EndObject() { _sb.Append('}'); _needComma = true; return this; }
        public JsonWriter BeginArray() { Sep(); _sb.Append('['); _needComma = false; return this; }
        public JsonWriter EndArray() { _sb.Append(']'); _needComma = true; return this; }

        public JsonWriter Key(string k)
        {
            Sep();
            WriteString(k);
            _sb.Append(':');
            _needComma = false;
            return this;
        }

        public JsonWriter Value(string s)
        {
            Sep();
            if (s == null) _sb.Append("null");
            else WriteString(s);
            _needComma = true;
            return this;
        }

        public JsonWriter Value(double d)
        {
            Sep();
            if (double.IsNaN(d) || double.IsInfinity(d)) _sb.Append('0');
            else _sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
            _needComma = true;
            return this;
        }

        public JsonWriter Value(long v)
        {
            Sep();
            _sb.Append(v.ToString(CultureInfo.InvariantCulture));
            _needComma = true;
            return this;
        }

        public JsonWriter Value(bool b)
        {
            Sep();
            _sb.Append(b ? "true" : "false");
            _needComma = true;
            return this;
        }

        public JsonWriter Prop(string k, string v) => Key(k).Value(v);
        public JsonWriter Prop(string k, double v) => Key(k).Value(v);
        public JsonWriter Prop(string k, long v) => Key(k).Value(v);
        public JsonWriter Prop(string k, bool v) => Key(k).Value(v);

        void Sep()
        {
            if (_needComma) _sb.Append(',');
            _needComma = false;
        }

        void WriteString(string s)
        {
            _sb.Append('"');
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) _sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else _sb.Append(ch);
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
