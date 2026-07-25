using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Relicfall.Core.Utils;

namespace Relicfall.Player
{
    /// <summary>
    /// Handles all player input using the New Input System.
    /// Supports keyboard+mouse and controller with full rebindable controls.
    /// Converts raw input into actionable data for the player controller.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Input Action Asset")]
        [SerializeField] private InputActionAsset _actionAsset;

        // Action references
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _lightAttackAction;
        private InputAction _heavyAttackAction;
        private InputAction _dashAction;
        private InputAction _parryAction;
        private InputAction _relicAbilityAction;
        private InputAction _secondaryAbilityAction;
        private InputAction _ultimateAction;
        private InputAction _interactAction;
        private InputAction _pauseAction;
        private InputAction _runInfoAction;

        // Current input state
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsControllerActive { get; private set; }
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.01f;

        // Input buffer
        public PlayerInputBuffer InputBuffer { get; private set; }

        // Aim direction (world space)
        public Vector3 AimDirectionWorld { get; private set; }
        public Vector3 MoveDirectionWorld { get; private set; }

        // Camera reference for aim direction calculation
        private Camera _mainCamera;

        // Smooth movement input
        private Vector2 _smoothMoveInput;
        private Vector2 _moveInputVelocity;
        private float _moveSmoothTime = 0.05f;

        private bool _inputEnabled = true;

        public bool InputEnabled => _inputEnabled;

        private void Awake()
        {
            InputBuffer = new PlayerInputBuffer();

            // Find or create input actions
            InitializeInputActions();
        }

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void InitializeInputActions()
        {
            // Create inline input actions for immediate functionality
            // These will be replaced by an InputActionAsset when available
            
            _moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
            _moveAction.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddBinding("<Gamepad>/leftStick");

            _lookAction = new InputAction("Look", binding: "<Mouse>/position");
            _lookAction.AddBinding("<Gamepad>/rightStick");

            _lightAttackAction = new InputAction("LightAttack", binding: "<Mouse>/leftButton");
            _lightAttackAction.AddBinding("<Gamepad>/buttonWest"); // X button

            _heavyAttackAction = new InputAction("HeavyAttack", binding: "<Mouse>/rightButton");
            _heavyAttackAction.AddBinding("<Gamepad>/rightTrigger");

            _dashAction = new InputAction("Dash", binding: "<Keyboard>/space");
            _dashAction.AddBinding("<Gamepad>/leftShoulder");

            _parryAction = new InputAction("Parry", binding: "<Keyboard>/shift");
            _parryAction.AddBinding("<Gamepad>/rightShoulder");

            _relicAbilityAction = new InputAction("RelicAbility", binding: "<Keyboard>/q");
            _relicAbilityAction.AddBinding("<Gamepad>/buttonNorth"); // Y button

            _secondaryAbilityAction = new InputAction("SecondaryAbility", binding: "<Keyboard>/e");
            _secondaryAbilityAction.AddBinding("<Gamepad>/buttonEast"); // B button

            _ultimateAction = new InputAction("Ultimate", binding: "<Keyboard>/r");
            _ultimateAction.AddBinding("<Gamepad>/leftTrigger");

            _interactAction = new InputAction("Interact", binding: "<Keyboard>/f");
            _interactAction.AddBinding("<Gamepad>/leftStickPress");

            _pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");
            _pauseAction.AddBinding("<Gamepad>/start");

            _runInfoAction = new InputAction("RunInfo", binding: "<Keyboard>/tab");
            _runInfoAction.AddBinding("<Gamepad>/select");

            // Enable all actions
            _moveAction.Enable();
            _lookAction.Enable();
            _lightAttackAction.Enable();
            _heavyAttackAction.Enable();
            _dashAction.Enable();
            _parryAction.Enable();
            _relicAbilityAction.Enable();
            _secondaryAbilityAction.Enable();
            _ultimateAction.Enable();
            _interactAction.Enable();
            _pauseAction.Enable();
            _runInfoAction.Enable();
        }

