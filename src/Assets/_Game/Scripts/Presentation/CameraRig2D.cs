using StickmanJumper.Config;
using StickmanJumper.Core;
using UnityEngine;

namespace StickmanJumper.Presentation
{
    /// <summary>
    /// Камера сбоку: на крыше близко, в полёте отъезжает со скоростью, смотрит чуть вперёд по движению.
    /// Перед первым тапом и по кнопке зума показывает весь уровень (как зум в оригинале). Без рывков: всё через сглаживание.
    /// </summary>
    public sealed class CameraRig2D
    {
        readonly Camera _cam;
        readonly CameraConfig _cfg;
        Vector2 _center;
        float _size;
        float _shake;

        public bool Overview { get; set; }

        public CameraRig2D(Camera cam, CameraConfig cfg)
        {
            _cam = cam;
            _cfg = cfg;
            _cam.orthographic = true;
        }

        public void Snap(Vector2 center, float size)
        {
            _center = center;
            _size = size;
            Apply(Vector2.zero);
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        /// <summary>Размер кадра, чтобы целиком показать прямоугольник.</summary>
        public float FitSize(Rect r) => Mathf.Max(r.height * 0.5f, r.width * 0.5f / Mathf.Max(0.1f, _cam.aspect)) + _cfg.OverviewMargin;

        public void Update(Vector2 focus, Vector2 velocity, JumpPhase phase, bool ragdoll, Rect overview, float dt)
        {
            Vector2 targetCenter;
            float targetSize;
            if (Overview || phase == JumpPhase.Ready)
            {
                targetCenter = overview.center;
                targetSize = FitSize(overview);
            }
            else if (phase.IsOnRoof())
            {
                targetCenter = focus + new Vector2(2.2f, 1.6f);
                targetSize = _cfg.SizeOnRoof;
            }
            else if (phase == JumpPhase.Airborne)
            {
                Vector2 ahead = Vector2.ClampMagnitude(velocity * _cfg.LookAhead, _cfg.LookAheadMax);
                targetCenter = focus + Vector2.up * 1f + ahead;
                targetSize = Mathf.Min(_cfg.SizeMax, _cfg.SizeInFlight + velocity.magnitude * _cfg.SizePerSpeed);
            }
            else
            {
                targetCenter = focus + Vector2.up * (ragdoll ? 0.5f : 1.2f);
                targetSize = ragdoll ? _cfg.SizeOnRoof + 1.5f : _cfg.SizeOnRoof;
            }

            float kc = 1f - Mathf.Exp(-_cfg.FollowSharpness * dt);
            float ks = 1f - Mathf.Exp(-_cfg.SizeSharpness * dt);
            _center = Vector2.Lerp(_center, targetCenter, kc);
            _size = Mathf.Lerp(_size, targetSize, ks);
            // Never show much below the street.
            _center.y = Mathf.Max(_center.y, _size - 2.5f);

            Vector2 shake = Vector2.zero;
            if (_shake > 0.001f)
            {
                shake = Random.insideUnitCircle * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.5f);
            }
            Apply(shake);
        }

        void Apply(Vector2 shake)
        {
            _cam.orthographicSize = _size;
            _cam.transform.position = new Vector3(_center.x + shake.x, _center.y + shake.y, -10f);
        }
    }
}
