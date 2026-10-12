using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using StickmanJumper.Presentation;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Анимации персонажа оригинала (заглушки): берёт расшифрованные .character (XML plist) из reference/decoded/characters,
    /// вырезает картинки частей тела из атласа оригинала и создаёт ассеты <see cref="CharacterClip"/>
    /// в Assets/_ThirdPartyPlaceholder/Character. Вызывается из <see cref="PlaceholderImporter.Run"/>; строки для таблицы учёта — <see cref="LedgerRows"/>.
    /// </summary>
    public static class CharacterImporter
    {
        public const string Folder = PlaceholderImporter.Folder + "/Character";
        public const string PartsFolder = Folder + "/Parts";
        const string Atlas = "gameEngineSpriteSheetDefault-hd";

        /// <summary>Какие анимации берём (файл оригинала → зачем). stuntmanWins01 не берём: его картинки купола нет в атласах.</summary>
        public static readonly (string File, string Usage)[] Clips =
        {
            ("stuntmanStand", "Стоит на крыше"),
            ("stuntmanWalk", "Пятится от края (ходьба)"),
            ("stuntmanRun", "Разбег"),
            ("stuntmanJump", "Толчок и вытягивание в нырок"),
            ("stuntmanParachute", "Висит под куполом"),
            ("stuntmanFastLanding", "Хватается за голову (время вышло)"),
            ("stuntmanWins", "Сел на флажок: радуется"),
            ("stuntmanLoose", "Сел мимо: грустит"),
        };

        /// <summary>…/reference/decoded/characters рядом с папкой src/.</summary>
        public static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "reference", "decoded", "characters"));

        static readonly List<(string Source, string File, string Usage)> Rows = new List<(string, string, string)>();
        public static IReadOnlyList<(string Source, string File, string Usage)> LedgerRows => Rows;

        public static CharacterClip LoadClip(string file) => AssetDatabase.LoadAssetAtPath<CharacterClip>($"{Folder}/{file}.asset");

        /// <summary>Импорт. atlasDir — распакованный оригинал (там атлас с частями тела). false — нет исходников.</summary>
        public static bool Run(string atlasDir)
        {
            Rows.Clear();
            string src = SourceDir;
            if (!Directory.Exists(src))
            {
                Debug.LogWarning($"[Character] {src} not found — the stickman stays the simple stick figure.");
                return false;
            }
            Directory.CreateDirectory(PartsFolder);

            var plists = new Dictionary<string, Dictionary<string, object>>();
            foreach (var (file, _) in Clips)
                plists[file] = (Dictionary<string, object>)ParsePlist(File.ReadAllText(Path.Combine(src, file + ".character")));

            // 1. Body-part pictures used by these animations, cut out of the original atlas (1 px = 1 unit, pivot bottom-left).
            var images = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var p in plists.Values) CollectImages(p["component"], images);
            images.Remove("point.png");
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            atlas.LoadImage(File.ReadAllBytes(Path.Combine(atlasDir, Atlas + ".png")));
            var frames = PlaceholderImporter.ParseFrames(File.ReadAllText(Path.Combine(atlasDir, Atlas + ".plist")));
            var written = new HashSet<string>();
            foreach (var image in images)
            {
                if (!frames.TryGetValue(image, out var r))
                {
                    Debug.LogWarning($"[Character] {image} is not in {Atlas} — that part stays invisible");
                    continue;
                }
                var crop = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
                crop.SetPixels(atlas.GetPixels(r.x, atlas.height - r.y - r.height, r.width, r.height)); // cocos frames are top-left based
                crop.Apply();
                string path = $"{PartsFolder}/{image}";
                File.WriteAllBytes(path, crop.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(crop);
                written.Add(path);
                Rows.Add(($"{Atlas}.png → {image}", path, "Часть тела стикмена / купола"));
            }
            UnityEngine.Object.DestroyImmediate(atlas);
            AssetDatabase.Refresh();
            foreach (var path in written) ConfigurePart(path);

            // 2. Animation assets (updated in place so references keep working).
            var keep = new HashSet<string>(written);
            foreach (var (file, usage) in Clips)
            {
                string path = $"{Folder}/{file}.asset";
                var clip = AssetDatabase.LoadAssetAtPath<CharacterClip>(path);
                bool create = clip == null;
                if (create) clip = ScriptableObject.CreateInstance<CharacterClip>();
                Fill(clip, plists[file], name => AssetDatabase.LoadAssetAtPath<Sprite>($"{PartsFolder}/{name}"));
                if (create) AssetDatabase.CreateAsset(clip, path);
                else EditorUtility.SetDirty(clip);
                keep.Add(path);
                Rows.Add(($"decoded/characters/{file}.character", path, usage));
            }
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path) && !keep.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Character] imported {Clips.Length} animations and {written.Count} body-part pictures into {Folder}");
            return true;
        }

        static void ConfigurePart(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f; // the skeleton works in the original's pixels
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = Vector2.zero; // anchors in the files are measured from the bottom-left corner
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ .character → CharacterClip

        /// <summary>Переносит кости и ключи из plist в ассет и считает землю и рост.</summary>
        public static void Fill(CharacterClip clip, Dictionary<string, object> plist, Func<string, Sprite> sprite)
        {
            clip.FrameTime = Num(plist, "FrameTime") / 1000f;
            var bones = new List<CharacterClip.Bone>();
            AddBone(plist["component"], -1, bones, sprite);
            clip.Bones = bones.ToArray();
            clip.LastFrame = bones.SelectMany(b => b.Keys).Select(k => k.Frame).DefaultIfEmpty(0).Max();
            MeasureGround(clip);
        }

        static void AddBone(object item, int parent, List<CharacterClip.Bone> bones, Func<string, Sprite> sprite)
        {
            var dict = (Dictionary<string, object>)item;
            var comp = dict.TryGetValue("component", out var c) ? (Dictionary<string, object>)c : dict;
            var bone = new CharacterClip.Bone { Name = Str(comp, "name"), Parent = parent, Z = Num(comp, "zOrder") };
            var keys = new List<CharacterClip.Key>();
            if (comp.TryGetValue("keyframes", out var kf) && kf is List<object> list)
                foreach (Dictionary<string, object> k in list)
                {
                    string image = Path.GetFileName(Str(k, "relativeImagePath"));
                    keys.Add(new CharacterClip.Key
                    {
                        Frame = Mathf.RoundToInt(Num(k, "index")),
                        Position = new Vector2(Num(k, "position.x"), Num(k, "position.y")),
                        Rotation = Num(k, "rotation"),
                        Scale = new Vector2(Num(k, "scale.x"), Num(k, "scale.y")),
                        Anchor = new Vector2(Num(k, "anchorPoint.x"), Num(k, "anchorPoint.y")),
                        Sprite = string.IsNullOrEmpty(image) || image == "." || image == "point.png" ? null : sprite(image),
                    });
                }
            bone.Keys = keys.OrderBy(k => k.Frame).ToArray();
            int index = bones.Count;
            bones.Add(bone);
            if (comp.TryGetValue("children", out var ch) && ch is List<object> children)
                foreach (var child in children) AddBone(child, index, bones, sprite);
        }

        /// <summary>Земля — самая низкая точка ног за всю анимацию; рост — от земли до макушки в первом кадре.</summary>
        static void MeasureGround(CharacterClip clip)
        {
            float ground = float.MaxValue, top = float.MinValue;
            for (int f = 0; f <= clip.LastFrame; f++)
            {
                var pose = clip.PoseMatrices(f, out var visible);
                for (int i = 0; i < clip.Bones.Length; i++)
                {
                    var b = clip.Bones[i];
                    if (!visible[i] || CharacterClip.IsCanopyBone(b.Name) || !CharacterClip.Sample(b.Keys, f, out var k) || k.Sprite == null) continue;
                    var corners = CharacterClip.ImageCorners(pose[i], k);
                    if (b.Name.Contains("Leg")) ground = Mathf.Min(ground, corners.Min(p => p.y));
                    if (f == 0) top = Mathf.Max(top, corners.Max(p => p.y));
                }
            }
            if (ground == float.MaxValue) ground = 0f;
            clip.GroundY = ground;
            clip.Height = top == float.MinValue ? 0f : top - ground;
        }

        static void CollectImages(object item, ISet<string> images)
        {
            var dict = (Dictionary<string, object>)item;
            var comp = dict.TryGetValue("component", out var c) ? (Dictionary<string, object>)c : dict;
            if (comp.TryGetValue("keyframes", out var kf) && kf is List<object> list)
                foreach (Dictionary<string, object> k in list)
                {
                    string image = Path.GetFileName(Str(k, "relativeImagePath"));
                    if (!string.IsNullOrEmpty(image) && image != ".") images.Add(image);
                }
            if (comp.TryGetValue("children", out var ch) && ch is List<object> children)
                foreach (var child in children) CollectImages(child, images);
        }

        static string Str(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) ? v as string ?? string.Empty : string.Empty;

        static float Num(Dictionary<string, object> d, string key) =>
            float.TryParse(Str(d, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;

        // ------------------------------------------------------------------ plist

        /// <summary>XML plist → словари, списки и строки (числа в этих файлах — строки).</summary>
        public static object ParsePlist(string xml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
                doc.Load(reader);
            var root = doc.SelectSingleNode("/plist");
            var first = root?.ChildNodes.Cast<XmlNode>().FirstOrDefault(n => n.NodeType == XmlNodeType.Element);
            return first == null ? null : Value(first);
        }

        static object Value(XmlNode node)
        {
            switch (node.Name)
            {
                case "dict":
                {
                    var d = new Dictionary<string, object>();
                    var kids = node.ChildNodes.Cast<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element).ToList();
                    for (int i = 0; i + 1 < kids.Count; i += 2)
                        if (kids[i].Name == "key") d[kids[i].InnerText] = Value(kids[i + 1]);
                    return d;
                }
                case "array":
                    return node.ChildNodes.Cast<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element).Select(Value).ToList();
                case "true":
                    return "1";
                case "false":
                    return "0";
                default:
                    return node.InnerText;
            }
        }
    }
}
