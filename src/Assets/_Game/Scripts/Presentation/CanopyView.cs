using StickmanJumper.Config;
using StickmanJumper.Gameplay;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>Купол над стикменом: кадры раскрытия → полностью открытый со стропами. При ударе отрывается и улетает.</summary>
    public sealed class CanopyView
    {
        static readonly Color LineColor = new Color(0.85f, 0.85f, 0.85f);
        static readonly Color FallbackRed = new Color(0.85f, 0.12f, 0.12f);
        const float OpenScale = 1.7f;

        readonly ArtBank _art;
        readonly Transform _holder;
        readonly SpriteRenderer _canopy;
        readonly SpriteRenderer _lineLeft, _lineRight;
        bool _detached;
        Vector2 _velocity;
        float _spin, _fade = 1f;

        public CanopyView(Transform parent, ArtBank art, SpriteKit kit)
        {
            _art = art;
            _holder = new GameObject("Canopy").transform;
            _holder.SetParent(parent.parent, false); // sibling of the stickman so it can detach freely
            _canopy = kit.Make("Shape", _holder, art.CanopyOpen != null ? art.CanopyOpen : art.Circle, SortOrder.Canopy,
                art.CanopyOpen != null ? Color.white : FallbackRed);
            _lineLeft = kit.Make("LineL", _holder, art.White, SortOrder.Canopy - 1, LineColor);
            _lineRight = kit.Make("LineR", _holder, art.White, SortOrder.Canopy - 1, LineColor);
            _holder.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (_holder != null) Object.Destroy(_holder.gameObject);
        }

        public void UpdateAttached(JumpSession s, Vector2 shoulder, float bodyAngle, float time, float dt)
        {
            if (_detached) return;
            bool visible = s.CanopyDeployed && s.Phase == Core.JumpPhase.Airborne || s.Phase == Core.JumpPhase.Landed && s.CanopyDeployed;
            _holder.gameObject.SetActive(visible);
            if (!visible) return;

            float open = s.Phase == Core.JumpPhase.Landed ? 1f : s.Canopy;
            _holder.position = new Vector3(shoulder.x, shoulder.y, 0f);
            float sway = Mathf.Sin(time * 1.4f) * 4f;
            _holder.rotation = Quaternion.Euler(0f, 0f, bodyAngle * 0.6f + sway);

            var frames = _art.CanopyOpening;
            bool fullyOpen = open >= 0.999f || frames == null || frames.Length == 0;
            if (fullyOpen && _art.CanopyOpen != null)
            {
                _canopy.sprite = _art.CanopyOpen;
                _canopy.transform.localScale = new Vector3(OpenScale, OpenScale, 1f);
                SpriteKit.PlaceBottomCenter(_canopy, 0f, 0f);
                _lineLeft.enabled = _lineRight.enabled = false;
            }
            else
            {
                if (frames != null && frames.Length > 0)
                {
                    var frame = frames[Mathf.Clamp((int)(open * frames.Length), 0, frames.Length - 1)];
                    if (frame != null) _canopy.sprite = frame;
                }
                float scale = Mathf.Lerp(0.5f, OpenScale, open);
                _canopy.transform.localScale = new Vector3(scale, scale, 1f);
                float lift = Mathf.Lerp(0.5f, 1.9f, open);
                SpriteKit.PlaceBottomCenter(_canopy, 0f, lift);
                float half = _canopy.bounds.extents.x * 0.85f;
                Line(_lineLeft, Vector2.zero, new Vector2(-half, lift));
                Line(_lineRight, Vector2.zero, new Vector2(half, lift));
            }
            // After landing the canopy deflates onto the roof.
            if (s.Phase == Core.JumpPhase.Landed) _holder.localScale = Vector3.Lerp(_holder.localScale, new Vector3(1f, 0.2f, 1f), 1f - Mathf.Exp(-3f * dt));
            else _holder.localScale = Vector3.one;
        }

        void Line(SpriteRenderer r, Vector2 from, Vector2 to)
        {
            r.enabled = true;
            Vector2 d = to - from;
            Vector2 unit = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            r.transform.localPosition = (from + to) * 0.5f;
            r.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude / unit.x, 0.035f / unit.y, 1f);
        }

        public void Detach(Vector2 velocity)
        {
            if (!_holder.gameObject.activeSelf) return;
            _detached = true;
            _velocity = new Vector2(velocity.x * 0.2f, Mathf.Max(velocity.y * 0.1f, 1.5f));
            _spin = Random.Range(-40f, 40f);
        }

        public void UpdateDetached(float dt)
        {
            if (!_detached || !_holder.gameObject.activeSelf) return;
            _velocity += new Vector2(0f, -2.5f) * dt;
            _velocity.y = Mathf.Max(_velocity.y, -2.5f);
            _holder.position += (Vector3)(_velocity * dt);
            _holder.Rotate(0f, 0f, _spin * dt);
            _fade = Mathf.Max(0f, _fade - dt * 0.35f);
            var c = _canopy.color;
            _canopy.color = new Color(c.r, c.g, c.b, _fade);
            _lineLeft.enabled = _lineRight.enabled = false;
            if (_fade <= 0f) _holder.gameObject.SetActive(false);
        }
    }
}