        /// <summary>
        /// Enable or disable input processing.
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled)
            {
                MoveInput = Vector2.zero;
                InputBuffer.ClearAll();
            }
        }

        /// <summary>
        /// Swap to a different input action asset (for rebinding).
        /// </summary>
        public void SetActionAsset(InputActionAsset asset)
        {
            if (_actionAsset != null)
                _actionAsset.Disable();

            _actionAsset = asset;
            if (_actionAsset != null)
                _actionAsset.Enable();

            // Re-resolve action references from the new asset
        }

        private void Update()
        {
            if (!_inputEnabled) return;

            // Read movement input
            var rawMove = _moveAction.ReadValue<Vector2>();
            MoveInput = Vector2.SmoothDamp(MoveInput, rawMove, ref _moveInputVelocity, _moveSmoothTime);

            // Read look/aim input
            LookInput = _lookAction.ReadValue<Vector2>();

            // Detect controller vs mouse
            IsControllerActive = Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.01f;

            // Convert move input to world-space direction
            ConvertMoveToWorldSpace();

            // Convert aim input to world-space direction
            ConvertAimToWorldSpace();

            // Handle button inputs with buffering
            if (_lightAttackAction.WasPressedThisFrame())
                InputBuffer.LightAttack.Press();
            if (_heavyAttackAction.WasPressedThisFrame())
                InputBuffer.HeavyAttack.Press();
            if (_dashAction.WasPressedThisFrame())
                InputBuffer.Dash.Press();
            if (_parryAction.WasPressedThisFrame())
                InputBuffer.Parry.Press();
            if (_relicAbilityAction.WasPressedThisFrame())
                InputBuffer.RelicAbility.Press();
            if (_secondaryAbilityAction.WasPressedThisFrame())
                InputBuffer.SecondaryAbility.Press();
            if (_ultimateAction.WasPressedThisFrame())
                InputBuffer.Ultimate.Press();
            if (_interactAction.WasPressedThisFrame())
                InputBuffer.Interact.Press();
            if (_pauseAction.WasPressedThisFrame())
            {
                // Handle pause directly
                Core.GameManager.Instance?.TogglePause();
            }
        }

        /// <summary>
        /// Convert 2D move input to 3D world-space direction relative to camera.
        /// </summary>
        private void ConvertMoveToWorldSpace()
        {
            if (_mainCamera == null) return;

            // Get camera forward and right projected onto ground plane
            var camForward = _mainCamera.transform.forward;
            var camRight = _mainCamera.transform.right;

            // Flatten to ground plane (remove Y component)
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // Combine inputs relative to camera
            MoveDirectionWorld = (camForward * MoveInput.y + camRight * MoveInput.x).normalized;
        }

        /// <summary>
        /// Convert aim input to world-space direction.
        /// Mouse: raycast from cursor to ground.
        /// Controller: use right stick direction relative to camera.
        /// </summary>
        private void ConvertAimToWorldSpace()
        {
            if (_mainCamera == null) return;

            if (IsControllerActive && LookInput.sqrMagnitude > 0.01f)
            {
                // Controller: right stick direction relative to camera
                var camForward = _mainCamera.transform.forward;
                var camRight = _mainCamera.transform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();
                AimDirectionWorld = (camForward * LookInput.y + camRight * LookInput.x).normalized;
            }
            else
            {
                // Mouse: ray from camera through mouse position to ground plane
                var mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
                var ray = _mainCamera.ScreenPointToRay(mousePos);
                var groundPlane = new Plane(Vector3.up, Vector3.zero);

                if (groundPlane.Raycast(ray, out float distance))
                {
                    var groundPoint = ray.GetPoint(distance);
                    var playerPos = transform.position;
                    AimDirectionWorld = (groundPoint - playerPos).normalized;
                    AimDirectionWorld.y = 0f;
                }
                else
                {
                    // Fallback: use movement direction
                    AimDirectionWorld = MoveDirectionWorld;
                }
            }
        }

        /// <summary>
        /// Get aim direction with aim assist applied.
        /// Snaps direction toward nearby enemies within a cone.
        /// </summary>
        public Vector3 GetAimDirectionWithAssist(float assistAngle = 15f, float assistRange = 8f)
        {
            var baseDir = AimDirectionWorld;

            if (IsControllerActive)
            {
                // Find nearest enemy within aim cone
                var enemies = FindNearbyEnemies(assistRange);
                float bestAngle = assistAngle;
                Vector3 bestTarget = baseDir;

                foreach (var enemy in enemies)
                {
                    var toEnemy = (enemy.position - transform.position).normalized;
                    toEnemy.y = 0f;
                    float angle = Vector3.Angle(baseDir, toEnemy);

                    if (angle < bestAngle)
                    {
                        bestAngle = angle;
                        bestTarget = toEnemy;
                    }
                }

                return bestTarget;
            }

            return baseDir;
        }

        private System.Collections.Generic.List<Transform> FindNearbyEnemies(float range)
        {
            var results = new System.Collections.Generic.List<Transform>();
            var colliders = Physics.OverlapSphere(transform.position, range, LayerMask.GetMask("EnemyHitbox"));
            foreach (var col in colliders)
            {
                if (col.TryGetComponent<EnemyMarker>(out var marker) && marker.IsAlive)
                    results.Add(col.transform);
            }
            return results;
        }

        private void OnDestroy()
        {
            _moveAction?.Dispose();
            _lookAction?.Dispose();
            _lightAttackAction?.Dispose();
            _heavyAttackAction?.Dispose();
            _dashAction?.Dispose();
            _parryAction?.Dispose();
            _relicAbilityAction?.Dispose();
            _secondaryAbilityAction?.Dispose();
            _ultimateAction?.Dispose();
            _interactAction?.Dispose();
            _pauseAction?.Dispose();
            _runInfoAction?.Dispose();
        }
    }

    /// <summary>
    /// Marker component for enemy identification by input handler.
    /// </summary>
    public class EnemyMarker : MonoBehaviour
    {
        public bool IsAlive = true;
        public string EnemyType;
    }
}
