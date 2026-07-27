using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Player;
using Relicfall.Corruption;
using Relicfall.Relics;
using Relicfall.Runs;
using Relicfall.Combat;
using Relicfall.Bosses;

namespace Relicfall.UI
{
    /// <summary>
    /// Main HUD overlay for gameplay. Shows health, abilities, relics,
    /// corruption, currency, and boss health. Minimal dark-fantasy interface.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Slider _healthBar;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private Image _healthBarFill;
        [SerializeField] private Color _healthColorHigh = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color _healthColorLow = new Color(1f, 0.3f, 0.1f);

        [Header("Ability Cooldowns")]
        [SerializeField] private Image _dashCooldownIcon;
        [SerializeField] private Image _parryCooldownIcon;
        [SerializeField] private Image _ultimateCooldownIcon;
        [SerializeField] private Image _abilityCooldownIcon;
        [SerializeField] private TextMeshProUGUI _dashCooldownText;
        [SerializeField] private TextMeshProUGUI _parryCooldownText;
        [SerializeField] private TextMeshProUGUI _ultimateCooldownText;
        [SerializeField] private Slider _dashCooldownSlider;
        [SerializeField] private Slider _parryCooldownSlider;
        [SerializeField] private Slider _ultimateCooldownSlider;

        [Header("Relic Slots")]
        [SerializeField] private Transform _relicSlotContainer;
        [SerializeField] private GameObject _relicSlotPrefab;
        [SerializeField] private int _maxRelicSlots = 12;
        private List<Image> _relicSlotIcons = new();

