using UnityEngine;
using UnityEngine.InputSystem;

namespace Relicfall.Settings
{
    /// <summary>
    /// Input action asset configuration for RELICFALL.
    /// Creates and manages the InputActionAsset at runtime for full rebinding support.
    /// </summary>
    public class InputActionAssetConfig : MonoBehaviour
    {
        public InputActionAsset ActionAsset { get; private set; }

        private void Awake()
        {
            CreateDefaultAsset();
        }

        private void CreateDefaultAsset()
        {
            ActionAsset = new InputActionAsset();
            ActionAsset.name = "RELICFALLControls";

            // Movement map
            var moveMap = ActionAsset.AddActionMap("PlayerMovement");
            var moveAction = moveMap.AddAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            moveAction.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Combat map
            var combatMap = ActionAsset.AddActionMap("PlayerCombat");
            combatMap.AddAction("LightAttack", InputActionType.Button, "<Mouse>/leftButton")
                .AddBinding("<Gamepad>/buttonWest");
            combatMap.AddAction("HeavyAttack", InputActionType.Button, "<Mouse>/rightButton")
                .AddBinding("<Gamepad>/rightTrigger");
            combatMap.AddAction("Dash", InputActionType.Button, "<Keyboard>/space")
                .AddBinding("<Gamepad>/leftShoulder");
            combatMap.AddAction("Parry", InputActionType.Button, "<Keyboard>/shift")
                .AddBinding("<Gamepad>/rightShoulder");

            // Abilities map
            var abilityMap = ActionAsset.AddActionMap("PlayerAbilities");
            abilityMap.AddAction("RelicAbility", InputActionType.Button, "<Keyboard>/q")
                .AddBinding("<Gamepad>/buttonNorth");
            abilityMap.AddAction("SecondaryAbility", InputActionType.Button, "<Keyboard>/e")
                .AddBinding("<Gamepad>/buttonEast");
            abilityMap.AddAction("Ultimate", InputActionType.Button, "<Keyboard>/r")
                .AddBinding("<Gamepad>/leftTrigger");

            // Interaction map
            var interactMap = ActionAsset.AddActionMap("PlayerInteraction");
            interactMap.AddAction("Interact", InputActionType.Button, "<Keyboard>/f")
                .AddBinding("<Gamepad>/leftStickPress");

            // System map
            var systemMap = ActionAsset.AddActionMap("System");
            systemMap.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape")
                .AddBinding("<Gamepad>/start");
            systemMap.AddAction("RunInfo", InputActionType.Button, "<Keyboard>/tab")
                .AddBinding("<Gamepad>/select");

            // Aim map
            var aimMap = ActionAsset.AddActionMap("PlayerAim");
            aimMap.AddAction("Look", InputActionType.Value, "<Mouse>/position")
                .AddBinding("<Gamepad>/rightStick");

            ActionAsset.Enable();
        }

        /// <summary>
        /// Get the action map for a specific category.
        /// </summary>
        public InputActionMap GetMap(string mapName)
        {
            return ActionAsset.FindActionMap(mapName);
        }

        /// <summary>
        /// Rebind a specific action to a new binding path.
        /// </summary>
        public void RebindAction(string mapName, string actionName, string newBindingPath)
        {
            var map = GetMap(mapName);
            if (map == null) return;

            var action = map.FindAction(actionName);
            if (action == null) return;

            // Remove existing bindings and add new one
            action.ChangeBinding(0).WithPath(newBindingPath);
        }

        /// <summary>
        /// Save current bindings to persistent storage.
        /// </summary>
        public void SaveBindings()
        {
            var saveManager = GetComponent<Relicfall.Saving.SaveManager>();
            if (saveManager == null) return;

            var bindingsData = new Relicfall.Saving.InputBindingsSaveData();
            foreach (var map in ActionAsset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    foreach (var binding in action.bindings)
                    {
                        bindingsData.CustomBindings.Add(new Relicfall.Saving.BindingEntry
                        {
                            ActionName = action.name,
                            BindingPath = binding.path,
                            OverridePath = binding.effectivePath
                        });
                    }
                }
            }

            saveManager.CurrentData.InputBindings = bindingsData;
            saveManager.SaveGame(saveManager.CurrentData);
        }

        /// <summary>
        /// Load bindings from persistent storage.
        /// </summary>
        public void LoadBindings()
        {
            var saveManager = GetComponent<Relicfall.Saving.SaveManager>();
            if (saveManager == null) return;

            var bindingsData = saveManager.CurrentData?.InputBindings;
            if (bindingsData == null) return;

            foreach (var entry in bindingsData.CustomBindings)
            {
                var action = ActionAsset.FindAction(entry.ActionName);
                if (action != null)
                {
                    action.ChangeBinding(0).WithPath(entry.OverridePath);
                }
            }
        }
    }
}
