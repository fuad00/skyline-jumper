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
        static readonly Color SkyColor = new Color(0.55f, 0.75f, 0.95f);

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
            EnsureAsset<LevelConfig>("Level_easy1");
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
            Assign(so, "_level", Load<LevelConfig>("Config/Level_easy1.asset"));
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
            art.SkyGradient = GeneratedArt.Load("Sky");
            art.Star = GeneratedArt.Load("Star");
            art.StarEmpty = GeneratedArt.Load("StarEmpty");
            art.IconTime = GeneratedArt.Load("IconClock");
            art.IconWind = GeneratedArt.Load("IconWind");
            art.IconZoom = GeneratedArt.Load("IconZoom");

            art.Buildings = new[]
            {
                Building("office", new Color(0.62f, 0.66f, 0.7f)),
                Building("brick", new Color(0.7f, 0.38f, 0.22f)),
                Building("glass", new Color(0.4f, 0.62f, 0.8f)),
                Building("dark", new Color(0.18f, 0.18f, 0.2f)),
                Building("beige", new Color(0.8f, 0.72f, 0.6f)),
            };
            art.Head = PlaceholderImporter.LoadSprite("head");
            var frames = new System.Collections.Generic.List<Sprite>();
            for (int i = 1; i <= 8; i++)
            {
                var f = PlaceholderImporter.LoadSprite($"para0{i}");
                if (f != null) frames.Add(f);
            }
            art.CanopyOpening = frames.ToArray();
            art.CanopyOpen = PlaceholderImporter.LoadSprite("paraHolder");
            art.Arrow = GeneratedArt.Load("Arrow");
            art.Flag = PlaceholderImporter.LoadSprite("flag");
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
