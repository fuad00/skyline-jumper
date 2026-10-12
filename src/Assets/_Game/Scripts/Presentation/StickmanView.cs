using System;
using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Core;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Стикмен из капсул (своя графика) с головой-спрайтом. Пока жив — позу считает код (кинематика по углам).
    /// При ударе каждая кость получает Rigidbody2D и шарнир — дальше честный ragdoll на физике Unity.
    /// Все кости лежат плоско под одним корнем, поэтому превращение в ragdoll — это просто «добавить физику».
    /// </summary>
    public sealed class StickmanView
    {
        enum B { Torso, Head, ArmBackUp, ArmBackLow, LegBackUp, LegBackLow, LegFrontUp, LegFrontLow, ArmFrontUp, ArmFrontLow, Count }

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
        StickmanPose _pose = StickmanPose.Stand;
        float _stridePhase, _time;
        bool _ragdoll;

        public Transform Root => _root;
        public bool IsRagdoll => _ragdoll;
        public CanopyView Canopy => _canopy;

        /// <summary>Где сейчас середина тела (для камеры после удара).</summary>
        public Vector2 Center => _ragdoll ? (Vector2)_bones[(int)B.Torso].T.position : (Vector2)_root.position + Vector2.up * (_cfg.UpperLegLength + _cfg.LowerLegLength);

        /// <summary>Скорость тела в ragdoll, м/с.</summary>
        public float RagdollSpeed => _ragdoll && _bones[(int)B.Torso].Body != null ? _bones[(int)B.Torso].Body.linearVelocity.magnitude : 0f;

        public StickmanView(Transform parent, StickmanConfig cfg, ArtBank art)
        {
            _cfg = cfg;
            _art = art;
            _kit = new SpriteKit(art.SpriteMaterial, art.White);
            _root = new GameObject("Stickman").transform;
            _root.SetParent(parent, false);

            float t = cfg.LimbThickness;
            MakeLimb(B.ArmBackUp, "ArmBackUpper", cfg.UpperArmLength, t, cfg.BackColor, SortOrder.StickmanBack, B.Torso);
            MakeLimb(B.ArmBackLow, "ArmBackLower", cfg.LowerArmLength, t, cfg.BackColor, SortOrder.StickmanBack, B.ArmBackUp);
            MakeLimb(B.LegBackUp, "LegBackUpper", cfg.UpperLegLength, t * 1.1f, cfg.BackColor, SortOrder.StickmanBack, B.Torso);
            MakeLimb(B.LegBackLow, "LegBackLower", cfg.LowerLegLength, t * 1.1f, cfg.BackColor, SortOrder.StickmanBack, B.LegBackUp);
            MakeLimb(B.Torso, "Torso", cfg.TorsoLength, t * 1.35f, cfg.FrontColor, SortOrder.StickmanBody, B.Count);
            MakeLimb(B.LegFrontUp, "LegFrontUpper", cfg.UpperLegLength, t * 1.1f, cfg.FrontColor, SortOrder.StickmanFront, B.Torso);
            MakeLimb(B.LegFrontLow, "LegFrontLower", cfg.LowerLegLength, t * 1.1f, cfg.FrontColor, SortOrder.StickmanFront, B.LegFrontUp);
            MakeLimb(B.ArmFrontUp, "ArmFrontUpper", cfg.UpperArmLength, t, cfg.FrontColor, SortOrder.StickmanFront, B.Torso);
            MakeLimb(B.ArmFrontLow, "ArmFrontLower", cfg.LowerArmLength, t, cfg.FrontColor, SortOrder.StickmanFront, B.ArmFrontUp);
            MakeHead();
            _canopy = new CanopyView(_root, art, _kit);
            ApplyPose(Vector2.zero);
        }

        public void Destroy()
        {
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
            var r = _kit.Make("Shape", go.transform, sprite, SortOrder.StickmanHead, _art.Head != null ? Color.white : _cfg.FrontColor);
            float s = size / Mathf.Max(0.001f, sprite.bounds.size.y);
            r.transform.localScale = new Vector3(s, s, 1f);
            r.transform.localPosition = -(Vector3)(sprite.bounds.center * s);
            _bones[(int)B.Head] = new Bone { T = go.transform, Length = size, Thickness = size, Parent = B.Torso };
        }

        // ------------------------------------------------------------------ live pose

        /// <summary>Каждый кадр, пока стикмен жив: поза по фазе прыжка.</summary>
        public void UpdateAlive(JumpSession s, float dt)
        {
            if (_ragdoll) return;
            _time += dt;
            var target = TargetPose(s, dt);
            float k = 1f - Mathf.Exp(-_cfg.PoseSharpness * dt);
            _pose = StickmanPose.Lerp(_pose, target, k);
            _root.position = new Vector3(s.Feet.x, s.Feet.y, 0f);
            ApplyPose(s.Velocity);
            _canopy.UpdateAttached(s, ShoulderWorld(), _pose.Body, _time, dt);
        }

        StickmanPose TargetPose(JumpSession s, float dt)
        {
            switch (s.Phase)
            {
                case JumpPhase.BackingUp:
                    _stridePhase -= Mathf.Abs(s.Velocity.x) * dt / Mathf.Max(0.1f, _cfg.StrideLength * 0.6f) * Mathf.PI * 2f;
                    return StickmanPose.Stride(_stridePhase, 0.45f, -4f);
                case JumpPhase.Running:
                    _stridePhase += Mathf.Abs(s.Velocity.x) * dt / Mathf.Max(0.1f, _cfg.StrideLength) * Mathf.PI * 2f;
                    float run = Mathf.Clamp01(s.Velocity.x / 6f);
                    return StickmanPose.Stride(_stridePhase, Mathf.Lerp(0.35f, 1f, run), Mathf.Lerp(4f, 16f, run));
                case JumpPhase.Airborne:
                    if (s.Canopy > 0.6f) return StickmanPose.Hang(_time, Mathf.Clamp(-s.Velocity.x * 2.5f, -14f, 14f) + Mathf.Sin(_time * 1.4f) * 5f);
                    return StickmanPose.Flail(_time, Mathf.Clamp01(-s.Velocity.y / 25f));
                case JumpPhase.Landed:
                    return s.LandedOnTarget ? StickmanPose.Cheer(_time) : StickmanPose.Shrug(_time);
                case JumpPhase.TimeUp:
                    return StickmanPose.Shrug(_time);
                default:
                    return StickmanPose.Stand;
            }
        }

        static Vector2 Dir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
        }

        Vector2 PelvisLocal => new Vector2(0f, _cfg.UpperLegLength + _cfg.LowerLegLength - 0.02f + _pose.Bob);

        Vector2 ShoulderWorld()
        {
            var torso = _bones[(int)B.Torso];
            return (Vector2)torso.T.position + Dir(torso.T.eulerAngles.z) * (torso.Length - 0.04f);
        }

        void ApplyPose(Vector2 velocity)
        {
            var p = _pose;
            Vector2 pelvis = (Vector2)_root.position + Rotate(PelvisLocal, p.Body);
            float torsoA = p.Body + p.Torso;
            Vector2 torsoEnd = pelvis + Dir(torsoA) * _cfg.TorsoLength;
            Vector2 shoulder = pelvis + Dir(torsoA) * (_cfg.TorsoLength - 0.04f);
            Set(B.Torso, pelvis, torsoA);
            float headA = torsoA + p.Head;
            Set(B.Head, torsoEnd + Dir(headA) * (_cfg.HeadSize * 0.5f + 0.03f), headA - 180f);

            Chain(B.ArmFrontUp, B.ArmFrontLow, shoulder, p.Body + p.ArmFront, p.ElbowFront);
            Chain(B.ArmBackUp, B.ArmBackLow, shoulder, p.Body + p.ArmBack, p.ElbowBack);
            Chain(B.LegFrontUp, B.LegFrontLow, pelvis, p.Body + p.LegFront, p.KneeFront);
            Chain(B.LegBackUp, B.LegBackLow, pelvis, p.Body + p.LegBack, p.KneeBack);
        }

        void Chain(B upper, B lower, Vector2 start, float angle, float bend)
        {
            Set(upper, start, angle);
            Set(lower, start + Dir(angle) * _bones[(int)upper].Length, angle + bend);
        }

        void Set(B id, Vector2 pos, float angle)
        {
            var t = _bones[(int)id].T;
            t.position = new Vector3(pos.x, pos.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ------------------------------------------------------------------ ragdoll

        /// <summary>Дальше тело падает само. velocity — скорость в момент удара/срыва.</summary>
        public void BecomeRagdoll(Vector2 velocity, int seed)
        {
            if (_ragdoll) return;
            _ragdoll = true;
            _canopy.Detach(velocity);
            var rng = new System.Random(seed);
            var mat = new PhysicsMaterial2D("Stickman") { friction = 0.6f, bounciness = 0.2f };
            var colliders = new List<Collider2D>();
            float massUnit = _cfg.Mass / 10f;

            for (int i = 0; i < (int)B.Count; i++)
            {
                var bone = _bones[i];
                var rb = bone.T.gameObject.AddComponent<Rigidbody2D>();
                rb.mass = massUnit * (i == (int)B.Torso ? 3f : i == (int)B.Head ? 1f : 0.75f);
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
                    // Unity measures a hinge as (parent − child) relative to the pose at creation, so absolute ranges
                    // are shifted by the current bend.
                    float current = Mathf.DeltaAngle(bone.T.eulerAngles.z, _bones[(int)bone.Parent].T.eulerAngles.z);
                    joint.limits = Limits((B)i, current);
                }
                float jitter = ((float)rng.NextDouble() - 0.5f) * 2f;
                bone.Body.linearVelocity = velocity + new Vector2(jitter, (float)rng.NextDouble()) * 0.8f;
                bone.Body.angularVelocity = jitter * _cfg.CrashSpin * 40f;
            }
        }

        /// <param name="current">Current (parent − child) angle, degrees.</param>
        static JointAngleLimits2D Limits(B id, float current)
        {
            switch (id)
            {
                case B.LegFrontLow:
                case B.LegBackLow: // knee: straight (0) to folded backwards (+150)
                    return Absolute(-5f, 150f, current);
                case B.ArmFrontLow:
                case B.ArmBackLow: // elbow: straight (0) to folded forwards (−150)
                    return Absolute(-150f, 5f, current);
                case B.Head:
                    return new JointAngleLimits2D { min = -45f, max = 45f };
                case B.LegFrontUp:
                case B.LegBackUp:
                    return new JointAngleLimits2D { min = -100f, max = 100f };
                default:
                    return new JointAngleLimits2D { min = -170f, max = 170f };
            }
        }

        static JointAngleLimits2D Absolute(float min, float max, float current) =>
            new JointAngleLimits2D { min = Mathf.Min(min - current, 0f), max = Mathf.Max(max - current, 0f) };

        /// <summary>Отделившийся купол продолжает падать сам.</summary>
        public void UpdateDetached(float dt) => _canopy.UpdateDetached(dt);
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
