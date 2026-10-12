using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace StickmanJumper.Maps
{
    /// <summary>
    /// Карта ↔ короткий текстовый код для обмена в мессенджерах: «SJM1.» + сжатые данные (base64 без +/=).
    /// Координаты — с точностью до сантиметра, масштаб — до тысячной, поворот — до десятой градуса.
    /// Код можно вставлять вместе с окружающим текстом сообщения: <see cref="TryDecode"/> найдёт его сам.
    /// </summary>
    public static class MapCode
    {
        public const string Prefix = "SJM1.";
        const byte FormatVersion = 1;
        const int MaxPieces = 3000, MaxPlatforms = 64, MaxArt = 512, MaxText = 200;

        public static string Encode(MapData map)
        {
            using var raw = new MemoryStream();
            using (var w = new BinaryWriter(raw, Encoding.UTF8, true))
            {
                w.Write(FormatVersion);
                w.Write(Clip(map.Name, 60));
                w.Write((byte)(map.Verified ? 1 : 0));
                WriteContent(w, map);
            }
            using var packed = new MemoryStream();
            using (var z = new DeflateStream(packed, System.IO.Compression.CompressionLevel.Optimal, true))
                raw.WriteTo(z);
            return Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>Достаёт карту из кода (или из текста, где он есть). error — что не так, по-русски/английски для игрока.</summary>
        public static bool TryDecode(string text, out MapData map, out string error)
        {
            map = null;
            error = null;
            string data = Find(text);
            if (data == null)
            {
                error = "no code";
                return false;
            }
            try
            {
                string b64 = data.Replace('-', '+').Replace('_', '/');
                b64 += new string('=', (4 - b64.Length % 4) % 4);
                using var packed = new MemoryStream(Convert.FromBase64String(b64));
                using var z = new DeflateStream(packed, CompressionMode.Decompress);
                using var raw = new MemoryStream();
                z.CopyTo(raw);
                raw.Position = 0;
                using var r = new BinaryReader(raw, Encoding.UTF8);
                byte version = r.ReadByte();
                if (version != FormatVersion)
                {
                    error = "newer version";
                    return false;
                }
                var m = new MapData { Name = Clip(r.ReadString(), 60) };
                byte flags = r.ReadByte();
                ReadContent(r, m);
                m.Imported = true;
                m.AuthorVerified = (flags & 1) != 0;
                if (m.AuthorVerified) m.VerifiedHash = ContentHash(m); // the author proved it can be done
                map = m;
                return true;
            }
            catch (Exception e)
            {
                error = e is EndOfStreamException || e is FormatException || e is InvalidDataException ? "broken code" : e.Message;
                return false;
            }
        }

        /// <summary>Есть ли в тексте код карты.</summary>
        public static bool Contains(string text) => Find(text) != null;

        /// <summary>Отпечаток содержимого карты (без имени): меняется от любой правки, которая влияет на прыжок или вид.</summary>
        public static string ContentHash(MapData map)
        {
            using var raw = new MemoryStream();
            using (var w = new BinaryWriter(raw, Encoding.UTF8, true)) WriteContent(w, map);
            ulong h = 14695981039346656037UL; // FNV-1a
            foreach (byte b in raw.ToArray())
            {
                h ^= b;
                h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }

        static string Find(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int at = text.IndexOf(Prefix, StringComparison.Ordinal);
            if (at < 0) return null;
            var sb = new StringBuilder();
            for (int i = at + Prefix.Length; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) continue; // a messenger may wrap a long code
                if (char.IsLetterOrDigit(c) && c < 128 || c == '-' || c == '_') sb.Append(c);
                else break;
            }
            return sb.Length >= 8 ? sb.ToString() : null;
        }

        // ------------------------------------------------------------------ content

        static void WriteContent(BinaryWriter w, MapData map)
        {
            w.Write((byte)(map.Night ? 1 : 0));
            w.Write((ushort)Mathf.RoundToInt(Mathf.Clamp(map.TimeLimit, MapData.MinTime, MapData.MaxTime) * 10f));
            w.Write((short)Mathf.RoundToInt(Mathf.Clamp(map.Wind, -MapData.MaxWind, MapData.MaxWind) * 100f));

            var art = new List<string>();
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var p in map.Pieces)
                if (!index.ContainsKey(p.Art ?? string.Empty))
                {
                    index[p.Art ?? string.Empty] = art.Count;
                    art.Add(p.Art ?? string.Empty);
                }
            WriteCount(w, art.Count);
            foreach (var a in art) w.Write(Clip(a, 64));

            WriteCount(w, map.Pieces.Count);
            foreach (var p in map.Pieces)
            {
                WriteCount(w, index[p.Art ?? string.Empty]);
                WriteCm(w, p.X);
                WriteCm(w, p.Y);
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(p.ScaleX, -32f, 32f) * 1000f));
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(p.ScaleY, -32f, 32f) * 1000f));
                w.Write((short)Mathf.RoundToInt(Mathf.DeltaAngle(0f, p.Rotation) * 10f));
                w.Write((sbyte)Mathf.Clamp(p.Layer, -100, 100));
                w.Write((byte)(p.Solid ? 1 : 0));
            }

            WriteStrip(w, map.Start);
            WriteStrip(w, map.Target);
            WriteCount(w, map.Platforms.Count);
            foreach (var s in map.Platforms) WriteStrip(w, s);
        }

        static void ReadContent(BinaryReader r, MapData map)
        {
            map.Night = (r.ReadByte() & 1) != 0;
            map.TimeLimit = Mathf.Clamp(r.ReadUInt16() / 10f, MapData.MinTime, MapData.MaxTime);
            map.Wind = Mathf.Clamp(r.ReadInt16() / 100f, -MapData.MaxWind, MapData.MaxWind);

            int artCount = ReadCount(r, MaxArt);
            var art = new string[artCount];
            for (int i = 0; i < artCount; i++) art[i] = Clip(r.ReadString(), 64);

            int pieces = ReadCount(r, MaxPieces);
            map.Pieces = new List<MapPiece>(pieces);
            for (int i = 0; i < pieces; i++)
            {
                int a = ReadCount(r, artCount - 1);
                map.Pieces.Add(new MapPiece
                {
                    Art = art[a],
                    X = ReadCm(r),
                    Y = ReadCm(r),
                    ScaleX = r.ReadInt16() / 1000f,
                    ScaleY = r.ReadInt16() / 1000f,
                    Rotation = r.ReadInt16() / 10f,
                    Layer = r.ReadSByte(),
                    Solid = (r.ReadByte() & 1) != 0,
                });
            }

            map.Start = ReadStrip(r);
            map.Target = ReadStrip(r);
            int platforms = ReadCount(r, MaxPlatforms);
            map.Platforms = new List<MapStrip>(platforms);
            for (int i = 0; i < platforms; i++) map.Platforms.Add(ReadStrip(r));
        }

        static void WriteStrip(BinaryWriter w, MapStrip s)
        {
            s ??= new MapStrip();
            WriteCm(w, s.X);
            WriteCm(w, s.Y);
            WriteCm(w, Mathf.Max(0f, s.Width));
            w.Write((byte)(s.Left ? 1 : 0));
        }

        static MapStrip ReadStrip(BinaryReader r) => new MapStrip(ReadCm(r), ReadCm(r), Mathf.Max(0f, ReadCm(r)), (r.ReadByte() & 1) != 0);

        // Metres as whole centimetres, zig-zag varint (small numbers — few bytes).
        static void WriteCm(BinaryWriter w, float metres)
        {
            long v = (long)Math.Round(Mathf.Clamp(metres, -100000f, 100000f) * 100.0);
            ulong z = (ulong)((v << 1) ^ (v >> 63));
            WriteVarint(w, z);
        }

        static float ReadCm(BinaryReader r)
        {
            ulong z = ReadVarint(r);
            long v = (long)(z >> 1) ^ -(long)(z & 1);
            return v / 100f;
        }

        static void WriteCount(BinaryWriter w, int n) => WriteVarint(w, (ulong)Mathf.Max(0, n));

        static int ReadCount(BinaryReader r, int max)
        {
            ulong n = ReadVarint(r);
            if (n > (ulong)Mathf.Max(0, max)) throw new InvalidDataException("count out of range");
            return (int)n;
        }

        static void WriteVarint(BinaryWriter w, ulong v)
        {
            while (v >= 0x80)
            {
                w.Write((byte)(v | 0x80));
                v >>= 7;
            }
            w.Write((byte)v);
        }

        static ulong ReadVarint(BinaryReader r)
        {
            ulong v = 0;
            for (int shift = 0; shift < 64; shift += 7)
            {
                byte b = r.ReadByte();
                v |= (ulong)(b & 0x7f) << shift;
                if ((b & 0x80) == 0) return v;
            }
            throw new InvalidDataException("bad number");
        }

        static string Clip(string s, int max)
        {
            s ??= string.Empty;
            s = s.Length > MaxText ? s.Substring(0, MaxText) : s;
            return s.Length > max ? s.Substring(0, max) : s;
        }
    }
}
