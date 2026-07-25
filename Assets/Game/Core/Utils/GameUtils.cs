using UnityEngine;

namespace Relicfall.Core.Utils
{
    /// <summary>
    /// Utility class for common math and game calculations.
    /// </summary>
    public static class GameMath
    {
        /// <summary>
        /// Remap a value from one range to another.
        /// </summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }

        /// <summary>
        /// Calculate damage with critical hit multiplier.
        /// </summary>
        public static float CalculateDamage(float baseDamage, bool isCritical, float critMultiplier = 2f)
        {
            return isCritical ? baseDamage * critMultiplier : baseDamage;
        }

        /// <summary>
        /// Check if an attack hits based on angle between attack direction and target facing.
        /// Used for frontal vs rear hit detection.
        /// </summary>
        public static bool IsFrontalHit(Vector3 attackDir, Vector3 targetForward, float frontalAngle = 120f)
        {
            float angle = Vector3.Angle(attackDir, targetForward);
            return angle < frontalAngle;
        }

        /// <summary>
        /// Get the knockback direction, combining hit direction with upward component.
        /// </summary>
        public static Vector3 CalculateKnockback(Vector3 hitDirection, float knockbackForce, float upwardRatio = 0.2f)
        {
            Vector3 result = hitDirection.normalized * knockbackForce;
            result.y = knockbackForce * upwardRatio;
            return result;
        }

        /// <summary>
        /// Smooth damp for float values, useful for animation blending.
        /// </summary>
        public static float SmoothDampFloat(float current, float target, ref float velocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            return Mathf.SmoothDamp(current, target, ref velocity, smoothTime, maxSpeed, deltaTime);
        }

        /// <summary>
        /// Check if a point is within an isometric camera's visible area.
        /// </summary>
        public static bool IsVisibleOnIsometricCamera(Vector3 worldPos, Camera cam, float margin = 0.1f)
        {
            var screenPos = cam.WorldToViewportPoint(worldPos);
            return screenPos.x >= -margin && screenPos.x <= 1f + margin &&
                   screenPos.y >= -margin && screenPos.y <= 1f + margin &&
                   screenPos.z > 0;
        }

        /// <summary>
        /// Convert an isometric grid position to world position.
        /// </summary>
        public static Vector3 IsometricGridToWorld(int gridX, int gridZ, float cellSize, float isoAngle = 30f)
        {
            float rad = isoAngle * Mathf.Deg2Rad;
            float scaleX = cellSize * Mathf.Cos(rad);
            float scaleY = cellSize * Mathf.Sin(rad);
            return new Vector3(gridX * scaleX, 0, gridZ * scaleY);
        }
    }

    /// <summary>
    /// Timer utility for cooldowns, durations, and delays.
    /// </summary>
    public class GameTimer
    {
        public float Duration { get; set; }
        public float Remaining { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsComplete => !IsRunning && Remaining <= 0f;
        public float Progress => Duration > 0f ? 1f - (Remaining / Duration) : 0f;
        public float Normalized => Mathf.Clamp01(Progress);

        public GameTimer(float duration)
        {
            Duration = duration;
            Remaining = duration;
            IsRunning = false;
        }

        public void Start()
        {
            Remaining = Duration;
            IsRunning = true;
        }

        public void Start(float duration)
        {
            Duration = duration;
            Start();
        }

        public void Reset()
        {
            Remaining = Duration;
            IsRunning = false;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public bool Tick(float deltaTime)
        {
            if (!IsRunning) return false;
            Remaining -= deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                IsRunning = false;
                return true; // Timer just completed
            }
            return false;
        }
    }

    /// <summary>
    /// Cooldown timer that auto-resets on completion.
    /// </summary>
    public class CooldownTimer : GameTimer
    {
        public bool IsReady => !IsRunning && Remaining <= 0f;

        public CooldownTimer(float duration) : base(duration) { }

        public void Use()
        {
            Start();
        }

        public bool TryUse()
        {
            if (IsReady)
            {
                Use();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Buffered input tracker for action games.
    /// Supports input buffering with configurable window duration.
    /// </summary>
    public class BufferedInput
    {
        private float _bufferDuration;
        private float _bufferTimestamp;
        private bool _buffered;
        private bool _consumed;

        public BufferedInput(float bufferDuration = 0.15f)
        {
            _bufferDuration = bufferDuration;
            _bufferTimestamp = 0f;
            _buffered = false;
            _consumed = false;
        }

        /// <summary>
        /// Register an input press. Will be buffered for the configured duration.
        /// </summary>
        public void Press()
        {
            _buffered = true;
            _consumed = false;
            _bufferTimestamp = Time.time;
        }

        /// <summary>
        /// Check if a buffered input is available and consume it.
        /// </summary>
        public bool Consume()
        {
            if (_buffered && !_consumed && Time.time - _bufferTimestamp <= _bufferDuration)
            {
                _consumed = true;
                _buffered = false;
                return true;
            }
            _buffered = false;
            return false;
        }

        /// <summary>
        /// Check if a buffered input is available without consuming it.
        /// </summary>
        public bool Peek()
        {
            return _buffered && !_consumed && Time.time - _bufferTimestamp <= _bufferDuration;
        }

        /// <summary>
        /// Clear the buffer.
        /// </summary>
        public void Clear()
        {
            _buffered = false;
            _consumed = false;
        }
    }

    /// <summary>
    /// Combination of buffered inputs for a complete player input handler.
    /// </summary>
    public class PlayerInputBuffer
    {
        public BufferedInput LightAttack { get; }
        public BufferedInput HeavyAttack { get; }
        public BufferedInput Dash { get; }
        public BufferedInput Parry { get; }
        public BufferedInput RelicAbility { get; }
        public BufferedInput SecondaryAbility { get; }
        public BufferedInput Ultimate { get; }
        public BufferedInput Interact { get; }

        public PlayerInputBuffer(
            float lightBuffer = 0.2f,
            float heavyBuffer = 0.2f,
            float dashBuffer = 0.15f,
            float parryBuffer = 0.25f,
            float abilityBuffer = 0.15f,
            float ultimateBuffer = 0.1f,
            float interactBuffer = 0.1f)
        {
            LightAttack = new BufferedInput(lightBuffer);
            HeavyAttack = new BufferedInput(heavyBuffer);
            Dash = new BufferedInput(dashBuffer);
            Parry = new BufferedInput(parryBuffer);
            RelicAbility = new BufferedInput(abilityBuffer);
            SecondaryAbility = new BufferedInput(abilityBuffer);
            Ultimate = new BufferedInput(ultimateBuffer);
            Interact = new BufferedInput(interactBuffer);
        }

        public void ClearAll()
        {
            LightAttack.Clear();
            HeavyAttack.Clear();
            Dash.Clear();
            Parry.Clear();
            RelicAbility.Clear();
            SecondaryAbility.Clear();
            Ultimate.Clear();
            Interact.Clear();
        }
    }
}
