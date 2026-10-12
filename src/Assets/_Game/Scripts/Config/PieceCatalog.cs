using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickmanJumper.Config
{
    /// <summary>Раздел палитры редактора карт.</summary>
    public enum PieceGroup { Buildings = 0, Construction = 1, Street = 2, Sky = 3 }

    /// <summary>Деталь редактора карт: картинка, её столкновения и что ставить по умолчанию.</summary>
    [Serializable]
    public struct PieceDef
    {
        [Tooltip("Имя детали в карте и в коде для обмена. Не менять: иначе старые карты её не найдут.")]
        public string Id;
        public Sprite Sprite;
        public PieceGroup Group;
        [Tooltip("Размер картинки, px (44 px = 1 м при масштабе 1).")]
        public Vector2 SizePx;
        [Tooltip("Непрозрачная часть картинки, px от левого нижнего угла (по ней детали прилипают друг к другу).")]
        public Rect OpaquePx;
        [Tooltip("Многоугольники столкновений подряд, px; ShapeCounts — сколько точек у каждого. Пусто — вся картинка.")]
        public Vector2[] ShapePoints;
        public int[] ShapeCounts;
        [Tooltip("Твёрдая по умолчанию (о неё разбиваются).")]
        public bool Solid;
        [Tooltip("Особая роль из оригинала: LightSprite, Glass, Bird, Helicopter или пусто.")]
        public string Kind;
        [Tooltip("Ночью всегда освещена (облака).")]
        public bool AlwaysLit;
        [Tooltip("Масштаб по умолчанию (как чаще всего в уровнях оригинала).")]
        public float Scale;
        [Tooltip("Слой по умолчанию (больше — ближе).")]
        public int Layer;
        [Tooltip("Не показывать в палитре (пустая замена картинки, которой нет; нужна, чтобы уровни оригинала открывались как есть).")]
        public bool Hidden;
        [Tooltip("Маска непрозрачности для выбора пальцем: клетки по 8 px, строки снизу вверх, 1 бит на клетку.")]
        public int MaskW, MaskH;
        public byte[] Mask;
    }

    /// <summary>
    /// Все детали редактора карт. Сейчас это картинки уровней оригинала (заглушки, создаёт импорт уровней).
    /// Карты хранят только Id деталей, поэтому картинки потом можно заменить своими с теми же Id.
    /// </summary>
    public sealed class PieceCatalog : ScriptableObject
    {
        public const int MaskCell = 8;

        public PieceDef[] Pieces = new PieceDef[0];

        [NonSerialized] Dictionary<string, int> _index;
        [NonSerialized] Dictionary<string, List<Vector2[]>> _shapes;

        public bool TryGet(string id, out PieceDef def)
        {
            if (_index == null)
            {
                _index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < Pieces.Length; i++) _index[Pieces[i].Id] = i;
            }
            if (id != null && _index.TryGetValue(id, out int k))
            {
                def = Pieces[k];
                return true;
            }
            def = default;
            return false;
        }

        /// <summary>Id детали по её картинке (для уровней оригинала → карта). null — такой детали нет.</summary>
        public string IdOf(Sprite sprite)
        {
            if (sprite == null) return null;
            foreach (var p in Pieces)
                if (p.Sprite == sprite) return p.Id;
            return null;
        }

        /// <summary>Многоугольники столкновений детали (px). Пустой список — столкновение по всей картинке.</summary>
        public IReadOnlyList<Vector2[]> Shapes(in PieceDef def)
        {
            _shapes ??= new Dictionary<string, List<Vector2[]>>(StringComparer.Ordinal);
            if (_shapes.TryGetValue(def.Id, out var cached)) return cached;
            var list = new List<Vector2[]>();
            if (def.ShapeCounts != null && def.ShapePoints != null)
            {
                int at = 0;
                foreach (int n in def.ShapeCounts)
                {
                    if (n <= 0 || at + n > def.ShapePoints.Length) break;
                    var poly = new Vector2[n];
                    Array.Copy(def.ShapePoints, at, poly, 0, n);
                    list.Add(poly);
                    at += n;
                }
            }
            _shapes[def.Id] = list;
            return list;
        }

        /// <summary>Есть ли в этом месте картинки (px от левого нижнего угла) что-то непрозрачное.</summary>
        public static bool Opaque(in PieceDef def, Vector2 px)
        {
            if (px.x < 0f || px.y < 0f || px.x >= def.SizePx.x || px.y >= def.SizePx.y) return false;
            if (def.Mask == null || def.MaskW <= 0) return true;
            int cx = Mathf.Min(def.MaskW - 1, (int)(px.x / MaskCell)), cy = Mathf.Min(def.MaskH - 1, (int)(px.y / MaskCell));
            int bit = cy * def.MaskW + cx;
            return bit / 8 < def.Mask.Length && (def.Mask[bit / 8] & (1 << (bit % 8))) != 0;
        }
    }
}
