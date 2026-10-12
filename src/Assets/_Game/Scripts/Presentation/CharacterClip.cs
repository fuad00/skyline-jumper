using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Скелетная анимация персонажа в формате редактора оригинала (ассет создаёт импорт заглушек из reference/decoded).
    /// Кости — дерево. У каждой кости ключевые кадры: смещение от опоры родителя (px, ось Y вверх), поворот (градусы,
    /// против часовой), масштаб, точка опоры картинки (px от левого нижнего угла) и сама картинка. Между ключами — линейно.
    /// Внутри скелета 1 единица = 1 px картинки; в метры переводит <see cref="CharacterRig"/>.
    /// </summary>
    public sealed class CharacterClip : ScriptableObject
    {
        [Serializable]
        public struct Key
        {
            public int Frame;
            public Vector2 Position;
            public float Rotation;
            public Vector2 Scale;
            public Vector2 Anchor;
            public Sprite Sprite;
        }

        [Serializable]
        public sealed class Bone
        {
            public string Name;
            [Tooltip("Номер кости-родителя в этом списке, −1 — корень.")]
            public int Parent = -1;
            [Tooltip("Порядок среди соседей: меньше нуля — под родителем, иначе над ним.")]
            public float Z;
            public Key[] Keys = new Key[0];
        }

        [Tooltip("Длительность кадра, с.")]
        public float FrameTime = 0.033f;
        [Tooltip("Номер последнего кадра. Цикл длится LastFrame × FrameTime (последний кадр = первый следующего круга).")]
        public int LastFrame;
        [Tooltip("Земля: самая низкая точка ног за всю анимацию, px от корня скелета.")]
        public float GroundY;
        [Tooltip("Рост: от земли до макушки в первом кадре, px.")]
        public float Height;
        public Bone[] Bones = new Bone[0];

        public float Duration => Mathf.Max(1, LastFrame) * FrameTime;

        public int IndexOf(string name)
        {
            for (int i = 0; i < Bones.Length; i++)
                if (Bones[i].Name == name) return i;
            return -1;
        }

        /// <summary>Дробный кадр по времени: по кругу или с остановкой на последнем.</summary>
        public float FrameAt(float time, bool loop)
        {
            if (LastFrame <= 0 || FrameTime <= 0f) return 0f;
            float f = time / FrameTime;
            return loop ? Mathf.Repeat(f, LastFrame) : Mathf.Clamp(f, 0f, LastFrame);
        }

        /// <summary>
        /// Ключ в дробном кадре: положение, поворот и масштаб — линейно между соседними ключами,
        /// картинка и опора — от левого ключа. false — у кости нет ключей (в этой анимации её не видно).
        /// </summary>
        public static bool Sample(Key[] keys, float frame, out Key result)
        {
            result = default;
            if (keys == null || keys.Length == 0) return false;
            if (frame <= keys[0].Frame || keys.Length == 1)
            {
                result = keys[0];
                return true;
            }
            for (int i = 0; i < keys.Length - 1; i++)
            {
                var a = keys[i];
                var b = keys[i + 1];
                if (frame > b.Frame) continue;
                float t = b.Frame == a.Frame ? 0f : (frame - a.Frame) / (b.Frame - a.Frame);
                result = a;
                result.Position = Vector2.LerpUnclamped(a.Position, b.Position, t);
                result.Rotation = Mathf.LerpUnclamped(a.Rotation, b.Rotation, t); // stored unwrapped (e.g. 356 → 383)
                result.Scale = Vector2.LerpUnclamped(a.Scale, b.Scale, t);
                return true;
            }
            result = keys[keys.Length - 1];
            return true;
        }

        /// <summary>
        /// Порядок отрисовки как в оригинале (cocos2d): сначала дети с z &lt; 0, потом сама кость, потом дети с z ≥ 0;
        /// соседи — по возрастанию z, при равных — как в файле.
        /// </summary>
        public int[] DrawOrder()
        {
            var children = new List<int>[Bones.Length];
            var roots = new List<int>();
            for (int i = 0; i < Bones.Length; i++) children[i] = new List<int>();
            for (int i = 0; i < Bones.Length; i++)
            {
                int p = Bones[i].Parent;
                if (p >= 0 && p < Bones.Length) children[p].Add(i);
                else roots.Add(i);
            }
            var order = new List<int>(Bones.Length);
            void Visit(int i)
            {
                var kids = children[i].OrderBy(c => Bones[c].Z).ToList(); // OrderBy is stable
                foreach (int c in kids) if (Bones[c].Z < 0f) Visit(c);
                order.Add(i);
                foreach (int c in kids) if (Bones[c].Z >= 0f) Visit(c);
            }
            foreach (int r in roots.OrderBy(r => Bones[r].Z)) Visit(r);
            return order.ToArray();
        }

        /// <summary>
        /// Поза в кадре: матрица каждой кости в пространстве скелета (px). Нужна импорту (где земля, какой рост)
        /// и тестам; в игре те же числа задают Transform-ы костей.
        /// </summary>
        public Matrix4x4[] PoseMatrices(float frame, out bool[] visible)
        {
            var result = new Matrix4x4[Bones.Length];
            visible = new bool[Bones.Length];
            for (int i = 0; i < Bones.Length; i++) // parents always come before children (import order)
            {
                var b = Bones[i];
                var parent = b.Parent >= 0 ? result[b.Parent] : Matrix4x4.identity;
                bool parentVisible = b.Parent < 0 || visible[b.Parent];
                if (!Sample(b.Keys, frame, out var k) || !parentVisible)
                {
                    result[i] = parent;
                    continue;
                }
                visible[i] = true;
                result[i] = parent * Local(k);
            }
            return result;
        }

        public static Matrix4x4 Local(Key k) =>
            Matrix4x4.TRS(new Vector3(k.Position.x, k.Position.y, 0f), Quaternion.Euler(0f, 0f, k.Rotation),
                new Vector3(k.Scale.x == 0f ? 1f : k.Scale.x, k.Scale.y == 0f ? 1f : k.Scale.y, 1f));

        /// <summary>Углы картинки кости в пространстве скелета (px): картинка лежит опорой в начале кости.</summary>
        public static Vector2[] ImageCorners(Matrix4x4 bone, Key k)
        {
            if (k.Sprite == null) return Array.Empty<Vector2>();
            var size = k.Sprite.rect.size;
            var corners = new Vector2[4];
            var local = new[] { Vector2.zero, new Vector2(size.x, 0f), new Vector2(0f, size.y), size };
            for (int c = 0; c < 4; c++) corners[c] = bone.MultiplyPoint3x4(local[c] - k.Anchor);
            return corners;
        }

        /// <summary>Кость купола (сам купол, секции, стропы, держатель) — их показываем и прячем отдельно от тела.</summary>
        public static bool IsCanopyBone(string name) =>
            name == "para" || name == "holder" || name == "strokeLeft" || name == "strokeRight" || name == "parachute" ||
            (name.Length == 1 && char.IsDigit(name[0]));
    }
}
