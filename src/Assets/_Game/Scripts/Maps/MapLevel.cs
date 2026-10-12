using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Maps
{
    /// <summary>
    /// Карта ↔ уровень игры. Карта превращается в обычный <see cref="LevelConfig"/> со сценой (<see cref="LevelScene"/>),
    /// поэтому играется той же физикой и рисуется так же, как уровни оригинала. Уровень оригинала можно открыть в редакторе как карту.
    /// </summary>
    public static class MapLevel
    {
        /// <summary>Высота коробок площадок, как у оригинала (низ — поверхность).</summary>
        const float RunwayBox = 1.1f, PlatformBox = 0.7f;
        /// <summary>Самая короткая полоса разбега: меньше — не успеть разогнаться для прыжка, м.</summary>
        public const float MinStartWidth = 4f;
        /// <summary>Самая короткая цель и промежуточная платформа, м.</summary>
        public const float MinTargetWidth = 1.5f;

        /// <summary>Уровень для игры (ассеты создаются в памяти — удалить через <see cref="Destroy"/>).</summary>
        public static LevelConfig ToLevel(MapData map, PieceCatalog catalog)
        {
            var scene = ScriptableObject.CreateInstance<LevelScene>();
            scene.name = map.Name;
            var pictures = new List<LevelScene.Picture>();
            var solids = new List<Rect>();
            float x1 = Mathf.Max(map.Start.XMax, map.Target.XMax), y1 = Mathf.Max(map.Start.Y, map.Target.Y);
            foreach (var p in map.Pieces)
            {
                if (catalog == null || !catalog.TryGet(p.Art, out var def)) continue;
                pictures.Add(new LevelScene.Picture
                {
                    Sprite = def.Sprite,
                    Position = p.Position,
                    Scale = p.Scale,
                    Rotation = p.Rotation,
                    Layer = p.Layer,
                    Alpha = 1f,
                    // At night everything but the sky lights up only near the stickman, like the original's night levels.
                    Kind = map.Night && !def.AlwaysLit && string.IsNullOrEmpty(def.Kind) ? "LightSprite" : def.Kind,
                    AlwaysLit = def.AlwaysLit,
                    Solid = p.Solid,
                });
                MapGeometry.AddSolids(solids, catalog, p);
                var b = MapGeometry.OpaqueBounds(def, p);
                x1 = Mathf.Max(x1, b.xMax);
                y1 = Mathf.Max(y1, b.yMax);
            }
            scene.Pictures = pictures.ToArray();
            scene.Solids = solids.ToArray();
            scene.Runway = new Rect(map.Start.X, map.Start.Y, map.Start.Width, RunwayBox);
            scene.Platform = new Rect(map.Target.X, map.Target.Y, map.Target.Width, PlatformBox);
            scene.InterPlatforms = new Rect[map.Platforms.Count];
            scene.InterLeft = new bool[map.Platforms.Count];
            for (int i = 0; i < map.Platforms.Count; i++)
            {
                var s = map.Platforms[i];
                scene.InterPlatforms[i] = new Rect(s.X, s.Y, s.Width, RunwayBox);
                scene.InterLeft[i] = s.Left;
                x1 = Mathf.Max(x1, s.XMax);
                y1 = Mathf.Max(y1, s.Y);
            }
            scene.Size = new Vector2(Mathf.Max(1f, x1), Mathf.Max(1f, y1));
            scene.Night = map.Night;
            scene.WindRaw = map.Wind * PictureShapes.PixelsPerMeter;

            var level = ScriptableObject.CreateInstance<LevelConfig>();
            level.name = map.Name;
            level.Id = map.LevelId;
            level.Title = map.Name;
            level.Scene = scene;
            level.TimeLimit = Mathf.Clamp(map.TimeLimit, MapData.MinTime, MapData.MaxTime);
            level.Wind = Mathf.Clamp(map.Wind, -MapData.MaxWind, MapData.MaxWind);
            level.WindMax = MapData.MaxWind;
            level.MaxFallSpeed = 0f;
            level.ThreeStarTime = level.TimeLimit * 0.6f;
            return level;
        }

        /// <summary>Удалить уровень, созданный <see cref="ToLevel"/>.</summary>
        public static void Destroy(LevelConfig level)
        {
            if (level == null) return;
            if (level.Scene != null) Object.Destroy(level.Scene);
            Object.Destroy(level);
        }

        /// <summary>Что мешает сыграть карту (пусто — можно играть).</summary>
        public static List<string> Problems(MapData map)
        {
            var list = new List<string>();
            if (map.Start.Width < MinStartWidth - 0.01f) list.Add("start");
            if (map.Target.Width < MinTargetWidth - 0.01f) list.Add("target");
            return list;
        }

        /// <summary>Уровень оригинала → карта для редактора (картинки, которых нет среди деталей, пропускаются).</summary>
        public static MapData FromLevel(LevelConfig level, PieceCatalog catalog, string name)
        {
            var map = new MapData { Name = name, TimeLimit = level.TimeLimit, Wind = level.Wind };
            var scene = level.Scene;
            if (scene == null) return map;
            map.Night = scene.Night;
            foreach (var p in scene.Pictures)
            {
                string id = catalog != null ? catalog.IdOf(p.Sprite) : null;
                if (id == null) continue;
                map.Pieces.Add(new MapPiece
                {
                    Art = id,
                    X = p.Position.x,
                    Y = p.Position.y,
                    ScaleX = p.Scale.x,
                    ScaleY = p.Scale.y,
                    Rotation = p.Rotation,
                    Layer = p.Layer,
                    Solid = p.Solid,
                });
            }
            map.Start = new MapStrip(scene.Runway.xMin, scene.Runway.yMin, scene.Runway.width);
            map.Target = new MapStrip(scene.Platform.xMin, scene.Platform.yMin, scene.Platform.width);
            for (int i = 0; i < scene.InterPlatforms.Length; i++)
            {
                var r = scene.InterPlatforms[i];
                map.Platforms.Add(new MapStrip(r.xMin, r.yMin, r.width, scene.InterLeft != null && i < scene.InterLeft.Length && scene.InterLeft[i]));
            }
            SettleStrips(map, catalog);
            return map;
        }

        /// <summary>Площадки — на поверхность под ними (если она рядом), как сядет стикмен.</summary>
        public static void SettleStrips(MapData map, PieceCatalog catalog)
        {
            var solids = MapGeometry.Solids(map, catalog);
            Settle(map.Start, solids);
            Settle(map.Target, solids);
            foreach (var s in map.Platforms) Settle(s, solids);
        }

        public static void Settle(MapStrip s, List<Rect> solids)
        {
            float y = MapGeometry.SurfaceUnder(solids, s.X, s.XMax, s.Y);
            if (!float.IsNaN(y)) s.Y = y;
        }

        // ------------------------------------------------------------------ new map

        /// <summary>
        /// Новая карта: высокий дом слева (старт на крыше), дом пониже справа (флажок), пара деревьев и облако.
        /// Сразу играется — дальше игрок переделывает как хочет.
        /// </summary>
        public static MapData NewMap(PieceCatalog catalog, string name)
        {
            var map = new MapData { Name = name, TimeLimit = 30f };
            float startRight = 0f, startTop = 38f, targetLeft = 30f, targetRight = 50f, targetTop = 18f;
            if (catalog != null && catalog.TryGet("building10FirstFloor", out _) && catalog.TryGet("building07", out _))
            {
                // A 3 × 5-floor tower on its ground floor, with the roof border on top.
                var b = Stack(map, catalog, "building10FirstFloor", 0f, 0f);
                for (int i = 0; i < 3; i++) b = Stack(map, catalog, "building105Floors", b.center.x, b.yMax);
                b = Stack(map, catalog, "building10Boarder01", b.center.x, b.yMax);
                startRight = b.xMax;
                startTop = b.yMax;
                var t = Stack(map, catalog, "building07", startRight + 30f, 0f);
                targetLeft = t.xMin;
                targetRight = t.xMax;
                targetTop = t.yMax;
                Decor(map, catalog, "tree02", startRight + 7f, 0f);
                Decor(map, catalog, "tree05", startRight + 13f, 0f);
                Decor(map, catalog, "cloud1", startRight + 16f, startTop - 6f);
            }
            map.Start = new MapStrip(startRight - 12f, startTop, 12f); // the jump edge is the roof's right edge
            float targetWidth = Mathf.Min(8f, targetRight - targetLeft - 1f);
            map.Target = new MapStrip((targetLeft + targetRight - targetWidth) * 0.5f, targetTop, targetWidth);
            SettleStrips(map, catalog);
            return map;
        }

        /// <summary>Ставит деталь так, чтобы низ её непрозрачной части был на bottom, а центр — на centerX. Возвращает её рамку.</summary>
        static Rect Stack(MapData map, PieceCatalog catalog, string id, float centerX, float bottom)
        {
            if (!catalog.TryGet(id, out var def)) return new Rect(centerX, bottom, 0f, 0f);
            var p = new MapPiece { Art = id, ScaleX = def.Scale, ScaleY = def.Scale, Layer = def.Layer, Solid = def.Solid };
            var b = MapGeometry.OpaqueBounds(def, p); // placed at the origin
            p.Position = new Vector2(centerX - b.center.x, bottom - b.yMin);
            map.Pieces.Add(p);
            return MapGeometry.OpaqueBounds(def, p);
        }

        static void Decor(MapData map, PieceCatalog catalog, string id, float centerX, float bottom)
        {
            if (catalog.TryGet(id, out _)) Stack(map, catalog, id, centerX, bottom);
        }
    }
}
