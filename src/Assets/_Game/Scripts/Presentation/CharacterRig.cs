using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Персонаж из картинок частей тела, который проигрывает анимации <see cref="CharacterClip"/> (как в оригинале).
    /// Иерархия: Stickman (стоит в точке между ступнями, зеркалится по направлению взгляда) → Spin (поворот всей
    /// фигуры вокруг середины тела: нырок, качание под куполом) → Skeleton (масштаб px → м) → кости.
    /// При ударе части тела получают Rigidbody2D и шарниры — дальше ragdoll; купол может оторваться и улететь.
    /// </summary>
    public sealed class CharacterRig
    {
        /// <summary>Высота точки, вокруг которой поворачивается фигура (середина тела), м.</summary>
        public const float PivotHeight = 0.9f;

        const float LimbThickness = 0.09f, TorsoThickness = 0.13f;
        static readonly string[] PhysicsBones =
            { "Body0", "head", "upperArmF", "lowerArmF", "upperArmB", "lowerArmB", "upperLegF", "lowerLegF", "upperLegB", "lowerLegB" };

        sealed class Part
        {
            public string Name;
            public Transform T;        // the bone; its origin is the joint with the parent
            public Transform Image;    // sprite holder, shifted by −anchor
            public SpriteRenderer R;
            public Part Parent;
            public bool Canopy;
            public int ClipIndex = -1; // bone index in the current clip, −1 = not in it
            public bool Detached;
        }

        readonly Transform _root, _spin, _skeleton;
        readonly Dictionary<string, Part> _parts = new Dictionary<string, Part>();
        readonly List<Part> _list = new List<Part>();
        readonly float _mpp;
        readonly int _sortBase;
        CharacterClip _clip;
        float _time;
        bool _loop = true, _canopyVisible, _ragdoll;
        float _canopyOpen = 1f;

        Transform _looseCanopy, _ragdollRoot;
        SpriteRenderer[] _looseRenderers;
        Vector2 _looseVelocity;
        float _looseAge, _looseSpin;

        public Transform Root => _root;
        public CharacterClip Clip => _clip;
        public float Time => _time;
        /// <summary>Анимация без повтора доиграла до конца.</summary>
        public bool Finished => _clip != null && !_loop && _time >= _clip.Duration;

        /// <param name="metersPerPixel">Сколько метров в одном пикселе картинок (рост / рост в px).</param>
        public CharacterRig(Transform parent, IEnumerable<CharacterClip> clips, float metersPerPixel, Material material, int sortBase)
        {
            _mpp = metersPerPixel;
            _sortBase = sortBase;
            _root = new GameObject("Stickman").transform;
            _root.SetParent(parent, false);
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_root, false);
            _spin.localPosition = new Vector3(0f, PivotHeight, 0f);
            _skeleton = new GameObject("Skeleton").transform;
            _skeleton.SetParent(_spin, false);
            _skeleton.localScale = new Vector3(metersPerPixel, metersPerPixel, 1f);

            foreach (var clip in clips)
            {
                if (clip == null) continue;
                for (int i = 0; i < clip.Bones.Length; i++) Ensure(clip, i, material);
            }
        }

        Part Ensure(CharacterClip clip, int index, Material material)
        {
            var bone = clip.Bones[index];
            if (_parts.TryGetValue(bone.Name, out var existing)) return existing;
            var parent = bone.Parent >= 0 ? Ensure(clip, bone.Parent, material) : null;
            var go = new GameObject(bone.Name);
            go.transform.SetParent(parent != null ? parent.T : _skeleton, false);
            var img = new GameObject("Image");
            img.transform.SetParent(go.transform, false);
            var r = img.AddComponent<SpriteRenderer>();
            if (material != null) r.sharedMaterial = material;
            var part = new Part
            {
                Name = bone.Name, T = go.transform, Image = img.transform, R = r, Parent = parent,
                Canopy = CharacterClip.IsCanopyBone(bone.Name) || (parent != null && parent.Canopy),
            };
            _parts[bone.Name] = part;
            _list.Add(part);
            go.SetActive(false);
            return part;
        }

        public void Destroy()
        {
            if (_looseCanopy != null) UnityEngine.Object.Destroy(_looseCanopy.gameObject);
            if (_ragdollRoot != null) UnityEngine.Object.Destroy(_ragdollRoot.gameObject);
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }

        // ------------------------------------------------------------------ playback

        /// <summary>Включить анимацию. Та же самая продолжает играть, если не просят начать заново.</summary>
        public void Play(CharacterClip clip, bool loop, bool restart = false)
        {
            if (clip == null || _ragdoll) return;
            _loop = loop;
            if (clip == _clip && !restart) return;
            _clip = clip;
            _time = 0f;
            foreach (var p in _list) p.ClipIndex = -1;
            for (int i = 0; i < clip.Bones.Length; i++)
                if (_parts.TryGetValue(clip.Bones[i].Name, out var p)) p.ClipIndex = i;
            var order = clip.DrawOrder();
            for (int n = 0; n < order.Length; n++)
                if (_parts.TryGetValue(clip.Bones[order[n]].Name, out var p)) p.R.sortingOrder = _sortBase + n;
            // Feet on the ground: the clip's lowest leg point lands on the root.
            _skeleton.localPosition = new Vector3(0f, -PivotHeight - clip.GroundY * _mpp, 0f);
        }

        /// <summary>Сразу последний кадр (оторвал купол — снова прямой, как в конце прыжка).</summary>
        public void SkipToEnd()
        {
            if (_clip != null) _time = _clip.Duration;
        }

        /// <param name="rate">1 — как задумано в оригинале; 2 — вдвое быстрее.</param>
        public void Advance(float dt, float rate = 1f) => _time += dt * Mathf.Max(0f, rate);

        public void SetPlacement(Vector2 feet, float facing, float spinDegrees)
        {
            _root.position = new Vector3(feet.x, feet.y, 0f);
            _root.localScale = new Vector3(facing < 0f ? -1f : 1f, 1f, 1f);
            _spin.localRotation = Quaternion.Euler(0f, 0f, spinDegrees);
        }

        /// <summary>Купол (если он есть в анимации): показан ли и насколько раскрыт (0 — комок у держателя, 1 — дуга).</summary>
        public void SetCanopy(bool visible, float open)
        {
            _canopyVisible = visible;
            _canopyOpen = Mathf.Clamp01(open);
        }

        /// <summary>Расставить кости по текущему кадру.</summary>
        public void Apply()
        {
            if (_clip == null || _ragdoll) return;
            float frame = _clip.FrameAt(_time, _loop);
            foreach (var p in _list)
            {
                if (p.Detached) continue;
                CharacterClip.Key k = default;
                bool on = p.ClipIndex >= 0 && !(p.Canopy && !_canopyVisible) && CharacterClip.Sample(_clip.Bones[p.ClipIndex].Keys, frame, out k);
                if (p.T.gameObject.activeSelf != on) p.T.gameObject.SetActive(on);
                if (!on) continue;
                p.T.localPosition = new Vector3(k.Position.x, k.Position.y, 0f);
                p.T.localRotation = Quaternion.Euler(0f, 0f, k.Rotation);
                p.T.localScale = new Vector3(k.Scale.x == 0f ? 1f : k.Scale.x, k.Scale.y == 0f ? 1f : k.Scale.y, 1f);
                p.Image.localPosition = new Vector3(-k.Anchor.x, -k.Anchor.y, 0f);
                if (p.R.sprite != k.Sprite) p.R.sprite = k.Sprite;
            }
            UnfoldCanopy();
        }

        /// <summary>Раскрытие: купол со стропами растёт из держателя над головой — сначала вверх, потом вширь.</summary>
        void UnfoldCanopy()
        {
            if (_canopyOpen >= 1f || !_parts.TryGetValue("para", out var para) || para.Detached || !para.T.gameObject.activeSelf) return;
            float e = _canopyOpen * _canopyOpen * (3f - 2f * _canopyOpen);
            float sx = Mathf.Lerp(0.08f, 1f, e), sy = Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(e));
            var s = para.T.localScale;
            para.T.localScale = new Vector3(s.x * sx, s.y * sy, 1f);
            if (_parts.TryGetValue("holder", out var holder) && holder.T.gameObject.activeSelf)
            {
                var h = holder.T.localScale;
                holder.T.localScale = new Vector3(h.x / sx, h.y / sy, 1f); // the holder keeps its size
            }
        }

        // ------------------------------------------------------------------ canopy torn off

        /// <summary>Купол отрывается и дальше летит сам: медленно падает, сносится и тает.</summary>
        public void DetachCanopy(Transform world, Vector2 velocity)
        {
            if (!_parts.TryGetValue("para", out var para) || para.Detached || !para.T.gameObject.activeInHierarchy) return;
            foreach (var p in _list) if (p.Canopy) p.Detached = true;
            _looseCanopy = para.T;
            _looseCanopy.SetParent(world, true);
            _looseRenderers = _looseCanopy.GetComponentsInChildren<SpriteRenderer>();
            _looseVelocity = new Vector2(velocity.x * 0.4f, Mathf.Max(velocity.y * 0.2f, 3f)); // pulled up and away from him
            _looseSpin = Mathf.Sign(velocity.x == 0f ? 1f : velocity.x) * -12f;
            _looseAge = 0f;
        }

        public void UpdateDetached(float dt)
        {
            if (_looseCanopy == null || !_looseCanopy.gameObject.activeSelf) return;
            _looseAge += dt;
            _looseVelocity = Vector2.MoveTowards(_looseVelocity, new Vector2(_looseVelocity.x * 0.98f, -1.4f), dt * 3f);
            _looseCanopy.position += (Vector3)(_looseVelocity * dt);
            _looseCanopy.Rotate(0f, 0f, _looseSpin * dt);
            float alpha = 1f - Mathf.Clamp01((_looseAge - 1.2f) / 1.6f);
            foreach (var r in _looseRenderers)
            {
                var c = r.color;
                c.a = alpha;
                r.color = c;
            }
            if (alpha <= 0f) _looseCanopy.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ ragdoll

        /// <summary>
        /// Части тела становятся физическими: капсула вдоль каждой кости (от сустава к дальнему концу картинки),
        /// голова — круг, шарниры в суставах. Колени и локти гнутся только в свою сторону.
        /// Возвращает тело-туловище (за ним следит камера).
        /// </summary>
        public Rigidbody2D BecomeRagdoll(Transform world, Vector2 velocity, int seed, float totalMass, float spin, Action<float> onImpact)
        {
            if (_ragdoll) return null;
            _ragdoll = true;
            float facing = _root.localScale.x < 0f ? -1f : 1f;
            var rng = new System.Random(seed);
            var material = new PhysicsMaterial2D("Stickman") { friction = 0.6f, bounciness = 0.2f };
            _ragdollRoot = new GameObject("Stickman Ragdoll").transform;
            _ragdollRoot.SetParent(world, false);
            var bodies = new Dictionary<Part, Rigidbody2D>();
            var dirs = new Dictionary<Part, Vector2>();
            var colliders = new List<Collider2D>();
            float unit = totalMass / 12f;

            foreach (string name in PhysicsBones)
            {
                if (!_parts.TryGetValue(name, out var part) || !part.T.gameObject.activeInHierarchy || part.R.sprite == null) continue;
                Vector2 joint = part.T.position;
                var corners = WorldCorners(part);
                var go = new GameObject("Ragdoll " + name);
                go.transform.SetParent(_ragdollRoot, false);
                go.transform.position = joint;
                Vector2 dir;
                Collider2D col;
                if (name == "head")
                {
                    dir = part.T.TransformDirection(Vector3.up);
                    go.transform.rotation = Quaternion.Euler(0f, 0f, Angle(dir));
                    var c = go.AddComponent<CircleCollider2D>();
                    Vector2 centre = (corners[0] + corners[3]) * 0.5f;
                    c.offset = go.transform.InverseTransformPoint(centre);
                    var size = part.R.sprite.rect.size * _mpp;
                    c.radius = Mathf.Min(size.x, size.y) * 0.45f;
                    col = c;
                }
                else
                {
                    Vector2 from = joint, to = Farthest(corners, joint);
                    float thick = LimbThickness;
                    if (name == "Body0")
                    {
                        // The torso's own origin is mid-body: run its capsule from the hips to the neck.
                        from = Average(Pos("upperLegF"), Pos("upperLegB"), corners[0]);
                        to = Pos("neck") ?? Farthest(corners, from);
                        thick = TorsoThickness;
                    }
                    dir = (to - from).sqrMagnitude > 1e-6f ? (to - from).normalized : Vector2.up;
                    go.transform.rotation = Quaternion.Euler(0f, 0f, Angle(dir));
                    var c = go.AddComponent<CapsuleCollider2D>();
                    c.direction = CapsuleDirection2D.Vertical;
                    float length = Vector2.Distance(from, to);
                    c.size = new Vector2(thick, length + thick);
                    c.offset = go.transform.InverseTransformPoint((from + to) * 0.5f);
                    col = c;
                }
                col.sharedMaterial = material;
                colliders.Add(col);
                var rb = go.AddComponent<Rigidbody2D>();
                rb.mass = unit * (name == "Body0" ? 3f : name == "head" ? 1.2f : 0.7f);
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.4f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var reporter = go.AddComponent<RagdollImpactReporter>();
                if (onImpact != null) reporter.Impact += onImpact;
                bodies[part] = rb;
                dirs[part] = dir;
            }
            if (!_parts.TryGetValue("Body0", out var torsoPart) || !bodies.TryGetValue(torsoPart, out var torso)) return null;

            // Every visible picture rides on the nearest physical ancestor (neck → torso, hands → forearms).
            foreach (var p in _list)
            {
                if (p.Canopy || !p.T.gameObject.activeInHierarchy || p.R.sprite == null) continue;
                var owner = Owner(p, bodies) ?? torso;
                p.Image.SetParent(owner.transform, true);
            }

            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++)
                    Physics2D.IgnoreCollision(colliders[i], colliders[j]);

            foreach (var kv in bodies)
            {
                var part = kv.Key;
                var rb = kv.Value;
                if (part != torsoPart)
                {
                    var parentRb = Owner(part.Parent, bodies) ?? torso;
                    var parentPart = PartOf(parentRb, bodies);
                    var joint = rb.gameObject.AddComponent<HingeJoint2D>();
                    joint.connectedBody = parentRb;
                    joint.autoConfigureConnectedAnchor = true;
                    joint.anchor = Vector2.zero; // physics object origin = the joint
                    joint.enableCollision = false;
                    joint.useLimits = true;
                    float bend = Vector2.SignedAngle(parentPart != null ? dirs[parentPart] : Vector2.up, dirs[part]);
                    joint.limits = Limits(part.Name, bend, facing);
                }
                float jitter = ((float)rng.NextDouble() - 0.5f) * 2f;
                rb.linearVelocity = velocity + new Vector2(jitter, (float)rng.NextDouble()) * 0.8f;
                rb.angularVelocity = jitter * spin * 40f;
            }
            _spin.gameObject.SetActive(false); // what is left there is only the hidden skeleton
            return torso;
        }

        /// <summary>
        /// Пределы шарнира. Unity считает угол как (родитель − ребёнок) от позы в момент создания,
        /// а изгиб «как у человека» удобнее задавать углом от кости-родителя к кости-ребёнку (против часовой, лицом вправо).
        /// </summary>
        static JointAngleLimits2D Limits(string name, float bendNow, float facing)
        {
            float min, max;
            switch (name)
            {
                case "lowerLegF":
                case "lowerLegB": // knee: the shin folds backwards
                    min = -150f; max = 5f;
                    break;
                case "lowerArmF":
                case "lowerArmB": // elbow: the forearm folds forwards
                    min = -5f; max = 150f;
                    break;
                case "head":
                    return new JointAngleLimits2D { min = -40f, max = 40f };
                case "upperLegF":
                case "upperLegB":
                    return new JointAngleLimits2D { min = -100f, max = 100f };
                default:
                    return new JointAngleLimits2D { min = -160f, max = 160f };
            }
            if (facing < 0f)
            {
                float t = min;
                min = -max;
                max = -t;
            }
            return new JointAngleLimits2D { min = Mathf.Min(bendNow - max, -1f), max = Mathf.Max(bendNow - min, 1f) };
        }

        Vector2[] WorldCorners(Part p)
        {
            var size = p.R.sprite.rect.size; // sprites are imported at 1 px per unit with the pivot bottom-left
            return new[]
            {
                (Vector2)p.Image.TransformPoint(Vector3.zero), (Vector2)p.Image.TransformPoint(new Vector3(size.x, 0f, 0f)),
                (Vector2)p.Image.TransformPoint(new Vector3(0f, size.y, 0f)), (Vector2)p.Image.TransformPoint(new Vector3(size.x, size.y, 0f)),
            };
        }

        Vector2? Pos(string bone) =>
            _parts.TryGetValue(bone, out var p) && p.T.gameObject.activeInHierarchy ? (Vector2)p.T.position : (Vector2?)null;

        static Vector2 Average(Vector2? a, Vector2? b, Vector2 fallback)
        {
            if (a.HasValue && b.HasValue) return (a.Value + b.Value) * 0.5f;
            return a ?? b ?? fallback;
        }

        static Vector2 Farthest(Vector2[] points, Vector2 from)
        {
            Vector2 best = from;
            float bestD = -1f;
            foreach (var p in points)
            {
                float d = (p - from).sqrMagnitude;
                if (d > bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>Поворот объекта, у которого локальная ось Y смотрит вдоль dir.</summary>
        static float Angle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

        static Rigidbody2D Owner(Part p, Dictionary<Part, Rigidbody2D> bodies)
        {
            for (; p != null; p = p.Parent)
                if (bodies.TryGetValue(p, out var rb)) return rb;
            return null;
        }

        static Part PartOf(Rigidbody2D rb, Dictionary<Part, Rigidbody2D> bodies)
        {
            foreach (var kv in bodies)
                if (kv.Value == rb) return kv.Key;
            return null;
        }
    }
}
