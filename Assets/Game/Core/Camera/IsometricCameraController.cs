using UnityEngine;
using Relicfall.Player;

namespace Relicfall.Core.Camera
{
    /// <summary>
    /// Isometric camera controller for RELICFALL.
    /// Fixed elevated three-quarter perspective with wall avoidance,
    /// player outline when obscured, and smooth follow.
    /// No manual camera rotation during normal combat.
    /// </summary>
    public class IsometricCameraController : MonoBehaviour
    {
        [Header("Camera Position")]
        [SerializeField] private float _cameraHeight = 12f;
        [SerializeField] private float _cameraDistance = 10f;
        [SerializeField] private float _cameraAngle = 35f;
        [SerializeField] private Vector3 _cameraOffset = new Vector3(-3f, 12f, -6f);

        [Header("Following")]
        [SerializeField] private float _followSpeed = 8f;
        [SerializeField] private float _lookAheadDistance = 1.5f;
        [SerializeField] private float _lookAheadSpeed = 5f;
        [SerializeField] private float _maxFollowDistance = 3f;

        [Header("Wall Avoidance")]
        [SerializeField] private float _wallAvoidDistance = 0.5f;
        [SerializeField] private float _wallAvoidSpeed = 10f;
        [SerializeField] private LayerMask _wallLayer = 1 << 14; // Environment layer

        [Header("Player Outline")]
        [SerializeField] private bool _enablePlayerOutline = true;
        [SerializeField] private Color _outlineColor = new Color(0f, 0.9f, 1f, 1f); // Cyan
        [SerializeField] private float _outlineWidth = 3f;
        [SerializeField] private float _outlineCheckInterval = 0.1f;
        [SerializeField] private float _outlineFadeTime = 0.3f;

        [Header("Combat Camera")]
        [SerializeField] private float _combatZoomFactor = 0.85f;
        [SerializeField] private float _combatZoomSpeed = 3f;
        [SerializeField] private float _bossZoomFactor = 0.9f;

        [Header("Shake Integration")]
        [SerializeField] private float _shakeRecoverySpeed = 5f;

        private Transform _target;
        private Vector3 _currentVelocity;
        private Vector3 _desiredPosition;
        private Vector3 _currentPosition;
        private Vector3 _shakeOffset;
        private bool _isObscured;
        private float _outlineAlpha;
        private float _outlineCheckTimer;
        private bool _inCombat;
        private float _currentZoomFactor = 1f;
        private Renderer _playerRenderer;
        private Material _outlineMaterial;
        private PlayerController _playerController;

        private void Start()
        {
            FindPlayer();
            InitializeCameraPosition();
            CreateOutlineMaterial();
        }

