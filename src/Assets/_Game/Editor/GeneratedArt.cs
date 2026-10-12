using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Наша собственная простая графика, нарисованная кодом (не заглушки): капсула для конечностей, круг, небо,
    /// звёзды, иконки часов/ветра/лупы. Рисуем через расстояние до фигуры — края получаются гладкими.
    /// </summary>
    public static class GeneratedArt
    {
        public const string Folder = "Assets/_Game/Art/Generated";

        public static void EnsureAll()
        {
            Directory.CreateDirectory(Folder);
            Make("White", 4, 4, 4f, new Vector2(0.5f, 0.5f), Vector4.zero, true, (x, y) => Color.white);
            Make("Circle", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, false,
                (x, y) => White(Circle(x, y, 64, 64, 62)));
            // Soft round light (night: a halo behind the stickman so he shows against the dark).
            Make("Glow", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, true, (x, y) =>
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f)) / 63f);
                float a = 1f - d * d * (3f - 2f * d);
                return new Color(1f, 1f, 1f, a * a);
            });
            // 32 px wide capsule, 16 px caps kept by 9-slice; PPU 320 → 0.1 m thick, caps 0.05 m.
            Make("Limb", 32, 64, 320f, new Vector2(0.5f, 1f), new Vector4(0, 16, 0, 16), true,
                (x, y) => White(Capsule(x, y, 16, 16, 16, 48, 15.5f)));
            // Crumpled graph paper, tileable: 512 px = 8 cells of 64 px; PPU 40 → a cell is 1.6 m.
            Make("Paper", 512, 512, 40f, new Vector2(0.5f, 0.5f), Vector4.zero, true, Paper);
            // Ground haze: white, fading out upwards.
            Make("FogBand", 4, 128, 128f, new Vector2(0.5f, 0f), Vector4.zero, true, (x, y) =>
                new Color(1f, 1f, 1f, Mathf.Pow(1f - y / 127f, 1.6f)));
            // One dash of the guide line above the target flag: 40 px red, 24 px gap; PPU 80 → 0.5 m + 0.3 m.
            Make("Dash", 8, 64, 80f, new Vector2(0.5f, 0f), Vector4.zero, true, (x, y) =>
                y < 40 ? new Color(0.86f, 0.1f, 0.1f, 0.9f) : Color.clear);
            Make("Star", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, false, (x, y) =>
            {
                float d = Star(x, y, 64, 62, 60, 25);
                var fill = new Color(1f, 0.80f, 0.16f);
                var edge = new Color(0.55f, 0.32f, 0.02f);
                var c = d > -6f ? edge : Color.Lerp(fill, new Color(1f, 0.93f, 0.55f), Mathf.Clamp01((y - 40) / 60f) * 0.6f);
                c.a = Cover(d);
                return c;
            });
            Make("StarEmpty", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, false, (x, y) =>
            {
                float d = Star(x, y, 64, 62, 60, 25);
                var c = d > -6f ? new Color(0.08f, 0.1f, 0.14f, 1f) : new Color(1f, 1f, 1f, 0.16f);
                c.a *= Cover(d);
                return c;
            });
            Make("IconClock", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, false, (x, y) =>
            {
                float d = Mathf.Min(Ring(x, y, 64, 60, 46, 9),
                    Mathf.Min(Segment(x, y, 64, 60, 64, 88, 5), Segment(x, y, 64, 60, 84, 60, 5)));
                d = Mathf.Min(d, Segment(x, y, 54, 118, 74, 118, 6));
                return White(d);
            });
            Make("IconWind", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, false, (x, y) =>
            {
                float d = Segment(x, y, 14, 84, 92, 84, 6);
                d = Mathf.Min(d, Segment(x, y, 8, 60, 104, 60, 6));
                d = Mathf.Min(d, Segment(x, y, 22, 36, 86, 36, 6));
                d = Mathf.Min(d, Segment(x, y, 104, 60, 84, 80, 6));
                d = Mathf.Min(d, Segment(x, y, 104, 60, 84, 40, 6));
                return White(d);
            });
            // Yellow padlock with a dark outline (closed stages and levels).
            Make("IconLock", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, false, (x, y) =>
            {
                float shackle = Mathf.Max(Ring(x, y, 64, 74, 26, 12), 66f - y); // upper half of a ring
                float body = RoundRect(x, y, 64, 42, 40, 32, 10);
                float d = Mathf.Min(shackle, body);
                var c = d > -6f ? new Color(0.12f, 0.1f, 0.05f) : (body < 0f ? Color.Lerp(new Color(0.96f, 0.76f, 0.12f), new Color(1f, 0.92f, 0.45f), Mathf.Clamp01((y - 14) / 56f)) : new Color(0.78f, 0.8f, 0.84f));
                if (body < -6f && Segment(x, y, 64, 34, 64, 50, 5) < 0f) c = new Color(0.25f, 0.18f, 0.05f);
                c.a = Cover(d);
                return c;
            });
            Make("IconPause", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, false, (x, y) =>
                White(Mathf.Min(Segment(x, y, 44, 30, 44, 98, 13), Segment(x, y, 84, 30, 84, 98, 13))));
            Make("IconZoom", 128, 128, 128f, new Vector2(0.5f, 0.5f), Vector4.zero, false, (x, y) =>
            {
                float d = Mathf.Min(Ring(x, y, 54, 74, 34, 9), Segment(x, y, 78, 50, 112, 16, 9));
                d = Mathf.Min(d, Mathf.Min(Segment(x, y, 40, 74, 68, 74, 5), Segment(x, y, 54, 60, 54, 88, 5)));
                return White(d);
            });
            AssetDatabase.Refresh();
        }

        // ---- paper ----

        static Color Paper(int x, int y)
        {
            float u = x / 512f, v = y / 512f;
            float soft = Fbm(u, v, 3, 5, 11);                      // large soft light/shadow patches of crumpled paper
            float crease = 1f - Mathf.Abs(Fbm(u, v, 4, 3, 37) * 2f - 1f); // a few faint ridges
            float shade = 0.9f + soft * 0.1f + Mathf.Pow(crease, 10f) * 0.025f;
            var paper = new Color(0.94f, 0.93f, 0.89f) * shade;
            // Grid every 64 px, 2 px lines, slightly blue-grey like a school exercise book.
            bool line = x % 64 < 2 || y % 64 < 2;
            if (line) paper = Color.Lerp(paper, new Color(0.62f, 0.66f, 0.72f), 0.55f);
            paper.a = 1f;
            return paper;
        }

        /// <summary>Тайлящийся фрактальный шум: решётка с периодом period, octaves октав.</summary>
        static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(u * period, v * period, period, seed + o * 101) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static float Value(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        static void Make(string name, int w, int h, float ppu, Vector2 pivot, Vector4 border, bool fullRect, Func<int, int, Color> paint)
        {
            string path = $"{Folder}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, paint(x, y));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = border;
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            importer.SetTextureSettings(s);
            importer.SaveAndReimport();
        }

        // ---- signed distances in pixels (negative = inside) ----

        static Color White(float d) => new Color(1f, 1f, 1f, Cover(d));

        static float Cover(float d) => Mathf.Clamp01(0.5f - d);

        static float Circle(float x, float y, float cx, float cy, float r) => Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;

        static float Ring(float x, float y, float cx, float cy, float r, float width) => Mathf.Abs(Circle(x, y, cx, cy, r)) - width * 0.5f;

        static float Segment(float x, float y, float ax, float ay, float bx, float by, float halfWidth)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            var a = new Vector2(ax, ay);
            var b = new Vector2(bx, by);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t) - halfWidth;
        }

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r) => Segment(x, y, ax, ay, bx, by, r);

        static float RoundRect(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float dx = Mathf.Abs(x + 0.5f - cx) - (hx - r), dy = Mathf.Abs(y + 0.5f - cy) - (hy - r);
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy) =>
            Polygon(new Vector2(x + 0.5f, y + 0.5f), new[] { new Vector2(ax, ay), new Vector2(bx, by), new Vector2(cx, cy) });

        static float Star(float x, float y, float cx, float cy, float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            return Polygon(new Vector2(x + 0.5f, y + 0.5f), pts);
        }

        static float Polygon(Vector2 p, Vector2[] pts)
        {
            float dist = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                var a = pts[j];
                var b = pts[i];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                dist = Mathf.Min(dist, Vector2.Distance(p, a + ab * t));
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -dist : dist;
        }
    }
}
