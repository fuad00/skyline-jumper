using System.Collections.Generic;
using System.Globalization;
using System.IO;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using StickmanJumper.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Автозаезд: сам играет настоящую сцену Game. Прыжок 1 — автопилот (точная посадка), 2 — без парашюта (разбился),
    /// 3 — проспал край (сорвался), 4 — парашют сразу после толчка (снесёт ветром). Сохраняет кадры (картинка + UI)
    /// и smoke-summary.txt, падает на любой ошибке в логе или неверном итоге. Рекорд игрока восстанавливает.
    /// Без окна: Unity -batchmode -projectPath src -executeMethod StickmanJumper.EditorTools.SmokeTest.Run -smokeOut DIR -logFile LOG
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeTest
    {
        const string ActiveKey = "SJ.Smoke.Active", OutKey = "SJ.Smoke.Out", StartKey = "SJ.Smoke.Start", BackupKey = "SJ.Smoke.Backup";
        const string BackgroundKey = "SJ.Smoke.RunInBackground";
        const double TimeoutSeconds = 240;
        const int SnapW = 1280, SnapH = 720;

        static int _phase, _errors, _failures;
        static double _phaseTime;
        static bool _hooked;
        static string _pendingSnap;
        static int _snapFrames;
        static float _closeSize;      // >0: close-up of the stickman, camera half-height in metres
        static Vector2 _closeOffset;  // close-up centre relative to the stickman's middle
        static float _peak;
        static RenderTexture _uiRt;
        static readonly HashSet<string> Shots = new HashSet<string>();
        static readonly List<string> Lines = new List<string>();

        static SmokeTest()
        {
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Stickman Jumper/Run Smoke Test (auto-play)")]
        public static void Run()
        {
            // Unity 6.6 enters Play mode without a domain reload: statics survive between runs, reset them by hand.
            _phase = 0;
            _phaseTime = 0;
            _errors = 0;
            _failures = 0;
            _hooked = false;
            _pendingSnap = null;
            Shots.Clear();
            Armed.Clear();
            Lines.Clear();

            string outDir = Path.GetFullPath(ArgValue("-smokeOut") ?? "Logs/Smoke");
            Directory.CreateDirectory(outDir);
            foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);
            SessionState.SetString(OutKey, outDir);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(BackupKey, PlayerPrefs.GetString(Save.PlayerPrefsSaveStore.Key, ""));
            PlayerPrefs.DeleteKey(Save.PlayerPrefsSaveStore.Key); // play from a clean save; restored in Finish()
            // An unfocused live Editor freezes the player loop unless the game may run in background.
            SessionState.SetBool(BackgroundKey, Application.runInBackground);
            Application.runInBackground = true;
            _levelShot = 0;
            _tourStep = 0;
            _peak = 0f;

            var violations = PlaceholderGuard.FindViolations();
            Log(violations.Count == 0
                ? "release guard: OK, no placeholders in build scenes"
                : $"release guard: BUILD BLOCKED — build scenes reference {violations.Count} placeholder files (expected while prototyping)");

            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }


        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(StartKey, (float)now) > TimeoutSeconds)
            {
                Log($"TIMEOUT in phase {_phase}");
                _failures++;
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            if (!_hooked)
            {
                _hooked = true;
                Application.logMessageReceived += OnLog;
            }
            var g = Object.FindAnyObjectByType<GameRoot>();
            if (g == null || !g.Ready) return;
            if (_pendingSnap != null)
            {
                ProcessSnap(g);
                return;
            }
            var s = g.Session;

            switch (_phase)
            {
                case 0: // main menu, stage and level select (the game starts in the menu, like the original)
                    if (Wait(1.2)) { Snap("00_main_menu"); Next(); }
                    break;
                case 1: // menu tour: settings, «are you sure?», map builder stub, English
                    if (MenuTour(g)) _phase = 9;
                    break;
                case 9:
                    g.ShowStageSelect();
                    Next();
                    break;
                case 10:
                    if (Wait(0.6)) { Snap("01_stage_select"); Next(); }
                    break;
                case 11:
                    g.ShowLevelSelect(0);
                    Next();
                    break;
                case 12:
                    if (Wait(0.6)) { Snap("02_level_select"); Next(); }
                    break;
                case 13:
                    g.StartLevel(0, 0);
                    Next();
                    break;
                case 14: // overview before the first tap
                    CloseUp(Wait(0.6), "c01_stand");
                    if (Wait(1.2) && _pendingSnap == null) { Snap("03_ready_overview"); g.Autopilot = new Autopilot(); Next(); }
                    break;
                case 15: // run A: autopilot plans run-up + canopy height and should land on the flag
                    if (s.Phase == JumpPhase.Airborne && !s.CanopyDeployed) _peak = Mathf.Max(_peak, s.Feet.y - s.Level.RoofY);
                    ShotWhen(s.Phase == JumpPhase.BackingUp && s.Level.EdgeX - s.Feet.x > 2.2f, "04_walk_back_facing_away");
                    ShotWhen(s.Phase == JumpPhase.Running && s.Velocity.x > 5f, "05_running");
                    CloseUp(s.Phase == JumpPhase.BackingUp && s.Level.EdgeX - s.Feet.x > 1.5f, "c02_walk_back");
                    CloseUp(s.Phase == JumpPhase.Running && s.Velocity.x > 6f, "c03_run");
                    CloseUp(s.Phase == JumpPhase.Airborne && s.Elapsed - _takeoffAt > 0.12f && s.Velocity.y > 0f, "c04_takeoff", 2f);
                    CloseUp(s.Phase == JumpPhase.Airborne && !s.CanopyDeployed && s.Velocity.y < -8f, "c05_dive", 2f);
                    CloseUp(s.Phase == JumpPhase.Airborne && s.CanopyDeployed && s.Canopy > 0.3f && s.Canopy < 0.8f, "c06_canopy_opening", 4.5f, 2.5f);
                    CloseUp(s.Phase == JumpPhase.Airborne && s.Canopy >= 1f && s.Feet.y > s.Level.TargetRect.yMax + 4f, "c07_canopy", 4.5f, 2.5f);
                    if (s.Phase != JumpPhase.Airborne) _takeoffAt = s.Elapsed;
                    ShotWhen(s.Phase == JumpPhase.Airborne && s.Velocity.y > 0f && s.Feet.x > s.Level.EdgeX + 0.6f, "06_takeoff");
                    ShotWhen(s.Phase == JumpPhase.Airborne && !s.CanopyDeployed && s.Velocity.y < -14f, "07_freefall_guide");
                    ShotWhen(s.Phase == JumpPhase.Airborne && s.Canopy >= 1f && s.Feet.y < s.Level.TargetRect.yMax + 8f, "08_canopy");
                    ShotWhenDelayed(s.Phase == JumpPhase.Landed, "09_landed", 0.9);
                    if (s.Phase == JumpPhase.Landed && !Armed.ContainsKey("c08")) Armed["c08"] = EditorApplication.timeSinceStartup;
                    CloseUp(s.Phase == JumpPhase.Landed && EditorApplication.timeSinceStartup - Armed["c08"] > 0.5, "c08_landed_canopy_falls", 3f, 1f);
                    CloseUp(s.Phase == JumpPhase.Landed && EditorApplication.timeSinceStartup - Armed["c08"] > 1.8, "c09_landed_cheer", 2f);
                    if (g.UI.SummaryVisible)
                    {
                        Report("run A autopilot     ", g, JumpOutcome.WellDone);
                        Log(string.Format(CultureInfo.InvariantCulture, "jump peak above the roof: {0:0.00} m", _peak));
                        Next();
                    }
                    break;
                case 16:
                    if (Wait(0.8)) { Snap("10_summary_welldone"); Next(); }
                    break;
                case 17:
                    g.Autopilot = new Autopilot { NeverOpenCanopy = true, DesiredRunUp = 4f };
                    g.Retry();
                    Next();
                    break;
                case 18: // run B: «Last» flag of the previous run is on the roof; no canopy → crash
                    ShotWhen(s.Phase == JumpPhase.BackingUp && s.Level.EdgeX - s.Feet.x > 3f, "11_roof_markers_last_flag");
                    ShotWhenDelayed(g.Stickman.IsRagdoll, "12_crash", 0.3);
                    if (g.Stickman.IsRagdoll && !Armed.ContainsKey("c10")) Armed["c10"] = EditorApplication.timeSinceStartup;
                    CloseUp(g.Stickman.IsRagdoll && EditorApplication.timeSinceStartup - Armed["c10"] > 1.2, "c10_crash_ragdoll", 2.2f);
                    if (g.UI.SummaryVisible) { Report("run B no canopy     ", g, JumpOutcome.Died); Next(); }
                    break;
                case 19:
                    if (Wait(0.6)) { Snap("13_summary_died"); Next(); }
                    break;
                case 20:
                    g.Autopilot = new Autopilot { NeverJump = true, DesiredRunUp = 6f };
                    g.Retry();
                    Next();
                    break;
                case 21: // run C: never taps jump → stumbles off the edge
                    ShotWhenDelayed(g.Stickman.IsRagdoll, "14_stumble", 0.45);
                    if (g.Stickman.IsRagdoll && !Armed.ContainsKey("c11")) Armed["c11"] = EditorApplication.timeSinceStartup;
                    CloseUp(g.Stickman.IsRagdoll && EditorApplication.timeSinceStartup - Armed["c11"] > 1.5, "c11_stumble_ragdoll", 2.2f);
                    if (g.UI.SummaryVisible) { Report("run C missed edge   ", g, JumpOutcome.Died); Next(); }
                    break;
                case 22:
                    g.Autopilot = null;
                    g.Retry();
                    Next();
                    break;
                case 23: // pause bar
                    if (Wait(0.5)) { g.SetPaused(true); Next(); }
                    break;
                case 24:
                    if (Wait(0.3)) { Snap("15_pause"); g.SetPaused(false); Next(); }
                    break;
                case 25: // level select after progress: level 1 has stars, level 2 is unlocked
                    g.ShowLevelSelect(0);
                    Next();
                    break;
                case 26:
                    if (Wait(0.6)) { Snap("16_level_select_progress"); _levelShot = 0; Next(); }
                    break;
                case 27: // every level of stage 1 from above, to review the layouts
                    g.StartLevel(0, _levelShot);
                    Next();
                    break;
                case 28:
                    if (Wait(1.0))
                    {
                        Snap($"lvl_{_levelShot + 1:00}");
                        _levelShot++;
                        if (_levelShot < g.Catalog.Stages[0].Levels.Length) _phase = 27;
                        else Next();
                    }
                    break;
                case 29:
                    Finish();
                    break;
            }
        }

        static int _levelShot, _tourStep;
        static float _takeoffAt;

        /// <summary>Шаг тура по меню: действие, пауза, снимок. Действие следующего шага — только после снимка.</summary>
        static readonly (string shot, System.Action<GameRoot> act)[] Tour =
        {
            ("00b_settings", g => g.ShowSettings()),
            ("00c_reset_are_you_sure", g => g.AskResetProgress()),
            ("00d_map_builder_stub", g =>
            {
                g.UI.DialogBack(); // = «cancel»
                if (g.UI.DialogVisible) Fail("«cancel» did not close the reset dialog");
                g.ShowMainMenu();
                g.ShowBuilderStub();
            }),
            ("00e_menu_english", g => { g.UI.DialogBack(); g.SetLanguage(Language.En); g.ShowMainMenu(); }),
            ("00f_settings_english", g => g.ShowSettings()),
        };

        static bool MenuTour(GameRoot g)
        {
            int step = _tourStep / 2;
            if (step >= Tour.Length)
            {
                g.SetLanguage(Language.Ru);
                g.ShowMainMenu();
                return true;
            }
            if (_tourStep % 2 == 0)
            {
                Tour[step].act(g);
                _tourStep++;
                _phaseTime = EditorApplication.timeSinceStartup;
            }
            else if (Wait(0.5))
            {
                Snap(Tour[step].shot);
                _tourStep++;
            }
            return false;
        }

        static readonly Dictionary<string, double> Armed = new Dictionary<string, double>();

        static void ShotWhen(bool condition, string name)
        {
            if (condition && !Shots.Contains(name) && _pendingSnap == null)
            {
                Shots.Add(name);
                Snap(name);
            }
        }

        static void ShotWhenDelayed(bool condition, string name, double delay)
        {
            if (!condition || Shots.Contains(name)) return;
            double now = EditorApplication.timeSinceStartup;
            if (!Armed.TryGetValue(name, out var since)) Armed[name] = since = now;
            if (now - since >= delay) ShotWhen(true, name);
        }

        static void Fail(string message)
        {
            _failures++;
            Log("FAIL: " + message);
        }

        static void Report(string label, GameRoot g, JumpOutcome? expected)
        {
            var s = g.Session;
            Log(string.Format(CultureInfo.InvariantCulture,
                "{0}: outcome {1,-14} | run-up {2,4:0.0} m | time {3,5:0.00} s | to flag {4,6:0.00} m | points {5,4} | stars {6}",
                label, s.Outcome, s.RunUpLength, s.Elapsed, s.LandingDistance, g.LastScore.Points, g.LastScore.Stars));
            if (expected.HasValue && s.Outcome != expected.Value)
            {
                _failures++;
                Log($"FAIL: expected {expected.Value}, got {s.Outcome}");
            }
        }

        // ---------------------------------------------------------------- snapshots (scene + UI)

        /// <summary>Крупный план стикмена (камера на время снимка подъезжает к нему).</summary>
        static void CloseUp(bool condition, string name, float size = 1.6f, float up = 0f)
        {
            if (!condition || Shots.Contains(name) || _pendingSnap != null) return;
            Shots.Add(name);
            Snap(name);
            _closeSize = size;
            _closeOffset = new Vector2(0f, up);
        }

        static void Snap(string name)
        {
            _closeSize = 0f;
            var g = Object.FindAnyObjectByType<GameRoot>();
            var panel = g.GetComponent<UIDocument>().panelSettings;
            _uiRt = new RenderTexture(SnapW, SnapH, 24, RenderTextureFormat.ARGB32);
            panel.targetTexture = _uiRt;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            _pendingSnap = name;
            _snapFrames = 0;
        }

        static void ProcessSnap(GameRoot g)
        {
            if (++_snapFrames < 4) return; // let the UI panel repaint into the texture
            string name = _pendingSnap;
            _pendingSnap = null;
            var panel = g.GetComponent<UIDocument>().panelSettings;
            try
            {
                var cam = Camera.main;
                var camRt = RenderTexture.GetTemporary(SnapW, SnapH, 24);
                var prev = cam.targetTexture;
                var prevPos = cam.transform.position;
                float prevSize = cam.orthographicSize;
                if (_closeSize > 0f)
                {
                    var c = g.Stickman.Center + _closeOffset;
                    cam.transform.position = new Vector3(c.x, c.y, prevPos.z);
                    cam.orthographicSize = _closeSize;
                }
                cam.targetTexture = camRt;
                cam.Render();
                cam.Render(); // first offscreen render after a big move can use stale GPU-driven culling data
                cam.targetTexture = prev;
                cam.transform.position = prevPos;
                cam.orthographicSize = prevSize;

                var scene = Read(camRt);
                var ui = Read(_uiRt);
                var sp = scene.GetPixels();
                var up = ui.GetPixels();
                for (int i = 0; i < sp.Length; i++)
                {
                    var u = up[i]; // UI Toolkit renders premultiplied alpha into a transparent texture
                    sp[i] = new Color(u.r + sp[i].r * (1f - u.a), u.g + sp[i].g * (1f - u.a), u.b + sp[i].b * (1f - u.a), 1f);
                }
                scene.SetPixels(sp);
                scene.Apply();
                File.WriteAllBytes(Path.Combine(SessionState.GetString(OutKey, "Logs/Smoke"), name + ".png"), scene.EncodeToPNG());
                Object.DestroyImmediate(scene);
                Object.DestroyImmediate(ui);
                RenderTexture.ReleaseTemporary(camRt);
            }
            catch (System.Exception e)
            {
                Log($"snapshot {name} failed: {e.Message}");
                _failures++;
            }
            finally
            {
                panel.targetTexture = null;
                if (_uiRt != null) { _uiRt.Release(); Object.DestroyImmediate(_uiRt); _uiRt = null; }
            }
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        // ---------------------------------------------------------------- plumbing

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (stackTrace != null && stackTrace.Contains("UnityEditor.Search")) return; // editor-internal batch-mode noise
            _errors++;
            Lines.Add($"ERROR: {condition}");
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(ActiveKey, false);
            Application.runInBackground = SessionState.GetBool(BackgroundKey, Application.runInBackground);
            Armed.Clear();
            string backup = SessionState.GetString(BackupKey, "");
            if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(Save.PlayerPrefsSaveStore.Key);
            else PlayerPrefs.SetString(Save.PlayerPrefsSaveStore.Key, backup);
            PlayerPrefs.Save();
            var g = Object.FindAnyObjectByType<GameRoot>();
            if (g != null) g.Autopilot = null;

            int code = _errors > 0 || _failures > 0 ? 1 : 0;
            Lines.Add($"errors: {_errors}, failed checks: {_failures}");
            string outDir = SessionState.GetString(OutKey, "Logs/Smoke");
            File.WriteAllText(Path.Combine(outDir, "smoke-summary.txt"), string.Join("\n", Lines) + $"\nexit code {code}\n");
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }

        static bool Wait(double seconds)
        {
            double now = EditorApplication.timeSinceStartup;
            if (_phaseTime <= 0) _phaseTime = now;
            return now - _phaseTime >= seconds;
        }

        static void Next()
        {
            _phase++;
            _phaseTime = EditorApplication.timeSinceStartup;
        }

        static void Log(string line)
        {
            Lines.Add(line);
            Debug.Log("[Smoke] " + line);
        }

        static string ArgValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
