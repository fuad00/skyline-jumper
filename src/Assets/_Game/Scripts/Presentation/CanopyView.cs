using System.Collections.Generic;
using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Купол как в оригинале: дуга из секций (кадры para01..08 — это именно секции купола), стропы от каждой секции
    /// сходятся в точку над головой, оттуда «держатель» к плечам. При раскрытии секции разлетаются из комка в дугу.
    /// При ударе купол отрывается и медленно улетает.
    /// </summary>
    public sealed class CanopyView
    {
        static readonly Color LineColor = new Color(0.82f, 0.82f, 0.84f);
        static readonly Color FallbackRed = new Color(0.82f, 0.12f, 0.12f);

        // Geometry in metres (stickman ≈ 1.9 m tall, like the original screenshots).
        const float Radius = 6.2f;          // from the line convergence point to the cells
        const float HalfAngle = 58f;        // half of the arc when fully open
        const float CellScale = 0.72f;      // cell sprite scale at full size (PPU 22)
        const float HolderHeight = 1.15f;   // convergence point above the shoulders

        readonly ArtBank _art;
        readonly SpriteKit _kit;
        readonly Transform _holder;
        readonly List<SpriteRenderer> _cells = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _lines = new List<SpriteRenderer>();
        readonly SpriteRenderer _holderSprite;
        readonly SpriteRenderer _riserL, _riserR;
        bool _detached;
        Vector2 _velocity;
        float _spin, _fade = 1f, _deflate;

        public CanopyView(Transform parent, ArtBank art, SpriteKit kit)
        {
            _art = art;
            _kit = kit;
            _holder = new GameObject("Canopy").transform;
            _holder.SetParent(parent.parent, false); // sibling of the stickman so it can detach freely

            int count = art.CanopyOpening != null && art.CanopyOpening.Length > 0 ? art.CanopyOpening.Length : 7;
            for (int i = 0; i < count; i++)
            {
                var sprite = art.CanopyOpening != null && i < art.CanopyOpening.Length && art.CanopyOpening[i] != null ? art.CanopyOpening[i] : art.Circle;
                var r = kit.Make($"Cell {i + 1}", _holder, sprite, SortOrder.Canopy, sprite == art.Circle ? FallbackRed : Color.white);
                _cells.Add(r);
                _lines.Add(kit.Make($"Line {i + 1}", _holder, art.White, SortOrder.Canopy - 2, LineColor));
            }
            if (art.CanopyOpen != null)
            {
                _holderSprite = kit.Make("Holder", _holder, art.CanopyOpen, SortOrder.Canopy - 1);
                // The holder's lobes sit right under the hub where the long lines meet.
                float s = HolderHeight / Mathf.Max(0.01f, art.CanopyOpen.bounds.size.y);
                _holderSprite.transform.localScale = new Vector3(s, s, 1f);
                SpriteKit.PlaceBottomCenter(_holderSprite, 0f, 0f);
            }
            else
            {
                _riserL = kit.Make("RiserL", _holder, art.White, SortOrder.Canopy - 2, LineColor);
                _riserR = kit.Make("RiserR", _holder, art.White, SortOrder.Canopy - 2, LineColor);
            }
            _holder.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (_holder != null) Object.Destroy(_holder.gameObject);
        }

        public void UpdateAttached(JumpSession s, Vector2 shoulder, float bodyAngle, float time, float dt)
        {
            if (_detached) return;
            bool landed = s.Phase == Core.JumpPhase.Landed;
            bool visible = s.CanopyDeployed && (s.Phase == Core.JumpPhase.Airborne || landed);
            _holder.gameObject.SetActive(visible);
            if (!visible) return;

            // Once on the ground the canopy sinks behind the jumper, drifts with the wind and fades out.
            _deflate = landed ? Mathf.MoveTowards(_deflate, 1f, dt * 0.9f) : 0f;
            float sink = _deflate * _deflate;
            float open = landed ? 1f : s.Canopy;
            _holder.position = new Vector3(shoulder.x + sink * s.Level.Wind * 1.5f, shoulder.y - sink * 4f, 0f);
            float sway = Mathf.Sin(time * 1.2f) * 3f * (1f - _deflate);
            _holder.rotation = Quaternion.Euler(0f, 0f, bodyAngle * 0.5f + sway - sink * 20f * Mathf.Sign(s.Level.Wind == 0f ? 1f : s.Level.Wind));
            Layout(open, 1f - sink * 0.5f);
            SetAlpha(1f - Mathf.InverseLerp(0.35f, 1f, _deflate));
        }

        /// <summary>Расставляет секции по дуге. open — раскрытие 0..1, height — 1 стоит, 0 сложился.</summary>
        void Layout(float open, float height)
        {
            Vector2 hub = new Vector2(0f, HolderHeight);
            float radius = Mathf.Lerp(1.2f, Radius, open) * Mathf.Lerp(0.15f, 1f, height);
            float half = Mathf.Lerp(7f, HalfAngle, open) + (1f - height) * 25f;
            float scale = Mathf.Lerp(0.3f, CellScale, open);
            int n = _cells.Count;
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? 0f : Mathf.Lerp(-half, half, i / (float)(n - 1)) * Mathf.Deg2Rad;
                Vector2 pos = hub + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
                var cell = _cells[i];
                float cellScale = cell.sprite == _art.Circle ? scale * 1.4f : scale;
                cell.transform.localScale = new Vector3(cellScale, cellScale * Mathf.Lerp(0.6f, 1f, height), 1f);
                // Cell sprites are pre-rotated for their place on the arc; centre them on the arc point.
                Vector3 c = cell.sprite.bounds.center;
                cell.transform.localPosition = (Vector3)pos - new Vector3(c.x * cellScale, c.y * cellScale, 0f);
                Line(_lines[i], hub, pos - new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * cell.sprite.bounds.extents.y * cellScale * 0.6f);
            }
            if (_riserL != null)
            {
                Line(_riserL, new Vector2(-0.12f, 0f), hub);
                Line(_riserR, new Vector2(0.12f, 0f), hub);
            }
        }

        static void Line(SpriteRenderer r, Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            Vector2 unit = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            r.transform.localPosition = (from + to) * 0.5f;
            r.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude / unit.x, 0.04f / unit.y, 1f);
        }

        public void Detach(Vector2 velocity)
        {
            if (!_holder.gameObject.activeSelf) return;
            _detached = true;
            _velocity = new Vector2(velocity.x * 0.15f, Mathf.Max(velocity.y * 0.05f, 1f));
            _spin = Random.Range(-30f, 30f);
        }

        public void UpdateDetached(float dt)
        {
            if (!_detached || !_holder.gameObject.activeSelf) return;
            _velocity += new Vector2(0f, -2f) * dt;
            _velocity.y = Mathf.Max(_velocity.y, -2.5f);
            _holder.position += (Vector3)(_velocity * dt);
            _holder.Rotate(0f, 0f, _spin * dt);
            _fade = Mathf.Max(0f, _fade - dt * 0.3f);
            foreach (var r in _cells) Fade(r);
            foreach (var r in _lines) Fade(r);
            if (_holderSprite != null) Fade(_holderSprite);
            if (_fade <= 0f) _holder.gameObject.SetActive(false);
        }

        void SetAlpha(float a)
        {
            foreach (var r in _cells) Alpha(r, a);
            foreach (var r in _lines) Alpha(r, a);
            if (_holderSprite != null) Alpha(_holderSprite, a);
        }

        static void Alpha(SpriteRenderer r, float a)
        {
            var c = r.color;
            r.color = new Color(c.r, c.g, c.b, a);
        }

        void Fade(SpriteRenderer r)
        {
            var c = r.color;
            r.color = new Color(c.r, c.g, c.b, Mathf.Min(c.a, _fade));
        }
    }
}
