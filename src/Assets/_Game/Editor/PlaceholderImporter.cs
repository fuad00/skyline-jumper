using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Берёт чужие ассеты из карантина reference/ (распакованный .apk оригинала) и кладёт ровно нужные в
    /// Assets/_ThirdPartyPlaceholder/: вырезает спрайты из атласа, копирует звуки, настраивает импорт.
    /// Список ниже — единственный источник правды: по нему же переписывается таблица в reference/PLACEHOLDER_ASSETS.md.
    /// Нет папки reference/ (свежий клон) — ничего не делает, игра работает на своих простых заменах.
    /// </summary>
    public static class PlaceholderImporter
    {
        public const string Folder = "Assets/_ThirdPartyPlaceholder";
        public const string SpritesFolder = Folder + "/Sprites";
        public const string AudioFolder = Folder + "/Audio";
        public const float PixelsPerUnit = 50f; // 1 floor of the original facades = 50 px = 1 m

        const string MainAtlas = "gameEngineSpriteSheetDefault-hd";
        const string SkylineAtlas = "backgroundSpriteSheetDefault-hd";
        const string LedgerBegin = "<!-- AUTO:BEGIN (PlaceholderImporter) -->";
        const string LedgerEnd = "<!-- AUTO:END -->";

        public readonly struct Item
        {
            public readonly string Atlas;   // null = standalone file
            public readonly string Source;  // frame name in the atlas or file name
            public readonly string Name;    // file name in Unity (no extension)
            public readonly string Usage;
            public readonly Vector2 Pivot;
            public readonly bool Tiled;

            public Item(string atlas, string source, string name, string usage, Vector2 pivot, bool tiled = false)
            {
                Atlas = atlas;
                Source = source;
                Name = name;
                Usage = usage;
                Pivot = pivot;
                Tiled = tiled;
            }
        }

        static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        public static readonly Item[] Sprites =
        {
            new Item(MainAtlas, "head.png", "head", "Голова стикмена", Center),
            new Item(MainAtlas, "para01.png", "para01", "Кадр раскрытия купола 1", Bottom),
            new Item(MainAtlas, "para02.png", "para02", "Кадр раскрытия купола 2", Bottom),
            new Item(MainAtlas, "para03.png", "para03", "Кадр раскрытия купола 3", Bottom),
            new Item(MainAtlas, "para04.png", "para04", "Кадр раскрытия купола 4", Bottom),
            new Item(MainAtlas, "para05.png", "para05", "Кадр раскрытия купола 5", Bottom),
            new Item(MainAtlas, "para06.png", "para06", "Кадр раскрытия купола 6", Bottom),
            new Item(MainAtlas, "para07.png", "para07", "Кадр раскрытия купола 7", Bottom),
            new Item(MainAtlas, "para08.png", "para08", "Кадр раскрытия купола 8", Bottom),
            new Item(MainAtlas, "paraHolder.png", "paraHolder", "Раскрытый купол со стропами", Bottom),
            new Item(null, "flag-hd.png", "flag", "Флажок на крыше цели", Bottom),

            new Item(MainAtlas, "building015Floors.png", "office_floors", "Стиль Office: этажи", Bottom, true),
            new Item(MainAtlas, "building01FirstFloor.png", "office_ground", "Стиль Office: первый этаж", Bottom),
            new Item(MainAtlas, "building01Boarder02.png", "office_roof", "Стиль Office: бортик крыши", Bottom),
            new Item(MainAtlas, "building095Floors.png", "brick_floors", "Стиль Brick: этажи", Bottom, true),
            new Item(MainAtlas, "building09FirstFloot.png", "brick_ground", "Стиль Brick: первый этаж", Bottom),
            new Item(MainAtlas, "building09Boarder01.png", "brick_roof", "Стиль Brick: бортик крыши", Bottom),
            new Item(MainAtlas, "building025Floors01.png", "glass_floors", "Стиль Glass: этажи", Bottom, true),
            new Item(MainAtlas, "building02FirstFloor.png", "glass_ground", "Стиль Glass: первый этаж", Bottom),
            new Item(MainAtlas, "building02Boarder02.png", "glass_roof", "Стиль Glass: бортик крыши", Bottom),
            new Item(MainAtlas, "building035Floors.png", "dark_floors", "Стиль Dark: этажи", Bottom, true),
            new Item(MainAtlas, "building03FirstFloor.png", "dark_ground", "Стиль Dark: первый этаж", Bottom),
            new Item(MainAtlas, "building03Boarder02.png", "dark_roof", "Стиль Dark: бортик крыши", Bottom),
            new Item(MainAtlas, "building115Floors.png", "beige_floors", "Стиль Beige: этажи", Bottom, true),
            new Item(MainAtlas, "building11FirstFloor.png", "beige_ground", "Стиль Beige: первый этаж", Bottom),
            new Item(MainAtlas, "building11Boarder01.png", "beige_roof", "Стиль Beige: бортик крыши", Bottom),

            new Item(MainAtlas, "tree01.png", "tree01", "Дерево на улице", Bottom),
            new Item(MainAtlas, "tree02.png", "tree02", "Дерево на улице", Bottom),
            new Item(MainAtlas, "tree03.png", "tree03", "Дерево на улице", Bottom),
            new Item(MainAtlas, "tree04.png", "tree04", "Дерево на улице", Bottom),
            new Item(MainAtlas, "bush01.png", "bush01", "Куст на улице", Bottom),
            new Item(MainAtlas, "bush02.png", "bush02", "Куст на улице", Bottom),
            new Item(MainAtlas, "bush03.png", "bush03", "Куст на улице", Bottom),
            new Item(MainAtlas, "bush04.png", "bush04", "Куст на улице", Bottom),
            new Item(MainAtlas, "streetlamp01.png", "streetlamp", "Фонарь на улице", Bottom),
            new Item(MainAtlas, "hydrant.png", "hydrant", "Гидрант на улице", Bottom),
            new Item(MainAtlas, "cloud1.png", "cloud1", "Облако (фон)", Center),
            new Item(MainAtlas, "cloud2.png", "cloud2", "Облако (фон)", Center),
            new Item(MainAtlas, "cloud3.png", "cloud3", "Облако (фон)", Center),
            new Item(SkylineAtlas, "background0.png", "skyline", "Силуэт города (дальний фон)", Bottom, true),
        };

        public static readonly (string Source, string Usage)[] Sounds =
        {
            ("footsteps.ogg", "Шаги (разбег)"),
            ("jump.ogg", "Прыжок"),
            ("air1.ogg", "Свист воздуха в полёте"),
            ("windhowl.ogg", "Вой ветра (фон)"),
            ("parachuteOpen.ogg", "Хлопок купола"),
            ("parachuteLanding1.ogg", "Посадка"),
            ("parachuteLanding2.ogg", "Посадка"),
            ("yell0.ogg", "Крик"), ("yell1.ogg", "Крик"), ("yell2.ogg", "Крик"), ("yell3.ogg", "Крик"), ("yell4.ogg", "Крик"),
            ("yell5.ogg", "Крик"), ("yell6.ogg", "Крик"), ("yell7.ogg", "Крик"), ("yell8.ogg", "Крик"), ("yell9.ogg", "Крик"),
            ("yell10.ogg", "Крик"), ("yell11.ogg", "Крик"), ("yell12.ogg", "Крик"),
            ("bones1.ogg", "Хруст при ударе"), ("bones2.ogg", "Хруст при ударе"), ("bones3.ogg", "Хруст при ударе"),
            ("bones4.ogg", "Хруст при ударе"), ("bones5.ogg", "Хруст при ударе"), ("bones6.ogg", "Хруст при ударе"),
            ("smash1.ogg", "Удар (разбился)"), ("smash2.ogg", "Удар (разбился)"), ("smash3.ogg", "Удар (разбился)"),
            ("success.ogg", "Точная посадка"),
            ("click.ogg", "Кнопки"),
            ("soundtrack.ogg", "Музыка"),
        };

        /// <summary>…/reference/extracted/android/assets рядом с папкой src/.</summary>
        public static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "reference", "extracted", "android", "assets"));

        static string LedgerPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "reference", "PLACEHOLDER_ASSETS.md"));

        [MenuItem("Stickman Jumper/Import Placeholders (from reference/)")]
        public static bool Run()
        {
            string src = SourceDir;
            if (!Directory.Exists(src))
            {
                Debug.LogWarning($"[Placeholders] {src} not found — skipping. The game falls back to its own simple art.");
                return false;
            }
            Directory.CreateDirectory(SpritesFolder);
            Directory.CreateDirectory(AudioFolder);

            var atlases = new Dictionary<string, (Texture2D tex, Dictionary<string, RectInt> frames)>();
            foreach (var item in Sprites)
            {
                string outPath = $"{SpritesFolder}/{item.Name}.png";
                if (item.Atlas == null)
                {
                    File.Copy(Path.Combine(src, item.Source), outPath, true);
                    continue;
                }
                if (!atlases.TryGetValue(item.Atlas, out var atlas))
                {
                    atlas = LoadAtlas(src, item.Atlas);
                    atlases[item.Atlas] = atlas;
                }
                if (!atlas.frames.TryGetValue(item.Source, out var r))
                {
                    Debug.LogError($"[Placeholders] frame {item.Source} not found in {item.Atlas}");
                    continue;
                }
                // cocos2d frames are top-left based; Texture2D rows start at the bottom.
                var pixels = atlas.tex.GetPixels(r.x, atlas.tex.height - r.y - r.height, r.width, r.height);
                var crop = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
                crop.SetPixels(pixels);
                crop.Apply();
                File.WriteAllBytes(outPath, crop.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(crop);
            }
            foreach (var a in atlases.Values) UnityEngine.Object.DestroyImmediate(a.tex);

            foreach (var (source, _) in Sounds)
                File.Copy(Path.Combine(src, source), $"{AudioFolder}/{source}", true);

            AssetDatabase.Refresh();
            RemoveStale();
            foreach (var item in Sprites) ConfigureSprite($"{SpritesFolder}/{item.Name}.png", item);
            foreach (var (source, _) in Sounds) ConfigureAudio($"{AudioFolder}/{source}", source == "soundtrack.ogg" || source == "windhowl.ogg");
            AssetDatabase.SaveAssets();
            WriteLedger();
            Debug.Log($"[Placeholders] imported {Sprites.Length} sprites and {Sounds.Length} sounds into {Folder}");
            return true;
        }

        /// <summary>Убирает из карантинной папки всё, чего больше нет в списке (чтобы таблица учёта не врала).</summary>
        static void RemoveStale()
        {
            var keep = new HashSet<string>(Sprites.Select(i => $"{SpritesFolder}/{i.Name}.png"));
            keep.UnionWith(Sounds.Select(s => $"{AudioFolder}/{s.Source}"));
            foreach (var guid in AssetDatabase.FindAssets("", new[] { SpritesFolder, AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path) && !keep.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        public static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/{name}.png");
        public static AudioClip LoadClip(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{file}");

        static (Texture2D, Dictionary<string, RectInt>) LoadAtlas(string dir, string name)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(dir, name + ".png")));
            return (tex, ParseFrames(File.ReadAllText(Path.Combine(dir, name + ".plist"))));
        }

        /// <summary>Читает кадры из XML plist cocos2d/TexturePacker (format 2): имя → прямоугольник в пикселях.</summary>
        public static Dictionary<string, RectInt> ParseFrames(string plistXml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(plistXml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
                doc.Load(reader);
            var root = ReadDict(doc.SelectSingleNode("/plist/dict"));
            var result = new Dictionary<string, RectInt>();
            foreach (var kv in ReadDict(root["frames"]))
            {
                var frame = ReadDict(kv.Value);
                if (frame.TryGetValue("rotated", out var rot) && rot.Name == "true")
                {
                    Debug.LogWarning($"[Placeholders] rotated frame {kv.Key} is not supported, skipped");
                    continue;
                }
                var nums = frame["frame"].InnerText.Split(new[] { '{', '}', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                result[kv.Key] = new RectInt(nums[0], nums[1], nums[2], nums[3]);
            }
            return result;
        }

        static Dictionary<string, XmlNode> ReadDict(XmlNode dict)
        {
            var d = new Dictionary<string, XmlNode>();
            var children = dict.ChildNodes.Cast<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element).ToList();
            for (int i = 0; i + 1 < children.Count; i += 2)
                if (children[i].Name == "key") d[children[i].InnerText] = children[i + 1];
            return d;
        }

        static void ConfigureSprite(string path, Item item)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = item.Pivot;
            settings.spriteMeshType = item.Tiled ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static void ConfigureAudio(string path, bool stream)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            var s = importer.defaultSampleSettings;
            s.loadType = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = s;
            importer.SaveAndReimport();
        }

        /// <summary>Переписывает авто-таблицу в reference/PLACEHOLDER_ASSETS.md (всё между маркерами).</summary>
        static void WriteLedger()
        {
            string path = LedgerPath;
            if (!File.Exists(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine(LedgerBegin);
            sb.AppendLine($"_Обновлено автоматически {DateTime.Now:yyyy-MM-dd HH:mm} (меню Stickman Jumper → Import Placeholders)._");
            sb.AppendLine();
            sb.AppendLine("| Ассет оригинала | Файл в Unity | Где используется |");
            sb.AppendLine("|---|---|---|");
            foreach (var i in Sprites)
                sb.AppendLine($"| `{(i.Atlas != null ? i.Atlas + ".png → " : "")}{i.Source}` | `src/{SpritesFolder}/{i.Name}.png` | {i.Usage} |");
            foreach (var (source, usage) in Sounds)
                sb.AppendLine($"| `{source}` | `src/{AudioFolder}/{source}` | {usage} |");
            sb.AppendLine();
            sb.AppendLine($"Итого: {Sprites.Length} картинок, {Sounds.Length} звуков. Всё это перерисовать/переозвучить до релиза.");
            sb.AppendLine(LedgerEnd);

            string text = File.ReadAllText(path);
            int a = text.IndexOf(LedgerBegin, StringComparison.Ordinal);
            int b = text.IndexOf(LedgerEnd, StringComparison.Ordinal);
            text = a >= 0 && b > a
                ? text.Substring(0, a) + sb.ToString().TrimEnd() + text.Substring(b + LedgerEnd.Length)
                : text.TrimEnd() + "\n\n" + sb.ToString();
            File.WriteAllText(path, text);
        }
    }
}
