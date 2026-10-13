using System.Collections;
using System.Collections.Generic;
using System.IO;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using StickmanJumper.Maps;
using StickmanJumper.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace StickmanJumper.EditorTools
{
    /// <summary>
    /// Кадры для README: игра сама проходит экраны и уровень (автопилот) и снимает 1920×1080 с интерфейсом на английском.
    /// Прыжок на уровне <see cref="FlightLevel"/> пишется по кадрам: игровое время идёт ровно по 1/25 с на кадр, сколько бы
    /// ни занимала запись. GIF из кадров собирает scripts/readme-media.sh (ffmpeg). Сохранение игрока и его карты не трогает.
    /// Unity -batchmode -projectPath src -executeMethod StickmanJumper.EditorTools.ReadmeMedia.Run -mediaOut DIR -logFile LOG
    /// </summary>
    [InitializeOnLoad]
    public static class ReadmeMedia
    {
        const string ActiveKey = "SJ.Media.Active", OutKey = "SJ.Media.Out", BackupKey = "SJ.Media.Backup", StartKey = "SJ.Media.Start";
        const int ShotW = 1920, ShotH = 1080;
        const float FrameStep = 1f / 25f;
        const double TimeoutSeconds = 600;
        const string FlightLevel = "easy6", NightLevel = "night1";
        /// <summary>Конструктор открываем на карте из этого уровня (этап, номер): башни и промежуточные платформы.</summary>
        const int EditorStage = 2, EditorLevel = 3;

        static readonly Stack<IEnumerator> Script = new Stack<IEnumerator>();
        static readonly List<string> Lines = new List<string>();
        static RenderTexture _uiRt;
        static int _errors;

        static ReadmeMedia()
        {
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Stickman Jumper/Capture README Media")]
        public static void Run()
        {
            Script.Clear();
            Lines.Clear();
            _errors = 0;
            string outDir = Path.GetFullPath(Arg("-mediaOut") ?? "Logs/Media");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(Path.Combine(outDir, "frames"));
            SessionState.SetString(OutKey, outDir);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(BackupKey, PlayerPrefs.GetString(Save.PlayerPrefsSaveStore.Key, ""));
            PlayerPrefs.DeleteKey(Save.PlayerPrefsSaveStore.Key); // a fresh save; restored in Finish()
            MapStore.FolderOverride = Path.Combine(outDir, "maps"); // the real maps on this computer stay untouched
            Application.runInBackground = true;

            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f) > TimeoutSeconds)
            {
                Fail("timeout");
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            var g = Object.FindAnyObjectByType<GameRoot>();
            if (g == null || !g.Ready) return;
            if (_uiRt == null) Begin(g);

            // A tiny coroutine runner: a step that yields an IEnumerator runs it to the end first.
            while (Script.Count > 0)
            {
                var top = Script.Peek();
                if (!top.MoveNext())
                {
                    Script.Pop();
                    continue;
                }
                if (top.Current is IEnumerator inner) Script.Push(inner);
                else return; // wait for the next frame
            }
            Finish();
        }

        static void Begin(GameRoot g)
        {
            // The UI draws into a texture of the shot's size; the camera frames for 16:9 like a phone.
            _uiRt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32);
            var panel = g.GetComponent<UIDocument>().panelSettings;
            panel.targetTexture = _uiRt;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            Camera.main.aspect = ShotW / (float)ShotH;
            Script.Push(Play(g));
        }

        // ---------------------------------------------------------------- the script

        static IEnumerator Play(GameRoot g)
        {
            g.SetLanguage(Language.En);
            g.ShowStageSelect();
            yield return Seconds(1.2f);
            Shot(g, "stages");

            if (!Find(g, FlightLevel, out int stage, out int level)) yield break;
            g.StartLevel(stage, level);
            yield return Seconds(1.5f);
            Shot(g, "level");
            yield return RecordJump(g);

            if (Find(g, NightLevel, out stage, out level))
            {
                g.StartLevel(stage, level);
                yield return Seconds(0.5f);
                g.Autopilot = new Autopilot();
                yield return Until(() => g.Session.CanopyDeployed || g.Session.Phase.IsFinished(), 40f);
                yield return Seconds(1.0f);
                Shot(g, "night", g.Stickman.Center + new Vector2(0f, 1.5f), 9f);
                g.Autopilot = null;
            }

            g.ShowMaps();
            g.Builder.UI.FromLevel?.Invoke();
            g.ShowLevelSelect(EditorStage);
            yield return Seconds(0.5f);
            g.ClickLevel(EditorLevel);
            yield return Seconds(1.5f);
            if (!g.Builder.EditorOpen) Fail("the map editor did not open");
            else Shot(g, "editor");
            g.Builder.CloseEditor();
        }

        /// <summary>Прыжок от первого тапа до итога: каждый кадр — в frames/, по пути — крупные снимки фаз.</summary>
        static IEnumerator RecordJump(GameRoot g)
        {
            var s = g.Session;
            g.Autopilot = new Autopilot();
            Time.captureDeltaTime = FrameStep;
            int frame = 0;
            float runAt = -1f, jumpAt = -1f, canopyAt = -1f, landAt = -1f, summaryAt = -1f;
            var done = new HashSet<string>();
            void Once(string name, bool when, System.Action shot)
            {
                if (!when || !done.Add(name)) return;
                shot();
            }

            while (frame < 25 * 60)
            {
                float t = frame * FrameStep;
                if (runAt < 0f && s.Phase == JumpPhase.Running) runAt = t;
                if (jumpAt < 0f && s.Phase == JumpPhase.Airborne) jumpAt = t;
                if (canopyAt < 0f && s.CanopyDeployed) canopyAt = t;
                if (landAt < 0f && s.Phase.IsFinished()) landAt = t;
                if (summaryAt < 0f && g.UI.SummaryVisible) summaryAt = t;

                Frame(g, frame++);
                var c = g.Stickman.Center;
                Once("run", runAt >= 0f && t - runAt > 1.0f, () => Shot(g, "run", c + new Vector2(3f, 1.5f), 6.5f));
                Once("canopy", canopyAt >= 0f && t - canopyAt > 0.7f && landAt < 0f, () => Shot(g, "canopy", c + new Vector2(0f, 1f), 6f));
                Once("landing", landAt >= 0f && t - landAt > 1.2f && summaryAt < 0f, () => Shot(g, "landing", c + new Vector2(0f, 1.2f), 4.5f));
                if (summaryAt >= 0f && t - summaryAt > 0.8f && done.Add("summary")) Shot(g, "summary");
                if (summaryAt >= 0f && t - summaryAt > 2.5f) break;
                yield return null;
            }
            Time.captureDeltaTime = 0f;
            g.Autopilot = null;
            if (s.Outcome != JumpOutcome.WellDone) Fail($"the recorded jump ended with {s.Outcome}");
            Lines.Add($"jump: {frame} frames, outcome {s.Outcome}, {s.LandingDistance:0.00} m from the flag");
            // Where each phase starts (frame numbers) — scripts/readme-media.sh cuts the long fall out of the GIF by these.
            int F(float at) => Mathf.RoundToInt(at / FrameStep);
            File.WriteAllText(Path.Combine(Out, "frames", "marks.txt"),
                $"run={F(runAt)} jump={F(jumpAt)} canopy={F(canopyAt)} land={F(landAt)} summary={F(summaryAt)} end={frame}\n");
        }

        /// <summary>Кадр для GIF: камера ближе игровой (стикмен в оригинальном кадре на GIF был бы с ноготь).</summary>
        static void Frame(GameRoot g, int index)
        {
            var cam = Camera.main;
            Vector2 center = Vector2.Lerp(cam.transform.position, g.Stickman.Center, 0.7f);
            float size = Mathf.Clamp(cam.orthographicSize * 0.45f, 5f, 9f);
            var tex = Render(g, center, size);
            File.WriteAllBytes(Path.Combine(Out, "frames", $"{index:0000}.jpg"), tex.EncodeToJPG(92));
            Object.DestroyImmediate(tex);
        }

        static void Shot(GameRoot g, string name, Vector2? center = null, float size = 0f)
        {
            var tex = Render(g, center, size);
            File.WriteAllBytes(Path.Combine(Out, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Lines.Add($"shot: {name}");
        }

        /// <summary>Мир камерой (можно ближе: центр и половина высоты кадра в метрах) + интерфейс поверх.</summary>
        static Texture2D Render(GameRoot g, Vector2? center, float size)
        {
            var cam = Camera.main;
            var camRt = RenderTexture.GetTemporary(ShotW, ShotH, 24);
            var prev = cam.targetTexture;
            var prevPos = cam.transform.position;
            float prevSize = cam.orthographicSize;
            if (center.HasValue)
            {
                cam.transform.position = new Vector3(center.Value.x, center.Value.y, prevPos.z);
                cam.orthographicSize = size;
            }
            cam.targetTexture = camRt;
            cam.Render();
            cam.Render(); // the first offscreen render after a big move can use stale culling data
            cam.targetTexture = prev;
            cam.transform.position = prevPos;
            cam.orthographicSize = prevSize;

            var scene = Read(camRt);
            var ui = Read(_uiRt);
            RenderTexture.ReleaseTemporary(camRt);
            var sp = scene.GetPixels32();
            var up = ui.GetPixels32();
            for (int i = 0; i < sp.Length; i++)
            {
                var u = up[i]; // UI Toolkit renders premultiplied alpha into a transparent texture
                int k = 255 - u.a;
                sp[i] = new Color32((byte)(u.r + sp[i].r * k / 255), (byte)(u.g + sp[i].g * k / 255), (byte)(u.b + sp[i].b * k / 255), 255);
            }
            scene.SetPixels32(sp);
            scene.Apply();
            Object.DestroyImmediate(ui);
            return scene;
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

        // ---------------------------------------------------------------- waiting

        /// <summary>Подождать столько игровых секунд (во время записи они идут по кадрам, а не по часам).</summary>
        static IEnumerator Seconds(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        static IEnumerator Until(System.Func<bool> condition, float timeout)
        {
            float end = Time.unscaledTime + timeout;
            while (!condition())
            {
                if (Time.unscaledTime > end)
                {
                    Fail("waited too long");
                    yield break;
                }
                yield return null;
            }
        }

        // ---------------------------------------------------------------- plumbing

        static string Out => SessionState.GetString(OutKey, "Logs/Media");

        static bool Find(GameRoot g, string id, out int stage, out int level)
        {
            var stages = g.Catalog.Stages;
            for (stage = 0; stage < stages.Length; stage++)
                for (level = 0; stages[stage] != null && level < stages[stage].Levels.Length; level++)
                    if (stages[stage].Levels[level] != null && stages[stage].Levels[level].Id == id) return true;
            stage = level = -1;
            Fail($"level {id} not found (are the original levels imported?)");
            return false;
        }

        static void Fail(string message)
        {
            _errors++;
            Lines.Add("FAIL: " + message);
            Debug.LogWarning("[Media] " + message);
        }

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            Time.captureDeltaTime = 0f;
            Script.Clear();
            var g = Object.FindAnyObjectByType<GameRoot>();
            if (g != null)
            {
                g.Autopilot = null;
                g.GetComponent<UIDocument>().panelSettings.targetTexture = null;
            }
            if (Camera.main != null) Camera.main.ResetAspect();
            if (_uiRt != null)
            {
                _uiRt.Release();
                Object.DestroyImmediate(_uiRt);
                _uiRt = null;
            }
            MapStore.FolderOverride = null;
            string backup = SessionState.GetString(BackupKey, "");
            if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey(Save.PlayerPrefsSaveStore.Key);
            else PlayerPrefs.SetString(Save.PlayerPrefsSaveStore.Key, backup);
            PlayerPrefs.Save();

            Lines.Add($"failures: {_errors}");
            File.WriteAllText(Path.Combine(Out, "media-summary.txt"), string.Join("\n", Lines) + "\n");
            if (Application.isBatchMode) EditorApplication.Exit(_errors > 0 ? 1 : 0);
            else EditorApplication.ExitPlaymode();
        }
    }
}
