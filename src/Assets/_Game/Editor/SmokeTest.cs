using System.Collections.Generic;
using System.Globalization;
using System.IO;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
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
        const double TimeoutSeconds = 240;
        const int SnapW = 1280, SnapH = 720;

        static int _phase, _errors, _failures;
        static double _phaseTime;
        static bool _hooked;
        static string _pendingSnap;
        static int _snapFrames;
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
            SessionState.SetString(BackupKey, BestKey() is string k && PlayerPrefs.HasKey(k) ? PlayerPrefs.GetFloat(k).ToString(CultureInfo.InvariantCulture) : "");

            var violations = PlaceholderGuard.FindViolations();
            Log(violations.Count == 0
                ? "release guard: OK, no placeholders in build scenes"
                : $"release guard: BUILD BLOCKED — build scenes reference {violations.Count} placeholder files (expected while prototyping)");

            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static string BestKey() => "SJ.Best.easy1";

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
                PlayerPrefs.DeleteKey(BestKey());
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
                case 0: // overview before the first tap
                    if (Wait(1.2)) { Snap("01_ready_overview"); Next(); }
                    break;
                case 1:
                    g.Autopilot = new Autopilot();
                    Next();
                    break;
                case 2: // run A: autopilot, should land on the arrow
                    ShotWhen(s.Phase == JumpPhase.BackingUp && s.Level.EdgeX - s.Feet.x > 2.5f, "02_backing_up");
                    ShotWhen(s.Phase == JumpPhase.Running && s.Velocity.x > 6.5f, "03_running");
                    ShotWhen(s.Phase == JumpPhase.Airborne && !s.CanopyDeployed && s.Velocity.y < -12f, "04_freefall");
                    ShotWhen(s.Phase == JumpPhase.Airborne && s.Canopy >= 1f && s.Feet.y < s.Level.RoofY - 20f, "05_canopy");
                    ShotWhen(s.Phase == JumpPhase.Landed, "06_landed");
                    if (g.UI.SummaryVisible) { Report("run A autopilot     ", g, JumpOutcome.WellDone); Next(); }
                    break;
                case 3:
                    if (Wait(0.8)) { Snap("07_summary_welldone"); Next(); }
                    break;
                case 4:
                    g.Autopilot = new Autopilot { NeverOpenCanopy = true };
                    g.Retry();
                    Next();
                    break;
                case 5: // run B: no canopy → crash
                    ShotWhen(s.Phase == JumpPhase.Airborne && s.Feet.y < s.Level.TargetRect.yMax + 12f && s.Feet.y > 4f, "08_falling_no_canopy");
                    ShotWhenDelayed(g.Stickman.IsRagdoll, "09_crash", 0.35);
                    if (g.UI.SummaryVisible) { Report("run B no canopy     ", g, JumpOutcome.Died); Next(); }
                    break;
                case 6:
                    if (Wait(0.6)) { Snap("10_summary_died"); Next(); }
                    break;
                case 7:
                    g.Autopilot = new Autopilot { NeverJump = true };
                    g.Retry();
                    Next();
                    break;
                case 8: // run C: never taps jump → stumbles off the edge
                    ShotWhenDelayed(g.Stickman.IsRagdoll, "11_stumble", 0.5);
                    if (g.UI.SummaryVisible) { Report("run C missed edge   ", g, JumpOutcome.Died); Next(); }
                    break;
                case 9:
                    g.Autopilot = new Autopilot { DesiredRunUp = 2f, OpenRightAway = true };
                    g.Retry();
                    Next();
                    break;
                case 10: // run D: short run-up, canopy right after take-off → wind drags it away
                    ShotWhen(s.Phase == JumpPhase.Airborne && s.Canopy >= 1f && s.Feet.y < s.Level.RoofY - 8f, "12_early_canopy");
                    if (g.UI.SummaryVisible) { Report("run D early canopy  ", g, null); Next(); }
                    break;
                case 11:
                    if (Wait(0.6)) { Snap("13_summary_early"); Next(); }
                    break;
                case 12:
                    Finish();
                    break;
            }
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

        static void Report(string label, GameRoot g, JumpOutcome? expected)
        {
            var s = g.Session;
            Log(string.Format(CultureInfo.InvariantCulture,
                "{0}: outcome {1,-14} | run-up {2,4:0.0} m | time {3,5:0.00} s | to arrow {4,6:0.00} m | points {5,4} | stars {6}",
                label, s.Outcome, s.RunUpLength, s.Elapsed, s.LandingDistance, g.LastScore.Points, g.LastScore.Stars));
            if (expected.HasValue && s.Outcome != expected.Value)
            {
                _failures++;
                Log($"FAIL: expected {expected.Value}, got {s.Outcome}");
            }
        }

        // ---------------------------------------------------------------- snapshots (scene + UI)

        static void Snap(string name)
        {
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
                cam.targetTexture = camRt;
                cam.Render();
                cam.Render(); // first offscreen render after a big move can use stale GPU-driven culling data
                cam.targetTexture = prev;

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
            Armed.Clear();
            string backup = SessionState.GetString(BackupKey, "");
            if (float.TryParse(backup, NumberStyles.Float, CultureInfo.InvariantCulture, out float best)) PlayerPrefs.SetFloat(BestKey(), best);
            else PlayerPrefs.DeleteKey(BestKey());
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
