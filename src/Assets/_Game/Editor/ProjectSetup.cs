using System.IO;
using StickmanJumper.Config;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Сборка проекта одной кнопкой (или без окна): заглушки из reference/, своя графика, настройки с цифрами по умолчанию,
    /// банки картинок и звуков, UI, сцена Game, настройки сборки под Android. Можно запускать сколько угодно раз:
    /// уже созданные настройки (и твои цифры в них) не трогаются, сцена пересобирается.
    /// Без окна: Unity -batchmode -quit -projectPath src -executeMethod StickmanJumper.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Game";
        public const string ScenePath = Root + "/Scenes/Game.unity";
        static readonly Color SkyColor = new Color(0.9f, 0.89f, 0.85f); // paper, in case the sheet sprite is missing

        [MenuItem("Stickman Jumper/Setup Project (placeholders, art, configs, scene)")]
        public static void Run()
        {
            Directory.CreateDirectory(Root + "/Config");
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Art");

            PlaceholderImporter.Run();
            GeneratedArt.EnsureAll();
            EnsureAsset<JumpConfig>("JumpConfig");
            EnsureAsset<FlightConfig>("FlightConfig");
            EnsureAsset<ScoreConfig>("ScoreConfig");
            EnsureAsset<CameraConfig>("CameraConfig");
            EnsureAsset<StickmanConfig>("StickmanConfig");
            EnsureCatalog();
            FillArtBank(EnsureAsset<ArtBank>("ArtBank"));
            FillSoundBank(EnsureAsset<SoundBank>("SoundBank"));
            EnsurePanelSettings();
            AssetDatabase.SaveAssets();

            // Opening a new single scene unloads unused assets, so objects held from before become fake-null.
            // Create the scene first, then (re)load every asset by path for the references.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.transform.position = new Vector3(0f, 30f, -10f);

            var rootGo = new GameObject("GameRoot");
            var doc = rootGo.AddComponent<UIDocument>();
            doc.panelSettings = Load<PanelSettings>("UI/GamePanelSettings.asset");
            doc.visualTreeAsset = Load<VisualTreeAsset>("UI/Game.uxml");
            var game = rootGo.AddComponent<GameRoot>();
            var so = new SerializedObject(game);
            Assign(so, "_catalog", Load<GameCatalog>("Config/GameCatalog.asset"));
            Assign(so, "_jumpConfig", Load<JumpConfig>("Config/JumpConfig.asset"));
            Assign(so, "_flightConfig", Load<FlightConfig>("Config/FlightConfig.asset"));
            Assign(so, "_scoreConfig", Load<ScoreConfig>("Config/ScoreConfig.asset"));
            Assign(so, "_cameraConfig", Load<CameraConfig>("Config/CameraConfig.asset"));
            Assign(so, "_stickmanConfig", Load<StickmanConfig>("Config/StickmanConfig.asset"));
            Assign(so, "_art", Load<ArtBank>("Config/ArtBank.asset"));
            Assign(so, "_sounds", Load<SoundBank>("Config/SoundBank.asset"));
            Assign(so, "_camera", cam);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Stickman Jumper project setup complete.");
        }

        // ------------------------------------------------------------------ levels

        // Facade widths of the placeholder styles at PPU 22 (so buildings are not squashed): Office, Brick, Glass, Dark, Beige.
        const float Office = 16.27f, Brick = 18.18f, Glass = 14.55f, Dark = 15.45f, Beige = 16.9f;

        static BuildingSpec StartOn(BuildingStyle style, float width, float height) => new BuildingSpec(-width, width, height, style);
        static BuildingSpec At(BuildingStyle style, float x, float width, float height) => new BuildingSpec(x, width, height, style);

        sealed class LevelDef
        {
            public string Id;
            public BuildingSpec Start, Target;
            public BuildingSpec[] Others = new BuildingSpec[0];
            public float Flag = 0.5f, Wind, Time = 30f;
        }

        /// <summary>Этап 1 «новичок» — 10 прыжков от простого к хитрому (ветер, встречный ветер, дома по пути).</summary>
        static readonly LevelDef[] Stage1 =
        {
            new LevelDef { Id = "s1_01", Start = StartOn(BuildingStyle.Office, Office, 60f), Target = At(BuildingStyle.Brick, 17f, Brick, 18f), Wind = 1.5f },
            new LevelDef { Id = "s1_02", Start = StartOn(BuildingStyle.Glass, Glass, 52f), Target = At(BuildingStyle.Beige, 13f, Beige, 20f), Flag = 0.35f, Wind = 0.5f },
            new LevelDef { Id = "s1_03", Start = StartOn(BuildingStyle.Dark, Dark, 70f), Target = At(BuildingStyle.Office, 22f, Office, 12f), Flag = 0.6f, Wind = 2.5f, Time = 35f },
            new LevelDef { Id = "s1_04", Start = StartOn(BuildingStyle.Office, Office, 65f), Target = At(BuildingStyle.Glass, 15f, Glass, 22f), Flag = 0.45f, Wind = -1.5f },
            new LevelDef { Id = "s1_05", Start = StartOn(BuildingStyle.Beige, Beige, 72f), Others = new[] { At(BuildingStyle.Dark, 8f, Dark, 28f) }, Target = At(BuildingStyle.Brick, 30f, Brick, 14f), Wind = 3f, Time = 40f },
            new LevelDef { Id = "s1_06", Start = StartOn(BuildingStyle.Glass, Glass, 58f), Target = At(BuildingStyle.Glass, 19f, Glass, 26f), Flag = 0.8f, Wind = 1f },
            new LevelDef { Id = "s1_07", Start = StartOn(BuildingStyle.Office, Office, 82f), Target = At(BuildingStyle.Beige, 27f, Beige, 8f), Flag = 0.4f, Wind = 2f, Time = 40f },
            new LevelDef { Id = "s1_08", Start = StartOn(BuildingStyle.Brick, Brick, 62f), Target = At(BuildingStyle.Office, 12f, Office, 16f), Flag = 0.3f, Wind = 4f, Time = 35f },
            new LevelDef { Id = "s1_09", Start = StartOn(BuildingStyle.Glass, Glass, 76f), Others = new[] { At(BuildingStyle.Office, 5f, Office, 44f) }, Target = At(BuildingStyle.Dark, 27f, Dark, 20f), Wind = 1f, Time = 40f },
            new LevelDef { Id = "s1_10", Start = StartOn(BuildingStyle.Dark, Dark, 90f), Others = new[] { At(BuildingStyle.Beige, 14f, Beige, 36f) }, Target = At(BuildingStyle.Brick, 36f, Brick, 10f), Wind = 3f, Time = 45f },
        };

        static void EnsureCatalog()
        {
            Directory.CreateDirectory(Root + "/Config/Levels");
            // The very first prototype level lived at Config/Level_easy1; it is now Stage 1 / Level 1.
            if (AssetDatabase.LoadAssetAtPath<LevelConfig>(Root + "/Config/Level_easy1.asset") != null)
                AssetDatabase.DeleteAsset(Root + "/Config/Level_easy1.asset");

            var levels = new LevelConfig[Stage1.Length];
            for (int i = 0; i < Stage1.Length; i++) levels[i] = EnsureLevel(Stage1[i], i + 1);

            var stage1 = EnsureAsset<StageConfig>("Stage1");
            stage1.Number = 1;
            stage1.Title = "новичок";
            stage1.Subtitle = "первые прыжки";
            stage1.TitleEn = "rookie";
            stage1.SubtitleEn = "first jumps";
            stage1.StarsToUnlock = 0;
            stage1.Levels = levels;
            EditorUtility.SetDirty(stage1);

            // Like the original's second card: locked until enough stars. Its levels come later.
            var stage2 = EnsureAsset<StageConfig>("Stage2");
            stage2.Number = 2;
            stage2.Title = "опытный";
            stage2.Subtitle = "тут ветрено";
            stage2.TitleEn = "advanced";
            stage2.SubtitleEn = "it gets windy";
            stage2.StarsToUnlock = 20;
            EditorUtility.SetDirty(stage2);

            var catalog = EnsureAsset<GameCatalog>("GameCatalog");
            catalog.Stages = new[] { stage1, stage2 };
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Создаёт уровень, если его ещё нет. Уже созданный не трогаем — в нём могут быть твои цифры.</summary>
        static LevelConfig EnsureLevel(LevelDef d, int number)
        {
            string path = $"{Root}/Config/Levels/{d.Id}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            if (level != null) return level;
            level = ScriptableObject.CreateInstance<LevelConfig>();
            level.Id = d.Id;
            level.Title = $"Прыжок {number}";
            level.Start = d.Start;
            level.Target = d.Target;
            level.Others = d.Others;
            level.ArrowPosition = d.Flag;
            level.Wind = d.Wind;
            level.TimeLimit = d.Time;
            level.ThreeStarTime = d.Time * 0.5f;
            AssetDatabase.CreateAsset(level, path);
            return level;
        }

        [MenuItem("Stickman Jumper/Rebuild Levels (overwrites level tuning!)")]
        static void RebuildLevels()
        {
            foreach (var d in Stage1) AssetDatabase.DeleteAsset($"{Root}/Config/Levels/{d.Id}.asset");
            EnsureCatalog();
            AssetDatabase.SaveAssets();
        }

        static void Assign(SerializedObject so, string field, Object value)
        {
            so.FindProperty(field).objectReferenceValue = value;
            if (value == null) Debug.LogError($"[Setup] GameRoot.{field} is not assigned");
        }

        static T Load<T>(string relativePath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{Root}/{relativePath}");
            if (asset == null) Debug.LogError($"[Setup] Missing asset {Root}/{relativePath}");
            return asset;
        }

        static T EnsureAsset<T>(string name) where T : ScriptableObject
        {
            string path = $"{Root}/Config/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Банк картинок пересобирается всегда: свои + заглушки (если импортированы).</summary>
        static void FillArtBank(ArtBank art)
        {
            art.SpriteMaterial = EnsureSpriteMaterial();
            art.White = GeneratedArt.Load("White");
            art.Circle = GeneratedArt.Load("Circle");
            art.Limb = GeneratedArt.Load("Limb");
            art.Paper = PlaceholderImporter.LoadSprite("paper") ?? GeneratedArt.Load("Paper");
            art.FogBand = GeneratedArt.Load("FogBand");
            art.Dash = GeneratedArt.Load("Dash");
            art.Star = GeneratedArt.Load("Star");
            art.StarEmpty = GeneratedArt.Load("StarEmpty");
            art.IconTime = GeneratedArt.Load("IconClock");
            art.IconWind = GeneratedArt.Load("IconWind");
            art.IconZoom = GeneratedArt.Load("IconZoom");
            art.IconPause = GeneratedArt.Load("IconPause");
            art.IconLock = GeneratedArt.Load("IconLock");

            art.Buildings = new[]
            {
                Building("office", new Color(0.62f, 0.66f, 0.7f)),
                Building("brick", new Color(0.7f, 0.38f, 0.22f)),
                Building("glass", new Color(0.4f, 0.62f, 0.8f)),
                Building("dark", new Color(0.18f, 0.18f, 0.2f)),
                Building("beige", new Color(0.8f, 0.72f, 0.6f)),
            };
            art.Head = PlaceholderImporter.LoadSprite("head");
            art.HeadFront = PlaceholderImporter.LoadSprite("head01");
            art.MarkerArrow = PlaceholderImporter.LoadSprite("downArrow");
            var frames = new System.Collections.Generic.List<Sprite>();
            for (int i = 1; i <= 8; i++)
            {
                var f = PlaceholderImporter.LoadSprite($"para0{i}");
                if (f != null) frames.Add(f);
            }
            art.CanopyOpening = frames.ToArray();
            art.CanopyOpen = PlaceholderImporter.LoadSprite("paraHolder");
            art.Flag = PlaceholderImporter.LoadSprite("flag");
            art.FlagLast = PlaceholderImporter.LoadSprite("lastFlag");
            art.Digits = LoadAll("digit0", "digit1", "digit2", "digit3", "digit4", "digit5", "digit6", "digit7", "digit8", "digit9");
            art.Skyline = PlaceholderImporter.LoadSprite("skyline");
            art.Clouds = LoadAll("cloud1", "cloud2", "cloud3");
            art.Trees = LoadAll("tree01", "tree02", "tree03", "tree04");
            art.Bushes = LoadAll("bush01", "bush02", "bush03", "bush04");
            art.Lamp = PlaceholderImporter.LoadSprite("streetlamp");
            art.Hydrant = PlaceholderImporter.LoadSprite("hydrant");
            EditorUtility.SetDirty(art);
        }

        static BuildingArt Building(string prefix, Color fallback) => new BuildingArt
        {
            Body = PlaceholderImporter.LoadSprite(prefix + "_floors"),
            Ground = PlaceholderImporter.LoadSprite(prefix + "_ground"),
            Roof = PlaceholderImporter.LoadSprite(prefix + "_roof"),
            Fallback = fallback,
        };

        static Sprite[] LoadAll(params string[] names)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var n in names)
            {
                var s = PlaceholderImporter.LoadSprite(n);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        static void FillSoundBank(SoundBank bank)
        {
            bank.Footsteps = PlaceholderImporter.LoadClip("footsteps.ogg");
            bank.Jump = PlaceholderImporter.LoadClip("jump.ogg");
            bank.AirLoop = PlaceholderImporter.LoadClip("air1.ogg");
            bank.WindLoop = PlaceholderImporter.LoadClip("windhowl.ogg");
            bank.CanopyOpen = PlaceholderImporter.LoadClip("parachuteOpen.ogg");
            bank.Landing = Clips("parachuteLanding1.ogg", "parachuteLanding2.ogg");
            var yells = new string[13];
            for (int i = 0; i < 13; i++) yells[i] = $"yell{i}.ogg";
            bank.Yells = Clips(yells);
            bank.Bones = Clips("bones1.ogg", "bones2.ogg", "bones3.ogg", "bones4.ogg", "bones5.ogg", "bones6.ogg");
            bank.Smash = Clips("smash1.ogg", "smash2.ogg", "smash3.ogg");
            bank.Success = PlaceholderImporter.LoadClip("success.ogg");
            bank.Click = PlaceholderImporter.LoadClip("click.ogg");
            bank.Music = PlaceholderImporter.LoadClip("soundtrack.ogg");
            EditorUtility.SetDirty(bank);
        }

        static AudioClip[] Clips(params string[] files)
        {
            var list = new System.Collections.Generic.List<AudioClip>();
            foreach (var f in files)
            {
                var c = PlaceholderImporter.LoadClip(f);
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        static Material EnsureSpriteMaterial()
        {
            string path = $"{Root}/Art/SpriteUnlit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsurePanelSettings()
        {
            string themePath = $"{Root}/UI/GameTheme.tss";
            if (!File.Exists(themePath))
            {
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath);
            }
            string path = $"{Root}/UI/GamePanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            // Expand: the 1920x1080 reference always fits; extra space goes to the longer axis (wide phones get more width).
            ps.screenMatchMode = PanelScreenMatchMode.Expand;
            EditorUtility.SetDirty(ps);
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "StickmanJumper";
            PlayerSettings.productName = "Stickman Jumper";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.stickmanjumper.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }
    }
}
