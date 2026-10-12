using System;
using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Стикмен. Если в ArtBank есть анимации оригинала — это <see cref="CharacterRig"/>: картинки частей тела и анимации
    /// из файлов оригинала (стойка, ходьба, бег, прыжок, купол, посадка). Иначе — простой стикмен из капсул, позу
    /// которого считает код (ходьба и бег через IK ног). Смотрит туда, куда идёт: пятится от края — спиной к нему.
    /// При ударе части тела получают Rigidbody2D и шарниры — дальше честный ragdoll на физике Unity.
    /// </summary>
    public sealed class StickmanView
    {
        enum B { Torso, Head, ArmBackUp, ArmBackLow, LegBackUp, LegBackLow, FootBack, LegFrontUp, LegFrontLow, FootFront, ArmFrontUp, ArmFrontLow, Count }

        sealed class Bone
        {
            public Transform T;
            public float Length;
            public float Thickness;
            public B Parent = B.Count;
            public Rigidbody2D Body;
        }

        /// <summary>Сообщает о каждом сильном ударе ragdoll (скорость удара, м/с).</summary>
        public event Action<float> RagdollImpact;

        readonly StickmanConfig _cfg;
        readonly ArtBank _art;
        readonly SpriteKit _kit;
        readonly Transform _root;
        readonly Bone[] _bones = new Bone[(int)B.Count];
        readonly CanopyView _canopy;
        readonly CharacterRig _rig;
        Rigidbody2D _torso;
        float _spin;
        JumpPhase _phaseBefore = JumpPhase.Ready;
        SpriteRenderer _headRenderer;
        readonly List<SpriteRenderer> _backShapes = new List<SpriteRenderer>();
        bool _frontView;
        StickmanPose _pose;
        float _gaitPhase, _time, _phaseTime, _facing = 1f;
        JumpPhase _lastPhase = JumpPhase.Ready;
        bool _ragdoll;

        public Transform Root => _root;
        public bool IsRagdoll => _ragdoll;
        public CanopyView Canopy => _canopy;
        /// <summary>+1 — смотрит вправо (к краю), −1 — влево.</summary>
        public float Facing => _facing;

        /// <summary>Где сейчас середина тела (для камеры после удара).</summary>
        public Vector2 Center
        {
            get
            {
                if (_rig != null) return _torso != null ? _torso.position : (Vector2)_root.position + Vector2.up * CharacterRig.PivotHeight;
                return _ragdoll ? (Vector2)_bones[(int)B.Torso].T.position : (Vector2)_root.position + Vector2.up * (_cfg.UpperLegLength + _cfg.LowerLegLength);
            }
        }

        /// <summary>Скорость тела в ragdoll, м/с.</summary>
        public float RagdollSpeed
        {
            get
            {
                if (_rig != null) return _torso != null ? _torso.linearVelocity.magnitude : 0f;
                return _ragdoll && _bones[(int)B.Torso].Body != null ? _bones[(int)B.Torso].Body.linearVelocity.magnitude : 0f;
            }
        }

        /// <summary>Какая анимация оригинала сейчас играет (null — простой стикмен). Для автотеста.</summary>
        public CharacterClip Clip => _rig?.Clip;

        public StickmanView(Transform parent, StickmanConfig cfg, ArtBank art)
        {
            _cfg = cfg;
            _art = art;
            _kit = new SpriteKit(art.SpriteMaterial, art.White);
            if (art.HasCharacter)
            {
                // Original pictures and animations: scale the stand pose to the configured height.
                float metersPerPixel = cfg.Height / Mathf.Max(1f, art.AnimStand.Height);
                _rig = new CharacterRig(parent, art.CharacterClips, metersPerPixel, art.SpriteMaterial, SortOrder.StickmanBack);
                _root = _rig.Root;
                _rig.Play(art.AnimStand, true);
                _rig.SetPlacement(Vector2.zero, 1f, 0f);
                _rig.Apply();
                return;
            }
            _root = new GameObject("Stickman").transform;
            _root.SetParent(parent, false);

            float t = cfg.LimbThickness;
            MakeLimb(B.ArmBackUp, "ArmBackUpper", cfg.UpperArmLength, t, cfg.BackColor, SortOrder.StickmanBack, B.Torso);
            MakeLimb(B.ArmBackLow, "ArmBackLower", cfg.LowerArmLength, t, cfg.BackColor, SortOrder.StickmanBack, B.ArmBackUp);
            MakeLimb(B.LegBackUp, "LegBackUpper", cfg.UpperLegLength, t * 1.1f, cfg.BackColor, SortOrder.StickmanBack, B.Torso);
            MakeLimb(B.LegBackLow, "LegBackLower", cfg.LowerLegLength, t * 1.05f, cfg.BackColor, SortOrder.StickmanBack, B.LegBackUp);
            MakeLimb(B.FootBack, "FootBack", cfg.FootLength, t * 0.95f, cfg.BackColor, SortOrder.StickmanBack, B.LegBackLow);
            MakeLimb(B.Torso, "Torso", cfg.TorsoLength, t * 1.45f, cfg.FrontColor, SortOrder.StickmanBody, B.Count);
            MakeLimb(B.LegFrontUp, "LegFrontUpper", cfg.UpperLegLength, t * 1.1f, cfg.FrontColor, SortOrder.StickmanFront, B.Torso);
            MakeLimb(B.LegFrontLow, "LegFrontLower", cfg.LowerLegLength, t * 1.05f, cfg.FrontColor, SortOrder.StickmanFront, B.LegFrontUp);
            MakeLimb(B.FootFront, "FootFront", cfg.FootLength, t * 0.95f, cfg.FrontColor, SortOrder.StickmanFront, B.LegFrontLow);
            MakeLimb(B.ArmFrontUp, "ArmFrontUpper", cfg.UpperArmLength, t, cfg.FrontColor, SortOrder.StickmanFront + 1, B.Torso);
            MakeLimb(B.ArmFrontLow, "ArmFrontLower", cfg.LowerArmLength, t, cfg.FrontColor, SortOrder.StickmanFront + 1, B.ArmFrontUp);
            MakeHead();
            _canopy = new CanopyView(_root, art, _kit);
            _pose = StickmanPose.Stand(0f, cfg);
            ApplyPose();
        }

        public void Destroy()
        {
            if (_rig != null)
            {
                _rig.Destroy();
                return;
            }
            _canopy.Destroy();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }

        // ------------------------------------------------------------------ build

        void MakeLimb(B id, string name, float length, float thickness, Color color, int order, B parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var bone = new Bone { T = go.transform, Length = length, Thickness = thickness, Parent = parent };
            if (_art.Limb != null)
            {
                var r = _kit.Make("Shape", go.transform, _art.Limb, order, color);
                if (order == SortOrder.StickmanBack) _backShapes.Add(r);
                r.drawMode = SpriteDrawMode.Sliced;
                // Limb sprite pivot is its top centre; extend half a thickness past both joints so ends overlap.
                r.size = new Vector2(thickness, length + thickness);
                r.transform.localPosition = new Vector3(0f, thickness * 0.5f, 0f);
            }
            else
            {
                _kit.Box("Shape", go.transform, new Rect(-thickness * 0.5f, -length, thickness, length), color, order);
            }
            _bones[(int)id] = bone;
        }

        void MakeHead()
        {
            var go = new GameObject("Head");
            go.transform.SetParent(_root, false);
            float size = _cfg.HeadSize;
            var sprite = _art.Head != null ? _art.Head : _art.Circle;
            _headRenderer = _kit.Make("Shape", go.transform, sprite, SortOrder.StickmanHead, _art.Head != null ? Color.white : _cfg.FrontColor);
            float s = size / Mathf.Max(0.001f, sprite.bounds.size.y);
            _headRenderer.transform.localScale = new Vector3(s, s, 1f);
            _headRenderer.transform.localPosition = -(Vector3)(sprite.bounds.center * s);
            _bones[(int)B.Head] = new Bone { T = go.transform, Length = size, Thickness = size, Parent = B.Torso };
        }

        // ------------------------------------------------------------------ live pose

        /// <summary>Каждый кадр, пока стикмен жив: поза по фазе прыжка.</summary>
        public void UpdateAlive(JumpSession s, float dt)
        {
            if (_ragdoll) return;
            _time += dt;
            if (s.Phase != _lastPhase)
            {
                _phaseBefore = _lastPhase;
                _lastPhase = s.Phase;
                _phaseTime = 0f;
            }
            else _phaseTime += dt;

            // Walking back he turns his back to the edge; otherwise he faces the jump direction (instant flip, like the original).
            _facing = s.Phase == JumpPhase.BackingUp || s.Phase == JumpPhase.AtBackWall ? -1f : 1f;
            if (_rig != null)
            {
                UpdateRig(s, dt);
                return;
            }

            var target = TargetPose(s, dt, out float sharpness);
            float k = 1f - Mathf.Exp(-sharpness * dt);
            _pose = StickmanPose.Lerp(_pose, target, k);
            _root.position = new Vector3(s.Feet.x, s.Feet.y, 0f);
            ApplyPose();
            _canopy.UpdateAttached(s, ShoulderWorld(), _facing * _pose.Body, _time, dt);
        }

        /// <summary>Анимации оригинала по фазе прыжка + поворот всей фигуры (нырок по траектории, качание под куполом).</summary>
        void UpdateRig(JumpSession s, float dt)
        {
            float speed = Mathf.Abs(s.Velocity.x);
            float rate = 1f, targetSpin = 0f, spinSharpness = 12f, open = 1f;
            bool canopy = false;
            switch (s.Phase)
            {
                case JumpPhase.BackingUp:
                    _rig.Play(_art.AnimWalk, true);
                    rate = speed / Mathf.Max(0.1f, _cfg.WalkClipSpeed); // feet do not slide
                    break;
                case JumpPhase.Running:
                    _rig.Play(_art.AnimRun, true);
                    rate = Mathf.Clamp(speed / Mathf.Max(0.1f, _cfg.RunClipSpeed), 0.6f, _cfg.RunClipMaxRate);
                    break;
                case JumpPhase.Airborne when s.CanopyDeployed:
                    _rig.Play(_art.AnimParachute, true);
                    canopy = true;
                    open = s.Canopy;
                    // Swings under the canopy: leans against the drift, plus a slow sway.
                    targetSpin = Mathf.Clamp(-(s.Velocity.x - s.Level.Wind) * 2.5f, -16f, 16f) + Mathf.Sin(_time * 1.3f) * 4f;
                    spinSharpness = 4f;
                    break;
                case JumpPhase.Airborne:
                    // Push-off, then the whole straight body follows the flight path: first flat, then head down.
                    _rig.Play(_art.AnimJump, false);
                    if (s.CanopyCut) _rig.SkipToEnd(); // canopy cut away: straight dive again
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_phaseTime / Mathf.Max(0.05f, _art.AnimJump.Duration)));
                    targetSpin = Mathf.Lerp(0f, DiveAngle(s.Velocity), k);
                    spinSharpness = 30f;
                    break;
                case JumpPhase.Landed:
                    _rig.Play(Or(s.LandedOnTarget ? _art.AnimWins : _art.AnimLoose, _art.AnimStand), false);
                    canopy = s.CanopyDeployed;
                    break;
                case JumpPhase.TimeUp:
                    if (s.CanopyDeployed)
                    {
                        _rig.Play(_art.AnimParachute, true);
                        canopy = true;
                        open = s.Canopy;
                    }
                    else if (_phaseBefore == JumpPhase.Airborne) targetSpin = _spin; // frozen mid-dive
                    else _rig.Play(Or(_art.AnimFastLanding, _art.AnimStand), true); // hands on the head: time is up
                    break;
                default: // Ready, AtBackWall
                    _rig.Play(_art.AnimStand, true);
                    break;
            }
            _spin = Mathf.LerpAngle(_spin, targetSpin, 1f - Mathf.Exp(-spinSharpness * dt));
            _rig.Advance(dt, rate);
            _rig.SetCanopy(canopy, open);
            _rig.SetPlacement(s.Feet, _facing, _spin);
            _rig.Apply();
        }

        static CharacterClip Or(CharacterClip clip, CharacterClip fallback) => clip != null ? clip : fallback;

        /// <summary>Наклон прямой фигуры по скорости (лицом вправо): вверх-вперёд −60°, горизонтально −90°, вниз −180°.</summary>
        static float DiveAngle(Vector2 v)
        {
            if (v.sqrMagnitude < 0.01f) return 0f;
            float a = Mathf.Atan2(v.y, Mathf.Abs(v.x)) * Mathf.Rad2Deg - 90f;
            return Mathf.Clamp(a, -179f, 0f);
        }

        StickmanPose TargetPose(JumpSession s, float dt, out float sharpness)
        {
            sharpness = _cfg.PoseSharpness;
            float speed = Mathf.Abs(s.Velocity.x);
            switch (s.Phase)
            {
                case JumpPhase.BackingUp:
                case JumpPhase.Running:
                    _gaitPhase += speed * dt / StickmanPose.Stride(speed, _cfg);
                    sharpness = 30f; // locomotion is already smooth; follow it closely so feet stay planted
                    return StickmanPose.Locomotion(_gaitPhase, speed, _cfg);
                case JumpPhase.Airborne:
                    if (s.CanopyDeployed && s.Canopy > 0.35f)
                    {
                        sharpness = 6f;
                        float sway = Mathf.Clamp(-(s.Velocity.x - s.Level.Wind) * 2.5f, -16f, 16f) + Mathf.Sin(_time * 1.3f) * 4f;
                        return StickmanPose.Hang(_time, sway, _cfg);
                    }
                    if (_phaseTime < 0.3f) return StickmanPose.Takeoff(_cfg);
                    sharpness = 7f;
                    return StickmanPose.Dive(s.Velocity, _time, _cfg);
                case JumpPhase.Landed:
                    if (_phaseTime < 0.35f)
                    {
                        sharpness = 22f;
                        return StickmanPose.Crouch(Mathf.Clamp01(s.ImpactVelocity.magnitude / 6f), _cfg);
                    }
                    return s.LandedOnTarget ? StickmanPose.Cheer(_phaseTime, _cfg) : StickmanPose.Shrug(_time, _cfg);
                case JumpPhase.TimeUp:
                    return StickmanPose.Shrug(_time, _cfg);
                default:
                    _gaitPhase = 0f;
                    return StickmanPose.Stand(_time, _cfg);
            }
        }

        /// <summary>Анфас (под куполом): голова с двумя глазами, все конечности одного цвета.</summary>
        void SetFrontView(bool front)
        {
            if (front == _frontView) return;
            _frontView = front;
            var sprite = front && _art.HeadFront != null ? _art.HeadFront : _art.Head;
            if (_headRenderer != null && sprite != null) _headRenderer.sprite = sprite;
            foreach (var r in _backShapes) r.color = front ? _cfg.FrontColor : _cfg.BackColor;
        }

        Vector2 ShoulderWorld()
        {
            var torso = _bones[(int)B.Torso];
            return (Vector2)torso.T.position + StickmanPose.Dir(torso.T.eulerAngles.z) * (torso.Length - 0.05f);
        }

        /// <summary>Кинематика: углы позы → положения костей. Считаем «лицом вправо», потом зеркалим по facing.</summary>
        void ApplyPose()
        {
            var p = _pose;
            SetFrontView(p.FrontView > 0.5f);
            // The whole figure turns around the middle of the body (where the collision box centre is), not the feet.
            Vector2 pivot = new Vector2(0f, _cfg.UpperLegLength + _cfg.LowerLegLength + _cfg.AnkleHeight + _cfg.TorsoLength * 0.4f);
            Vector2 pelvis = pivot + Rotate(new Vector2(p.PelvisX, p.PelvisY) - pivot, p.Body);
            float torsoA = p.Body + p.Torso;
            Vector2 torsoEnd = pelvis + StickmanPose.Dir(torsoA) * _cfg.TorsoLength;
            Vector2 shoulder = pelvis + StickmanPose.Dir(torsoA) * (_cfg.TorsoLength - 0.05f);
            Set(B.Torso, pelvis, torsoA);
            float headA = torsoA + p.Head;
            // The big head sits right on the shoulders, like the original (no visible neck).
            Set(B.Head, torsoEnd + StickmanPose.Dir(headA) * (_cfg.HeadSize * 0.42f), headA - 180f + p.HeadLook);

            Chain(B.ArmFrontUp, B.ArmFrontLow, shoulder, p.Body + p.ArmFront, p.ElbowFront);
            Chain(B.ArmBackUp, B.ArmBackLow, shoulder, p.Body + p.ArmBack, p.ElbowBack);
            Vector2 ankleF = Chain(B.LegFrontUp, B.LegFrontLow, pelvis, p.Body + p.LegFront, p.KneeFront);
            Vector2 ankleB = Chain(B.LegBackUp, B.LegBackLow, pelvis, p.Body + p.LegBack, p.KneeBack);
            Set(B.FootFront, ankleF, p.Body + p.FootFront);
            Set(B.FootBack, ankleB, p.Body + p.FootBack);
            if (_headRenderer != null) _headRenderer.flipX = _facing < 0f;
        }

        Vector2 Chain(B upper, B lower, Vector2 start, float angle, float bend)
        {
            Set(upper, start, angle);
            Vector2 joint = start + StickmanPose.Dir(angle) * _bones[(int)upper].Length;
            Set(lower, joint, angle + bend);
            return joint + StickmanPose.Dir(angle + bend) * _bones[(int)lower].Length;
        }

        /// <summary>local — «лицом вправо» относительно точки между ступнями; зеркалим по facing.</summary>
        void Set(B id, Vector2 local, float angle)
        {
            var t = _bones[(int)id].T;
            Vector2 root = _root.position;
            t.position = new Vector3(root.x + local.x * _facing, root.y + local.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle * _facing);
        }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ------------------------------------------------------------------ ragdoll

        /// <summary>Тап под куполом: купол отрывается и улетает, стикмен снова в свободном падении.</summary>
        public void CutCanopy(Vector2 velocity)
        {
            if (_ragdoll) return;
            if (_rig != null) _rig.DetachCanopy(_root.parent, velocity);
            else _canopy.Detach(velocity);
        }

        /// <summary>Дальше тело падает само. velocity — скорость в момент удара/срыва.</summary>
        public void BecomeRagdoll(Vector2 velocity, int seed)
        {
            if (_ragdoll) return;
            _ragdoll = true;
            if (_rig != null)
            {
                _rig.DetachCanopy(_root.parent, velocity);
                _torso = _rig.BecomeRagdoll(_root.parent, velocity, seed, _cfg.Mass, _cfg.CrashSpin, speed => RagdollImpact?.Invoke(speed));
                return;
            }
            _canopy.Detach(velocity);
            var rng = new System.Random(seed);
            var mat = new PhysicsMaterial2D("Stickman") { friction = 0.6f, bounciness = 0.2f };
            var colliders = new List<Collider2D>();
            float massUnit = _cfg.Mass / 12f;

            for (int i = 0; i < (int)B.Count; i++)
            {
                var bone = _bones[i];
                var rb = bone.T.gameObject.AddComponent<Rigidbody2D>();
                rb.mass = massUnit * (i == (int)B.Torso ? 3f : i == (int)B.Head ? 1f : 0.7f);
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.4f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                Collider2D col;
                if (i == (int)B.Head)
                {
                    var c = bone.T.gameObject.AddComponent<CircleCollider2D>();
                    c.radius = _cfg.HeadSize * 0.45f;
                    col = c;
                }
                else
                {
                    var c = bone.T.gameObject.AddComponent<CapsuleCollider2D>();
                    c.direction = CapsuleDirection2D.Vertical;
                    c.size = new Vector2(bone.Thickness * 1.6f, bone.Length + bone.Thickness);
                    c.offset = new Vector2(0f, -bone.Length * 0.5f);
                    col = c;
                }
                col.sharedMaterial = mat;
                colliders.Add(col);
                bone.Body = rb;
                var reporter = bone.T.gameObject.AddComponent<RagdollImpactReporter>();
                reporter.Impact += speed => RagdollImpact?.Invoke(speed);
            }

            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++)
                    Physics2D.IgnoreCollision(colliders[i], colliders[j]);

            for (int i = 0; i < (int)B.Count; i++)
            {
                var bone = _bones[i];
                if (bone.Parent != B.Count)
                {
                    var joint = bone.T.gameObject.AddComponent<HingeJoint2D>();
                    joint.connectedBody = _bones[(int)bone.Parent].Body;
                    joint.autoConfigureConnectedAnchor = true;
                    // Limbs hinge at their own origin (the joint); the head hinges at the neck, below its centre.
                    joint.anchor = i == (int)B.Head ? new Vector2(0f, -_cfg.HeadSize * 0.5f) : Vector2.zero;
                    joint.enableCollision = false;
                    joint.useLimits = true;
                    // Unity measures a hinge as (parent − child) relative to the pose at creation; mirrored when facing left.
                    float current = Mathf.DeltaAngle(bone.T.eulerAngles.z, _bones[(int)bone.Parent].T.eulerAngles.z);
                    joint.limits = Limits((B)i, current, _facing);
                }
                float jitter = ((float)rng.NextDouble() - 0.5f) * 2f;
                bone.Body.linearVelocity = velocity + new Vector2(jitter, (float)rng.NextDouble()) * 0.8f;
                bone.Body.angularVelocity = jitter * _cfg.CrashSpin * 40f;
            }
        }

        /// <param name="current">Current (parent − child) angle, degrees.</param>
        static JointAngleLimits2D Limits(B id, float current, float facing)
        {
            switch (id)
            {
                case B.LegFrontLow:
                case B.LegBackLow: // knee: straight (0) to folded backwards (+150 facing right)
                    return Absolute(-5f, 150f, current, facing);
                case B.ArmFrontLow:
                case B.ArmBackLow: // elbow: straight (0) to folded forwards (−150 facing right)
                    return Absolute(-150f, 5f, current, facing);
                case B.FootFront:
                case B.FootBack:
                    return new JointAngleLimits2D { min = -35f, max = 35f };
                case B.Head:
                    return new JointAngleLimits2D { min = -45f, max = 45f };
                case B.LegFrontUp:
                case B.LegBackUp:
                    return new JointAngleLimits2D { min = -100f, max = 100f };
                default:
                    return new JointAngleLimits2D { min = -170f, max = 170f };
            }
        }

        static JointAngleLimits2D Absolute(float min, float max, float current, float facing)
        {
            if (facing < 0f)
            {
                float t = min;
                min = -max;
                max = -t;
            }
            return new JointAngleLimits2D { min = Mathf.Min(min - current, 0f), max = Mathf.Max(max - current, 0f) };
        }

        /// <summary>Отделившийся купол продолжает падать сам.</summary>
        public void UpdateDetached(float dt)
        {
            if (_rig != null) _rig.UpdateDetached(dt);
            else _canopy.UpdateDetached(dt);
        }
    }

    /// <summary>Шлёт событие, когда часть ragdoll сильно обо что-то ударилась.</summary>
    public sealed class RagdollImpactReporter : MonoBehaviour
    {
        public event Action<float> Impact;

        void OnCollisionEnter2D(Collision2D collision)
        {
            Impact?.Invoke(collision.relativeVelocity.magnitude);
        }
    }
}
