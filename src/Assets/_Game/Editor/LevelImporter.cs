using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using StickmanJumper.Config;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Уровни оригинала (заглушки): расшифрованные .landscape из reference/decoded/levels → <see cref="LevelScene"/> +
    /// <see cref="LevelConfig"/> + этапы <see cref="StageConfig"/> в Assets/_ThirdPartyPlaceholder/Levels, картинки — в LevelArt.
    /// 1 м = 44 единицы редактора оригинала (тот же масштаб, что у персонажа: рост 83 px = 1.9 м, этаж 100 единиц = 2.3 м).
    /// Уровни, где в оригинале прыгают влево, отзеркаливаются. Вызывается из <see cref="PlaceholderImporter.Run"/>.
    /// </summary>
    public static class LevelImporter
    {
        public const string Folder = PlaceholderImporter.Folder + "/Levels";
        public const string ArtFolder = PlaceholderImporter.Folder + "/LevelArt";
        public const float UnitsPerMeter = 44f;
        const string Atlas = "gameEngineSpriteSheetDefault-hd";

        public readonly struct Pack
        {
            public readonly string Prefix, TitleRu, SubRu, TitleEn, SubEn;
            public readonly int Number, StarsToUnlock;
            /// <summary>Уровни по порядку прохождения (как в оригинале, не по номерам файлов).</summary>
            public readonly string[] Levels;

            public Pack(string prefix, int number, string titleRu, string subRu, string titleEn, string subEn, int stars, params string[] levels)
            {
                Prefix = prefix;
                Number = number;
                TitleRu = titleRu;
                SubRu = subRu;
                TitleEn = titleEn;
                SubEn = subEn;
                StarsToUnlock = stars;
                Levels = levels;
            }
        }

        static string[] Seq(string prefix, string format = "{0}{1}") =>
            Enumerable.Range(1, 10).Select(i => string.Format(CultureInfo.InvariantCulture, format, prefix, i)).ToArray();

        /// <summary>
        /// Этапы оригинала в его порядке (reference/decoded/level_packs.json; названия — с его логотипов menuPackLogoBig1..9).
        /// 8-й — ночной этап Android-версии (землетрясение), 9-й — ночной этап iOS-версии (в оригинале — один из двух).
        /// Сколько звёзд нужно для открытия, неизвестно — пока по 15 за каждый прошлый этап (из 30 возможных).
        /// </summary>
        public static readonly Pack[] Packs =
        {
            new Pack("easy", 1, "новичок", "первые прыжки", "rookie", "your first adrenaline jumps", 0, Seq("easy")),
            new Pack("advanced", 2, "опытный", "наверху ветрено", "advanced", "it's windy up there", 15,
                "advanced1", "advanced2", "advanced3", "advanced4", "advanced5", "advanced6", "advanced8", "advanced7", "advanced9", "advanced10"),
            new Pack("expert", 3, "эксперт", "прыжки по платформам", "expert", "multiplatform jumps", 30, Seq("expert")),
            new Pack("construction", 4, "стройка", "стекло и кирпичи", "construction site", "glass and bricks", 45, Seq("construction")),
            new Pack("f", 5, "передышка", "отдохни после трудов", "humble", "chill out after a hard work", 60, Seq("f")),
            new Pack("x", 6, "вертолёты!", "держи ухо востро", "heli-alert", "keep your powder dry", 75, Seq("x", "{0}{1:00}")),
            new Pack("d", 7, "теснота", "будет тесно", "closetown", "it will be a tight squeeze", 90,
                "d3", "d1", "d6", "d5", "d4", "d7", "d8", "d10", "d9", "d2"),
            new Pack("n", 8, "землетрясение", "беда в городе", "bigQuake", "a disaster hit the town", 105,
                "n9", "n2", "n3", "n1", "n4", "n5", "n7", "n8", "n6", "n10"),
            new Pack("night", 9, "ночь", "прыжки в темноте", "night only", "jump in dark night", 120,
                "night4", "night1", "night8", "night6", "night7", "night5", "night9", "night10", "night3", "night2"),
        };

        /// <summary>
        /// На двух дирижаблях оригинала — реклама его студии и её игры. Бренд не используем даже в заглушках:
        /// надпись закрашиваем цветом корпуса (прямоугольник в пикселях картинки, от левого верхнего угла).
        /// </summary>
        static readonly Dictionary<string, RectInt> BrandAreas = new Dictionary<string, RectInt>
        {
            { "zeppelinDjinnWorks.png", new RectInt(52, 12, 380, 92) },
            { "zeppelinWingsuitStickman.png", new RectInt(100, 34, 392, 72) },
        };

        public static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "reference", "decoded", "levels"));

        static readonly List<(string Source, string File, string Usage)> Rows = new List<(string, string, string)>();
        public static IReadOnlyList<(string Source, string File, string Usage)> LedgerRows => Rows;

        public static StageConfig[] LoadStages() =>
            Packs.Select(p => AssetDatabase.LoadAssetAtPath<StageConfig>(StagePath(p))).Where(s => s != null && s.Levels.Length > 0).ToArray();

        static string StagePath(Pack p) => $"{Folder}/Stage{p.Number}_{p.Prefix}.asset";

        /// <summary>Файлы уровней этапа в порядке прохождения (только те, что есть в папке).</summary>
        public static List<string> LevelFiles(string dir, Pack pack) =>
            pack.Levels.Where(n => File.Exists(Path.Combine(dir, n + ".landscape"))).ToList();

        public static bool Run(string assetsDir)
        {
            Rows.Clear();
            string src = SourceDir;
            if (!Directory.Exists(src))
            {
                Debug.LogWarning($"[Levels] {src} not found — the game keeps its own levels.");
                return false;
            }
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(ArtFolder);

            var shapes = ParseShapes(File.ReadAllText(Path.Combine(assetsDir, "physicShapes.physicShapes")));
            var frames = PlaceholderImporter.ParseFrames(File.ReadAllText(Path.Combine(assetsDir, Atlas + ".plist")));
            var levels = new List<(Pack pack, string name, Dictionary<string, object> plist)>();
            foreach (var pack in Packs)
                foreach (var name in LevelFiles(src, pack))
                    levels.Add((pack, name, (Dictionary<string, object>)CharacterImporter.ParsePlist(File.ReadAllText(Path.Combine(src, name + ".landscape")))));

            // 1. Pictures used by the levels, cut out of the original atlas (44 px per metre, pivot in the centre).
            var images = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var l in levels)
                foreach (var n in Nodes(l.plist))
                    if (IsPicture(n)) images.Add(Path.GetFileName(Str(n, "image")));
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            atlas.LoadImage(File.ReadAllBytes(Path.Combine(assetsDir, Atlas + ".png")));
            var sizes = new Dictionary<string, Vector2Int>();
            var written = new HashSet<string>();
            foreach (var image in images)
            {
                Texture2D tex = null;
                if (frames.TryGetValue(image, out var r))
                {
                    tex = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
                    tex.SetPixels(atlas.GetPixels(r.x, atlas.height - r.y - r.height, r.width, r.height)); // cocos frames are top-left based
                }
                else
                {
                    string file = Path.Combine(assetsDir, Path.GetFileNameWithoutExtension(image) + "-hd.png");
                    if (!File.Exists(file)) file = Path.Combine(assetsDir, image);
                    if (!File.Exists(file))
                    {
                        Debug.LogWarning($"[Levels] picture {image} not found — skipped");
                        continue;
                    }
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(File.ReadAllBytes(file));
                }
                if (BrandAreas.TryGetValue(image, out var brand)) PaintOver(tex, brand);
                tex.Apply();
                string path = $"{ArtFolder}/{image}";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                sizes[image] = new Vector2Int(tex.width, tex.height);
                UnityEngine.Object.DestroyImmediate(tex);
                written.Add(path);
                Rows.Add(($"{Atlas}.png → {image}", path, "Картинка уровней оригинала"));
            }
            UnityEngine.Object.DestroyImmediate(atlas);
            AssetDatabase.Refresh();
            foreach (var path in written) ConfigurePicture(path);

            // 2. Level scenes, level configs and stages (updated in place so saves and references keep working).
            var keep = new HashSet<string>(written);
            foreach (var pack in Packs)
            {
                var stageLevels = new List<LevelConfig>();
                int number = 0;
                foreach (var l in levels.Where(l => l.pack.Prefix == pack.Prefix))
                {
                    number++;
                    var scene = Load<LevelScene>($"{Folder}/{l.name}_scene.asset", keep);
                    FillScene(scene, l.plist, shapes, sizes, name => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{name}"));
                    EditorUtility.SetDirty(scene);
                    var level = Load<LevelConfig>($"{Folder}/{l.name}.asset", keep);
                    var keys = LevelKeys(l.plist);
                    level.Id = l.name;
                    level.Title = $"Прыжок {number}";
                    level.Scene = scene;
                    level.TimeLimit = Num(keys, "ccMaxTime", 30f);
                    level.Wind = (scene.Mirrored ? -scene.WindRaw : scene.WindRaw) / UnitsPerMeter;
                    level.WindMax = 3f; // strongest wind in the original's files: 130 units/s ≈ 3 m/s
                    level.MaxFallSpeed = Mathf.Abs(scene.MaxFlySpeedYRaw) / UnitsPerMeter;
                    level.ThreeStarTime = level.TimeLimit * 0.6f;
                    EditorUtility.SetDirty(level);
                    stageLevels.Add(level);
                    Rows.Add(($"decoded/levels/{l.name}.landscape", $"{Folder}/{l.name}.asset", $"Уровень оригинала: этап {pack.Number}, прыжок {number}"));
                }
                var stage = Load<StageConfig>(StagePath(pack), keep);
                stage.Number = pack.Number;
                stage.Title = pack.TitleRu;
                stage.Subtitle = pack.SubRu;
                stage.TitleEn = pack.TitleEn;
                stage.SubtitleEn = pack.SubEn;
                stage.StarsToUnlock = pack.StarsToUnlock;
                stage.Levels = stageLevels.ToArray();
                EditorUtility.SetDirty(stage);
            }
            foreach (var folder in new[] { Folder, ArtFolder })
                foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!AssetDatabase.IsValidFolder(path) && !keep.Contains(path)) AssetDatabase.DeleteAsset(path);
                }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Levels] imported {levels.Count} levels in {Packs.Length} stages and {written.Count} pictures");
            return true;
        }

        static T Load<T>(string path, HashSet<string> keep) where T : ScriptableObject
        {
            keep.Add(path);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ------------------------------------------------------------------ .landscape → LevelScene

        public static void FillScene(LevelScene scene, Dictionary<string, object> plist, Dictionary<string, List<Vector2[]>> shapes,
            Dictionary<string, Vector2Int> sizes, Func<string, Sprite> sprite)
        {
            var nodes = Nodes(plist).ToList();
            var keys = LevelKeys(plist);
            float width = Num(plist, "levelSize.width"), height = Num(plist, "levelSize.height");
            var runway = nodes.FirstOrDefault(n => Str(n, "className") == "Runway");
            bool mirror = runway != null && NodeKeys(runway).TryGetValue("direction", out var dir) && dir == "1"; // 1 = jump to the left
            scene.Mirrored = mirror;
            scene.Size = new Vector2(width, height) / UnitsPerMeter;
            scene.WindRaw = Num(keys, "windSpeed");
            scene.MaxFlySpeedYRaw = Num(keys, "maxFlySpeedY");
            scene.Night = Str(keys, "nightMode") == "1";
            scene.Earthquakes = Str(keys, "earthquakes") == "1";

            Vector2 Point(float x, float y) => new Vector2((mirror ? width - x : x) / UnitsPerMeter, y / UnitsPerMeter);
            Rect Box(Dictionary<string, object> n)
            {
                Vector2 c = Point(Num(n, "position.x"), Num(n, "position.y"));
                Vector2 size = new Vector2(Num(n, "size.x"), Num(n, "size.y")) / UnitsPerMeter;
                return new Rect(c - size * 0.5f, size);
            }

            var pictures = new List<LevelScene.Picture>();
            var solids = new List<Rect>();
            var inter = new List<Rect>();
            var interLeft = new List<bool>();
            var lights = new List<LevelScene.LightSource>();
            foreach (var n in nodes)
            {
                string type = Str(n, "Type"), cls = Str(n, "className");
                if (IsPicture(n))
                {
                    string image = Path.GetFileName(Str(n, "image"));
                    if (!sizes.TryGetValue(image, out var px)) continue;
                    float sx = Num(n, "scale.x", 1f), sy = Num(n, "scale.y", 1f), rot = Num(n, "rotation");
                    float x = Num(n, "position.x"), y = Num(n, "position.y");
                    pictures.Add(new LevelScene.Picture
                    {
                        Sprite = sprite(image),
                        Position = Point(x, y),
                        Scale = new Vector2(mirror ? -sx : sx, sy),
                        Rotation = mirror ? -rot : rot,
                        Layer = Mathf.RoundToInt(Num(n, "zOrder")),
                        Alpha = Mathf.Clamp01(Num(n, "opacity", 255f) / 255f),
                        Kind = cls,
                        AlwaysLit = NodeKeys(n).TryGetValue("objectLighting", out var lit) && lit == "1",
                    });
                    // Breakable glass and birds are not walls (in the original you fly through them).
                    if (Str(n, "enablePhysics") != "1" || cls == "Glass" || cls == "Bird") continue;
                    List<Vector2[]> polys = shapes.TryGetValue(image, out var known)
                        ? known
                        : new List<Vector2[]> { new[] { Vector2.zero, new Vector2(px.x, 0f), new Vector2(px.x, px.y), new Vector2(0f, px.y) } };
                    float a = rot * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    foreach (var poly in polys)
                    {
                        if (poly.Length == 0) continue;
                        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                        foreach (var p in poly) // image pixels, y up from the bottom-left corner
                        {
                            float lx = (p.x - px.x * 0.5f) * sx, ly = (p.y - px.y * 0.5f) * sy;
                            var w = Point(x + lx * c - ly * s, y + lx * s + ly * c);
                            x0 = Mathf.Min(x0, w.x); x1 = Mathf.Max(x1, w.x);
                            y0 = Mathf.Min(y0, w.y); y1 = Mathf.Max(y1, w.y);
                        }
                        if (x1 - x0 > 0.05f && y1 - y0 > 0.05f) solids.Add(Rect.MinMaxRect(x0, y0, x1, y1));
                    }
                }
                else if (type == "LandscapeNodeObject")
                {
                    switch (cls)
                    {
                        case "Runway": scene.Runway = Box(n); break;
                        case "Platform": scene.Platform = Box(n); break;
                        case "InterPlatform":
                            inter.Add(Box(n));
                            // direction 1 = run to the left in the file; mirrored levels swap left and right
                            interLeft.Add((NodeKeys(n).TryGetValue("direction", out var d) && d == "1") != mirror);
                            break;
                        case "Light":
                        {
                            var lightKeys = NodeKeys(n);
                            float Radius(string k, float fallback) =>
                                lightKeys.TryGetValue(k, out var r) && float.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
                            lights.Add(new LevelScene.LightSource
                            {
                                Position = Point(Num(n, "position.x"), Num(n, "position.y")),
                                RadiusFull = Radius("radiusHigh", 300f) / UnitsPerMeter,
                                RadiusFade = Radius("radiusLow", 500f) / UnitsPerMeter,
                            });
                            break;
                        }
                        case "PhysicDummy":
                            if (NodeKeys(n).TryGetValue("sensor", out var sensor) && sensor == "1") break; // a trigger, not a wall
                            solids.Add(Box(n));
                            break;
                    }
                }
                else if (type == "LandscapeNodeCamera")
                {
                    var p = Point(Num(n, "position.x"), Num(n, "position.y"));
                    switch (Str(n, "Name"))
                    {
                        case "cameraStart": scene.CameraStart = p; break;
                        case "cameraEnd": scene.CameraEnd = p; break;
                        case "cameraZoom": scene.CameraZoom = p; break;
                    }
                }
            }
            scene.Pictures = pictures.ToArray();
            scene.Solids = solids.ToArray();
            scene.InterPlatforms = inter.ToArray();
            scene.InterLeft = interLeft.ToArray();
            scene.Lights = lights.ToArray();
        }

        /// <summary>Картинка уровня, если не скрыта (LightSprite — обычная картинка, которая ещё и светится ночью).</summary>
        static bool IsPicture(Dictionary<string, object> n) =>
            Str(n, "Type") == "LandscapeNodeImage" && Str(n, "hidden") != "1" && !string.IsNullOrEmpty(Str(n, "image"));

        static IEnumerable<Dictionary<string, object>> Nodes(Dictionary<string, object> plist) =>
            plist.TryGetValue("nodes", out var v) && v is List<object> list ? list.OfType<Dictionary<string, object>>() : Enumerable.Empty<Dictionary<string, object>>();

        /// <summary>Keys в файлах — список словариков по одному ключу; склеиваем в один.</summary>
        static Dictionary<string, object> KeyList(Dictionary<string, object> d)
        {
            var result = new Dictionary<string, object>();
            if (d.TryGetValue("Keys", out var v) && v is List<object> list)
                foreach (var item in list.OfType<Dictionary<string, object>>())
                    foreach (var kv in item) result[kv.Key] = kv.Value;
            return result;
        }

        static Dictionary<string, object> LevelKeys(Dictionary<string, object> plist) => KeyList(plist);

        static Dictionary<string, string> NodeKeys(Dictionary<string, object> node) => KeyList(node).ToDictionary(kv => kv.Key, kv => kv.Value as string ?? string.Empty);

        static string Str(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) ? v as string ?? string.Empty : string.Empty;

        static float Num(Dictionary<string, object> d, string key, float fallback = 0f) =>
            float.TryParse(Str(d, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

        // ------------------------------------------------------------------ physics shapes & pictures

        /// <summary>physicShapes.physicShapes: картинка → многоугольники (в пикселях, ось Y вверх от левого нижнего угла).</summary>
        public static Dictionary<string, List<Vector2[]>> ParseShapes(string xml)
        {
            var result = new Dictionary<string, List<Vector2[]>>();
            var root = (Dictionary<string, object>)CharacterImporter.ParsePlist(xml);
            if (!(root.TryGetValue("images", out var v) && v is List<object> images)) return result;
            foreach (var item in images.OfType<Dictionary<string, object>>())
            {
                var polys = new List<Vector2[]>();
                if (item.TryGetValue("shapes", out var sv) && sv is List<object> shapeList)
                    foreach (var shape in shapeList.OfType<Dictionary<string, object>>())
                        if (shape.TryGetValue("points", out var pv) && pv is List<object> points)
                            polys.Add(points.OfType<string>().Select(ParsePoint).ToArray());
                result[Str(item, "image")] = polys;
            }
            return result;
        }

        static Vector2 ParsePoint(string s)
        {
            var parts = s.Trim('{', '}', ' ').Split(',');
            float x = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
            float y = parts.Length > 1 ? float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture) : 0f;
            return new Vector2(x, y);
        }

        /// <summary>Закрашивает прямоугольник цветом картинки той же строки (медиана непрозрачных пикселей вне прямоугольника).</summary>
        static void PaintOver(Texture2D tex, RectInt areaFromTop)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            for (int yTop = Mathf.Max(0, areaFromTop.yMin); yTop < Mathf.Min(h, areaFromTop.yMax); yTop++)
            {
                int y = h - 1 - yTop;
                var row = new List<Color32>();
                for (int x = 0; x < w; x++)
                    if ((x < areaFromTop.xMin || x >= areaFromTop.xMax) && px[y * w + x].a > 230) row.Add(px[y * w + x]);
                if (row.Count == 0) continue;
                var fill = row.OrderBy(c => c.r + c.g + c.b).ElementAt(row.Count / 2);
                for (int x = Mathf.Max(0, areaFromTop.xMin); x < Mathf.Min(w, areaFromTop.xMax); x++)
                    if (px[y * w + x].a > 0) px[y * w + x] = new Color32(fill.r, fill.g, fill.b, px[y * w + x].a);
            }
            tex.SetPixels32(px);
        }

        static void ConfigurePicture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = UnitsPerMeter; // node scale 2 → 22 hd px per metre, like the other placeholders
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center; // every node in the original files is anchored in the middle
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