        [Header("Corruption Meter")]
        [SerializeField] private Slider _corruptionBar;
        [SerializeField] private TextMeshProUGUI _corruptionText;
        [SerializeField] private Image _corruptionBarFill;
        [SerializeField] private Color _corruptionLow = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] private Color _corruptionMid = new Color(0.85f, 0.4f, 0.2f);
        [SerializeField] private Color _corruptionHigh = new Color(0.85f, 0.2f, 0.3f);
        [SerializeField] private Color _corruptionCritical = new Color(0.6f, 0f, 0.5f);

        [Header("Boss Health")]
        [SerializeField] private Slider _bossHealthBar;
        [SerializeField] private TextMeshProUGUI _bossNameText;
        [SerializeField] private TextMeshProUGUI _bossPhaseText;
        [SerializeField] private GameObject _bossHealthBarContainer;

        [Header("Currency")]
        [SerializeField] private TextMeshProUGUI _currencyText;

        [Header("Interaction Prompts")]
        [SerializeField] private GameObject _interactionPromptContainer;
        [SerializeField] private TextMeshProUGUI _interactionPromptText;
        [SerializeField] private Image _interactionPromptIcon;

        [Header("Controller Glyphs")]
        [SerializeField] private Sprite _keyboardGlyphLightAttack;
        [SerializeField] private Sprite _controllerGlyphLightAttack;
        [SerializeField] private Sprite _keyboardGlyphHeavyAttack;
        [SerializeField] private Sprite _controllerGlyphHeavyAttack;
        [SerializeField] private Sprite _keyboardGlyphDash;
        [SerializeField] private Sprite _controllerGlyphDash;
        [SerializeField] private Sprite _keyboardGlyphParry;
        [SerializeField] private Sprite _controllerGlyphParry;
        [SerializeField] private Sprite _keyboardGlyphInteract;
        [SerializeField] private Sprite _controllerGlyphInteract;

        [Header("Combo Counter")]
        [SerializeField] private TextMeshProUGUI _comboText;
        [SerializeField] private GameObject _comboContainer;

        [Header("Run Info Overlay")]
        [SerializeField] private GameObject _runInfoOverlay;
        [SerializeField] private TextMeshProUGUI _runTimeText;
        [SerializeField] private TextMeshProUGUI _runRoomsText;
        [SerializeField] private TextMeshProUGUI _runEnemiesText;
        [SerializeField] private TextMeshProUGUI _runRelicsText;

        private PlayerController _player;
        private RelicManager _relicManager;
        private CorruptionTracker _corruption;
        private bool _isControllerActive;

        private void Start()
        {
            FindReferences();
            CreateRelicSlots();
            HideBossHealthBar();
            HideInteractionPrompt();
            HideComboCounter();
            HideRunInfo();

            EventBus.Subscribe<RelicCollectedEvent>(OnRelicCollected);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<RelicCollectedEvent>(OnRelicCollected);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void FindReferences()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _player = player.GetComponent<PlayerController>();
                _relicManager = player.GetComponent<RelicManager>();
            }
        }

        private void CreateRelicSlots()
        {
            for (int i = 0; i < _maxRelicSlots; i++)
            {
                var slot = Instantiate(_relicSlotPrefab, _relicSlotContainer);
                var icon = slot.GetComponent<Image>();
                _relicSlotIcons.Add(icon);
                icon.enabled = false;
            }
        }

        private void Update()
        {
            if (_player == null) return;

            // Health bar
            UpdateHealthBar();

            // Cooldown indicators
            UpdateCooldowns();

            // Corruption meter
            UpdateCorruptionMeter();

            // Currency
            UpdateCurrency();

            // Controller detection
            UpdateControllerGlyphs();

            // Boss health bar (if active)
            UpdateBossHealthBar();
        }

        private void UpdateHealthBar()
        {
            float healthPercent = _player.HealthPercent;
            _healthBar.value = healthPercent;
            _healthText.text = $"{(int)_player.CurrentHealth}/{(int)_player.MaxHealth}";
            _healthBarFill.color = Color.Lerp(_healthColorLow, _healthColorHigh, healthPercent);
        }

        private void UpdateCooldowns()
        {
            // Dash cooldown
            _dashCooldownSlider.value = _player.DashCooldownProgress;
            _dashCooldownIcon.color = _player.DashCooldownProgress >= 1f ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f);

            // Parry cooldown
            _parryCooldownSlider.value = _player.ParryCooldownProgress;
            _parryCooldownIcon.color = _player.ParryCooldownProgress >= 1f ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f);

            // Ultimate cooldown
            _ultimateCooldownSlider.value = _player.UltimateCooldownProgress;
            _ultimateCooldownIcon.color = _player.UltimateCooldownProgress >= 1f ? new Color(0f, 0.9f, 1f) : new Color(0.3f, 0.3f, 0.5f, 0.8f);
        }

        private void UpdateCorruptionMeter()
        {
            float corruption = Core.GameManager.Instance?.Corruption?.CurrentLevel ?? 0f;
            _corruptionBar.value = corruption / 100f;
            _corruptionText.text = $"{(int)corruption}%";

            // Color based on corruption tier
            if (corruption < 25f)
                _corruptionBarFill.color = _corruptionLow;
            else if (corruption < 50f)
                _corruptionBarFill.color = _corruptionMid;
            else if (corruption < 75f)
                _corruptionBarFill.color = _corruptionHigh;
            else
                _corruptionBarFill.color = _corruptionCritical;
        }

        private void UpdateCurrency()
        {
            var run = Core.GameManager.Instance?.CurrentRun;
            if (run != null)
            {
                _currencyText.text = $"◆ {run.ResourcesEarned:F0}";
            }
        }

        private void UpdateControllerGlyphs()
        {
            var inputHandler = _player?.GetComponent<PlayerInputHandler>();
            _isControllerActive = inputHandler?.IsControllerActive ?? false;
            // Swap glyph sprites based on active input method
        }

        private void UpdateBossHealthBar()
        {
            // Find active boss and update its health bar
            var boss = FindObjectOfType<BossController>();
            if (boss != null && boss.IsAlive)
            {
                ShowBossHealthBar(boss.BossDef?.BossName ?? "Unknown", boss.HealthPercent);
            }
        }

        #region Show/Hide UI Elements

        public void ShowBossHealthBar(string bossName, float healthPercent)
        {
            _bossHealthBarContainer.SetActive(true);
            _bossNameText.text = bossName;
            _bossHealthBar.value = healthPercent;
        }

        public void HideBossHealthBar()
        {
            _bossHealthBarContainer.SetActive(false);
        }

        public void ShowInteractionPrompt(string text, Sprite icon = null)
        {
            _interactionPromptContainer.SetActive(true);
            _interactionPromptText.text = text;
            if (icon != null)
                _interactionPromptIcon.sprite = icon;
        }

        public void HideInteractionPrompt()
        {
            _interactionPromptContainer.SetActive(false);
        }

        public void ShowComboCounter(int step)
        {
            _comboContainer.SetActive(true);
            _comboText.text = step.ToString();
        }

        public void HideComboCounter()
        {
            _comboContainer.SetActive(false);
        }

        public void ShowRunInfo()
        {
            _runInfoOverlay.SetActive(true);
        }

        public void HideRunInfo()
        {
            _runInfoOverlay.SetActive(false);
        }

        #endregion

        #region Event Handlers

        private void OnRelicCollected(RelicCollectedEvent e)
        {
            // Update relic slot display
            if (_relicManager != null)
            {
                int index = _relicManager.RelicCount - 1;
                if (index < _relicSlotIcons.Count)
                {
                    var relic = _relicManager.ActiveRelics[index];
                    if (relic.Definition?.Icon != null)
                    {
                        _relicSlotIcons[index].sprite = relic.Definition.Icon;
                        _relicSlotIcons[index].enabled = true;
                        _relicSlotIcons[index].color = relic.Definition.RelicGlowColor;
                    }
                }
            }
        }

        private void OnBossDefeated(BossDefeatedEvent e)
        {
            HideBossHealthBar();
        }

        private void OnPlayerDeath(PlayerDeathEvent e)
        {
            HideBossHealthBar();
            HideComboCounter();
        }

        #endregion
    }

    /// <summary>
    /// Route selection UI showing preview information for available routes.
    /// Displays encounter danger, possible reward, corruption increase,
    /// elite presence, healing opportunity, and extraction opportunity.
    /// </summary>
    public class RouteSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject _routeSelectionPanel;
        [SerializeField] private Transform _routeButtonContainer;
        [SerializeField] private GameObject _routeButtonPrefab;

        private List<RouteNode> _availableRoutes;

        public void ShowRoutes(List<RouteNode> routes)
        {
            _availableRoutes = routes;
            _routeSelectionPanel.SetActive(true);

            // Clear existing buttons
            foreach (Transform child in _routeButtonContainer)
                Destroy(child.gameObject);

            // Create buttons for each route
            foreach (var route in routes)
            {
                var buttonObj = Instantiate(_routeButtonPrefab, _routeButtonContainer);
                var routeUI = buttonObj.AddComponent<RouteButtonUI>();
                routeUI.Initialize(route);
                buttonObj.GetComponent<Button>().onClick.AddListener(() => SelectRoute(route));
            }
        }

        private void SelectRoute(RouteNode route)
        {
            _routeSelectionPanel.SetActive(false);
            EventBus.Publish(new RouteChoiceEvent
            {
                ChosenRouteIndex = _availableRoutes.IndexOf(route),
                RouteType = route.Type.ToString(),
                RouteInfo = route.GetPreviewText()
            });
        }
    }

    /// <summary>
    /// Individual route button UI component.
    /// </summary>
    public class RouteButtonUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _typeText;
        [SerializeField] private TextMeshProUGUI _dangerText;
        [SerializeField] private TextMeshProUGUI _rewardText;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _infoText;
        [SerializeField] private GameObject _eliteIndicator;
        [SerializeField] private GameObject _healingIndicator;
        [SerializeField] private GameObject _extractionIndicator;
        [SerializeField] private GameObject _unknownIndicator;

        public void Initialize(RouteNode route)
        {
            _typeText.text = route.Type.ToString();
            _dangerText.text = $"Danger: {route.DangerLevel:F1}";
            _rewardText.text = route.IsUnknownEvent ? "Unknown" : route.PossibleReward.ToString();
            _infoText.text = route.PreviewInfo;

            _eliteIndicator.SetActive(route.HasElite);
            _healingIndicator.SetActive(route.HasHealing);
            _extractionIndicator.SetActive(route.IsExtractionPoint);
            _unknownIndicator.SetActive(route.IsUnknownEvent);

            // Color danger text based on level
            _dangerText.color = route.DangerLevel < 2 ? Color.green :
                               route.DangerLevel < 3 ? Color.yellow :
                               route.DangerLevel < 4 ? new Color(1f, 0.5f, 0f) : Color.red;
        }
    }

    /// <summary>
    /// Extraction choice UI showing available extraction options.
    /// </summary>
    public class ExtractionChoiceUI : MonoBehaviour
    {
        [SerializeField] private GameObject _extractionPanel;
        [SerializeField] private Transform _choiceButtonContainer;
        [SerializeField] private GameObject _choiceButtonPrefab;
        [SerializeField] private TextMeshProUGUI _currentResourcesText;
        [SerializeField] private TextMeshProUGUI _corruptionText;
        [SerializeField] private TextMeshProUGUI _extractValueText;
        [SerializeField] private TextMeshProUGUI _continueValueText;

        private ExtractionOptions _options;

        public void ShowExtractionChoices(ExtractionOptions options, float currentResources, float currentCorruption)
        {
            _options = options;
            _extractionPanel.SetActive(true);

            _currentResourcesText.text = $"Resources: {currentResources:F0}";
            _corruptionText.text = $"Corruption: {currentCorruption:F0}%";
            _extractValueText.text = $"Bank {currentResources * options.ExtractRewardMultiplier:F0} resources";
            _continueValueText.text = $"Continue for {options.ContinueRewardMultiplier:F1}x multiplier";

            // Clear and create choice buttons
            foreach (Transform child in _choiceButtonContainer)
                Destroy(child.gameObject);

            foreach (var option in options.GetAvailableOptions())
            {
                var buttonObj = Instantiate(_choiceButtonPrefab, _choiceButtonContainer);
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                text.text = GetOptionDisplayText(option);
                buttonObj.GetComponent<Button>().onClick.AddListener(() => ChooseOption(option));
            }
        }

        private string GetOptionDisplayText(string option)
        {
            return option switch
            {
                "extract" => "🚪 Extract & Bank Rewards",
                "continue" => "⚔ Continue Deeper (1.5x Rewards)",
                "sacrifice_relic" => "📜 Sacrifice a Relic (-15 Corruption)",
                "scar_benefit" => "💀 Accept a Scar for Power (-5 Corruption)",
                "challenge_boss_early" => "👑 Challenge Boss Early",
                "health_to_reward" => "❤ Convert Health to Better Rewards",
                _ => option
            };
        }

        private void ChooseOption(string option)
        {
            _extractionPanel.SetActive(false);
            EventBus.Publish(new ExtractionChosenEvent { ChosenOption = option });
        }
    }

    /// <summary>
    /// Run summary UI displayed after extraction or death.
    /// Shows complete statistics for the run.
    /// </summary>
    public class RunSummaryUI : MonoBehaviour
    {
        [SerializeField] private GameObject _summaryPanel;
        [SerializeField] private TextMeshProUGUI _outcomeText;
        [SerializeField] private TextMeshProUGUI _durationText;
        [SerializeField] private TextMeshProUGUI _roomsText;
        [SerializeField] private TextMeshProUGUI _enemiesText;
        [SerializeField] private TextMeshProUGUI _relicsText;
        [SerializeField] private TextMeshProUGUI _bossesText;
        [SerializeField] private TextMeshProUGUI _corruptionText;
        [SerializeField] private TextMeshProUGUI _resourcesBankedText;
        [SerializeField] private TextMeshProUGUI _resourcesLostText;
        [SerializeField] private TextMeshProUGUI _weaponText;
        [SerializeField] private Transform _relicListContainer;
        [SerializeField] private GameObject _relicEntryPrefab;
        [SerializeField] private TextMeshProUGUI _deathCauseText;
        [SerializeField] private Button _returnToHubButton;

        public void ShowRunSummary(RunData run, bool isSuccessful)
        {
            _summaryPanel.SetActive(true);

            _outcomeText.text = isSuccessful ? "EXTRACTION SUCCESSFUL" : "RUN FAILED";
            _outcomeText.color = isSuccessful ? new Color(0f, 0.9f, 1f) : new Color(0.85f, 0.2f, 0.3f);

            _durationText.text = FormatDuration(run.RunDurationSeconds);
            _roomsText.text = $"Rooms: {run.RoomsCompleted}";
            _enemiesText.text = $"Enemies Killed: {run.EnemiesKilled}";
            _relicsText.text = $"Relics: {run.RelicsCollected.Count}";
            _bossesText.text = $"Bosses: {run.BossesDefeated}";
            _corruptionText.text = $"Peak Corruption: {run.Corruption:F0}%";
            _weaponText.text = $"Weapon: {run.WeaponId}";

            if (isSuccessful)
            {
                _resourcesBankedText.text = $"Banked: {run.ResourcesBanked:F0} ◆";
                _resourcesLostText.text = "";
                _deathCauseText.text = "";
            }
            else
            {
                _resourcesBankedText.text = $"Banked: {run.ResourcesBanked:F0} ◆";
                _resourcesLostText.text = $"Lost: {(run.ResourcesEarned - run.ResourcesBanked):F0} ◆";
                _deathCauseText.text = $"Died from: {run.DeathCause}";
            }

            // Display collected relics
            foreach (Transform child in _relicListContainer)
                Destroy(child.gameObject);

            foreach (var relicId in run.RelicsCollected)
            {
                var entry = Instantiate(_relicEntryPrefab, _relicListContainer);
                var text = entry.GetComponentInChildren<TextMeshProUGUI>();
                text.text = relicId;
            }

            _returnToHubButton.onClick.AddListener(() => ReturnToHub());
        }

        private string FormatDuration(float seconds)
        {
            int mins = (int)(seconds / 60f);
            int secs = (int)(seconds % 60f);
            return $"{mins}:{secs:D2}";
        }

        private void ReturnToHub()
        {
            _summaryPanel.SetActive(false);
            Core.GameManager.Instance?.ReturnToHub();
        }
    }

    /// <summary>
    /// Pause menu with settings, controls, and run info access.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _runInfoButton;
        [SerializeField] private Button _quitRunButton;
        [SerializeField] private SettingsUI _settingsUI;

        private void Start()
        {
            _resumeButton.onClick.AddListener(Resume);
            _settingsButton.onClick.AddListener(OpenSettings);
            _runInfoButton.onClick.AddListener(OpenRunInfo);
            _quitRunButton.onClick.AddListener(QuitRun);
        }

        public void ShowPauseMenu()
        {
            _pausePanel.SetActive(true);
        }

        public void HidePauseMenu()
        {
            _pausePanel.SetActive(false);
        }

        private void Resume()
        {
            HidePauseMenu();
            Core.GameManager.Instance?.TogglePause();
        }

        private void OpenSettings()
        {
            _settingsUI.ShowSettings();
        }

        private void OpenRunInfo()
        {
            // Show run info overlay
        }

        private void QuitRun()
        {
            // Confirm and quit current run (counts as a failed extraction)
            HidePauseMenu();
            EventBus.Publish(new PlayerDeathEvent
            {
                DeathPosition = Vector3.zero,
                DeathCause = "voluntary_quit",
                RunDurationSeconds = Core.GameManager.Instance?.CurrentRun?.RunDurationSeconds ?? 0
            });
        }
    }

    /// <summary>
    /// Settings UI with all configurable options.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [SerializeField] private GameObject _settingsPanel;

        // Volume sliders
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private Slider _ambienceVolumeSlider;

        // Display settings
        [SerializeField] private TMP_Dropdown _fullscreenDropdown;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private Slider _renderScaleSlider;
        [SerializeField] private Toggle _vsyncToggle;
        [SerializeField] private TMP_Dropdown _qualityDropdown;
        [SerializeField] private TMP_Dropdown _shadowDropdown;
        [SerializeField] private TMP_Dropdown _antiAliasingDropdown;
        [SerializeField] private Toggle _motionBlurToggle;
        [SerializeField] private Toggle _chromaticAberrationToggle;

        // Gameplay settings
        [SerializeField] private Slider _screenShakeSlider;
        [SerializeField] private Toggle _vibrationToggle;
        [SerializeField] private Slider _aimAssistSlider;
        [SerializeField] private TMP_Dropdown _textSizeDropdown;
        [SerializeField] private Toggle _subtitlesToggle;
        [SerializeField] private Toggle _highContrastToggle;
        [SerializeField] private TMP_Dropdown _colorblindDropdown;

        // Controls
        [SerializeField] private Button _rebindControlsButton;
        [SerializeField] private Button _resetBindingsButton;

        [SerializeField] private Button _backButton;

        private void Start()
        {
            _backButton.onClick.AddListener(CloseSettings);
            _masterVolumeSlider.onValueChanged.AddListener(v => EventBus.Publish(new SettingsChangedEvent { SettingName = "master_volume", NewValue = v.ToString() }));
            _sfxVolumeSlider.onValueChanged.AddListener(v => EventBus.Publish(new SettingsChangedEvent { SettingName = "sfx_volume", NewValue = v.ToString() }));
            _screenShakeSlider.onValueChanged.AddListener(v => CameraShake.SetGlobalIntensity(v));
        }

        public void ShowSettings()
        {
            _settingsPanel.SetActive(true);
            LoadCurrentSettings();
        }

        public void CloseSettings()
        {
            _settingsPanel.SetActive(false);
            ApplySettings();
            SaveSettings();
        }

        private void LoadCurrentSettings()
        {
            // Load settings from SaveManager
            var settings = Core.GameManager.Instance?.GetComponent<SaveManager>()?.CurrentData?.Settings;
            if (settings != null)
            {
                _masterVolumeSlider.value = settings.Value.MasterVolume;
                _musicVolumeSlider.value = settings.Value.MusicVolume;
                _sfxVolumeSlider.value = settings.Value.SFXVolume;
                _screenShakeSlider.value = settings.Value.ScreenShakeIntensity;
                _vibrationToggle.isOn = settings.Value.VibrationEnabled;
                _aimAssistSlider.value = settings.Value.AimAssistStrength;
            }
        }

        private void ApplySettings()
        {
            // Apply all settings to game systems
            var sfxManager = FindObjectOfType<SFXManager>();
            if (sfxManager != null)
                sfxManager.SetMasterVolume(_masterVolumeSlider.value);
        }

        private void SaveSettings()
        {
            // Save settings through SaveManager
        }
    }

    /// <summary>
    /// Hub UI for weapon selection, upgrades, and permanent progression.
    /// </summary>
    public class HubUI : MonoBehaviour
    {
        [SerializeField] private GameObject _hubPanel;
        [SerializeField] private Button _startRunButton;
        [SerializeField] private TMP_Dropdown _realmDropdown;
        [SerializeField] private Transform _weaponSelectContainer;
        [SerializeField] private Transform _upgradeContainer;
        [SerializeField] private Transform _relicArchiveContainer;
        [SerializeField] private Button _trainingButton;
        [SerializeField] private Transform _npcContainer;
        [SerializeField] private TextMeshProUGUI _currencyText;

        private void Start()
        {
            _startRunButton.onClick.AddListener(StartRun);
            _trainingButton.onClick.AddListener(OpenTraining);
        }

        public void ShowHub()
        {
            _hubPanel.SetActive(true);
            UpdateHubState();
        }

        public void HideHub()
        {
            _hubPanel.SetActive(false);
        }

        private void UpdateHubState()
        {
            var progression = Core.GameManager.Instance?.Progression;
            if (progression != null)
            {
                _currencyText.text = $"◆ {progression.Currency:F0}";
            }
        }

        private void StartRun()
        {
            string realm = _realmDropdown.options[_realmDropdown.value].text;
            // Get selected weapon
            Core.GameManager.Instance?.StartRun(realm, "chain_blade");
            HideHub();
        }

        private void OpenTraining()
        {
            // Open training area scene
        }
    }

    /// <summary>
    /// Relic reward selection UI (shown after room completion).
    /// </summary>
    public class RelicRewardUI : MonoBehaviour
    {
        [SerializeField] private GameObject _rewardPanel;
        [SerializeField] private Transform _relicChoiceContainer;
        [SerializeField] private GameObject _relicChoicePrefab;

        private List<RelicDefinition> _choices;

        public void ShowRelicChoices(List<RelicDefinition> relics)
        {
            _choices = relics;
            _rewardPanel.SetActive(true);

            foreach (Transform child in _relicChoiceContainer)
                Destroy(child.gameObject);

            foreach (var relic in relics)
            {
                var choiceObj = Instantiate(_relicChoicePrefab, _relicChoiceContainer);
                var choiceUI = choiceObj.AddComponent<RelicChoiceUI>();
                choiceUI.Initialize(relic);
                choiceObj.GetComponent<Button>().onClick.AddListener(() => SelectRelic(relic));
            }
        }

        private void SelectRelic(RelicDefinition relic)
        {
            _rewardPanel.SetActive(false);
            var relicManager = FindObjectOfType<RelicManager>();
            relicManager?.CollectRelic(relic);
        }
    }

    /// <summary>
    /// Individual relic choice UI component showing benefit and corruption.
    /// </summary>
    public class RelicChoiceUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _rarityText;
        [SerializeField] private TextMeshProUGUI _benefitText;
        [SerializeField] private TextMeshProUGUI _corruptionText;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _border;

        public void Initialize(RelicDefinition relic)
        {
            _nameText.text = relic.RelicName;
            _rarityText.text = relic.Rarity.ToString();
            _benefitText.text = relic.BenefitDescription;
            _corruptionText.text = $"⚠ {relic.CorruptionEffectDescription} (+{relic.CorruptionIncrease}% Corruption)";

            // Color border based on rarity
            _border.color = relic.Rarity switch
            {
                RelicRarity.Common => new Color(0.6f, 0.6f, 0.6f),
                RelicRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),
                RelicRarity.Rare => new Color(0.2f, 0.4f, 0.9f),
                RelicRarity.Epic => new Color(0.7f, 0.3f, 0.9f),
                RelicRarity.Legendary => new Color(1f, 0.8f, 0f),
                RelicRarity.Cursed => new Color(0.5f, 0f, 0.5f),
                _ => Color.gray
            };
        }
    }
}