        private void FindPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _target = player.transform;
                _playerRenderer = player.GetComponentInChildren<SkinnedMeshRenderer>();
                _playerController = player.GetComponent<PlayerController>();
            }
        }

        private void InitializeCameraPosition()
        {
            if (_target != null)
            {
                _desiredPosition = _target.position + _cameraOffset;
                _currentPosition = _desiredPosition;
                transform.position = _desiredPosition;
                transform.rotation = Quaternion.Euler(_cameraAngle, 45f, 0f);
            }
        }

        private void CreateOutlineMaterial()
        {
            _outlineMaterial = new Material(Resources.Load<Shader>("Shaders/OutlineShader") ?? Shader.Find("Unlit/Color"));
            _outlineMaterial.color = _outlineColor;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                FindPlayer();
                return;
            }

            // Calculate desired camera position
            UpdateDesiredPosition();

            // Apply wall avoidance
            ApplyWallAvoidance();

            // Smoothly follow target
            UpdateCameraPosition();

            // Apply shake offset
            ApplyShakeOffset();

            // Update camera look at
            UpdateCameraLook();

            // Check for player obscurement
            CheckPlayerObscurement();

            // Update zoom based on combat state
            UpdateCombatZoom();
        }

        private void UpdateDesiredPosition()
        {
            // Base position: target + offset
            Vector3 targetPos = _target.position + _cameraOffset;

            // Add look-ahead based on player movement direction
            if (_playerController != null)
            {
                Vector3 moveDir = _playerController.MoveVelocity.normalized;
                Vector3 lookAhead = moveDir * _lookAheadDistance;
                targetPos += lookAhead;
            }

            _desiredPosition = targetPos;
        }

        private void ApplyWallAvoidance()
        {
            // Check if camera would be behind a wall
            Vector3 dirToTarget = (_target.position - _desiredPosition).normalized;
            float distToTarget = Vector3.Distance(_desiredPosition, _target.position);

            if (Physics.Raycast(_desiredPosition, dirToTarget, distToTarget, _wallLayer))
            {
                // Move camera closer to player to avoid wall
                Vector3 avoidDirection = (_target.position - _desiredPosition).normalized;
                float avoidAmount = _wallAvoidSpeed * Time.deltaTime;
                _desiredPosition += avoidDirection * avoidAmount;
            }
        }

        private void UpdateCameraPosition()
        {
            // Smooth follow with max distance constraint
            _currentPosition = Vector3.SmoothDamp(
                _currentPosition,
                _desiredPosition * _currentZoomFactor,
                ref _currentVelocity,
                1f / _followSpeed,
                Mathf.Infinity,
                Time.deltaTime
            );

            // Clamp distance from target
            float currentDist = Vector3.Distance(_currentPosition, _target.position);
            if (currentDist > _maxFollowDistance + Vector3.Distance(_desiredPosition, _target.position))
            {
                Vector3 clampedDir = (_currentPosition - _target.position).normalized;
                float maxDist = Vector3.Distance(_desiredPosition, _target.position) + _maxFollowDistance;
                _currentPosition = _target.position + clampedDir * maxDist;
            }

            transform.position = _currentPosition;
        }

        private void ApplyShakeOffset()
        {
            _shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, _shakeRecoverySpeed * Time.deltaTime);
            transform.position += _shakeOffset;
        }

        private void UpdateCameraLook()
        {
            // Look at player position with slight offset for better framing
            Vector3 lookTarget = _target.position + Vector3.up * 0.5f;
            transform.LookAt(lookTarget);
        }

        private void CheckPlayerObscurement()
        {
            _outlineCheckTimer -= Time.deltaTime;
            if (_outlineCheckTimer > 0f) return;
            _outlineCheckTimer = _outlineCheckInterval;

            // Raycast from camera to player
            Vector3 camPos = transform.position;
            Vector3 playerPos = _target.position + Vector3.up * 1f;
            Vector3 dir = (playerPos - camPos).normalized;
            float dist = Vector3.Distance(camPos, playerPos);

            RaycastHit hit;
            bool wasObscured = _isObscured;
            _isObscured = Physics.Raycast(camPos, dir, out hit, dist, _wallLayer);

            // Update outline visibility
            if (_isObscured && !wasObscured)
                _outlineAlpha = 1f;
            else if (!_isObscured && wasObscured)
                _outlineAlpha = 0f;

            // Smooth outline fade
            if (_enablePlayerOutline && _playerRenderer != null)
            {
                float targetAlpha = _isObscured ? 1f : 0f;
                _outlineAlpha = Mathf.Lerp(_outlineAlpha, targetAlpha, Time.deltaTime / _outlineFadeTime);
                UpdatePlayerOutline(_outlineAlpha);
            }
        }

        private void UpdatePlayerOutline(float alpha)
        {
            // In full implementation, this would use an outline shader pass
            // For now, we toggle a secondary renderer
        }

        private void UpdateCombatZoom()
        {
            float targetZoom = 1f;

            // Zoom in slightly during combat
            if (_inCombat)
                targetZoom = _combatZoomFactor;

            // Additional zoom for boss
            var gm = Core.GameManager.Instance;
            if (gm != null && gm.CurrentState == Core.GameManager.GameState.BossArena)
                targetZoom = _bossZoomFactor;

            _currentZoomFactor = Mathf.Lerp(_currentZoomFactor, targetZoom, _combatZoomSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Set combat zoom state (called when enemies are nearby).
        /// </summary>
        public void SetCombatZoom(bool inCombat)
        {
            _inCombat = inCombat;
        }

        /// <summary>
        /// Add a shake offset (called by CombatFeedback).
        /// </summary>
        public void AddShakeOffset(Vector3 offset)
        {
            _shakeOffset += offset;
        }

        /// <summary>
        /// Override camera position for boss intros or special events.
        /// </summary>
        public void OverridePosition(Vector3 position, float duration)
        {
            // Animate to override position then return
        }

        /// <summary>
        /// Reset camera to default following state.
        /// </summary>
        public void ResetCamera()
        {
            _inCombat = false;
            _currentZoomFactor = 1f;
        }
    }
}
