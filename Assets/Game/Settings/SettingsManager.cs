using UnityEngine;
using UnityEngine.Rendering.Universal;
using Relicfall.Core.Events;
using Relicfall.Saving;

namespace Relicfall.Settings
{
    /// <summary>
    /// Settings manager with full accessibility and configuration support.
    /// Handles graphics, audio, controls, accessibility, and resolution settings.
    /// All settings are saved and persisted.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public SettingsSaveData CurrentSettings { get; private set; }

        private SaveManager _saveManager;
        private UnityEngine.Camera _mainCamera;
        private Light _mainLight;
        private UniversalAdditionalCameraData _cameraData;

        public System.Action<SettingsSaveData> OnSettingsChanged;

        private void Awake()
        {
            _saveManager = GetComponent<SaveManager>();
            CurrentSettings = new SettingsSaveData();
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera != null)
                _cameraData = _mainCamera.GetUniversalAdditionalCameraData();
        }

        /// <summary>
        /// Apply saved settings to the game.
        /// </summary>
        public void ApplySettings(SettingsSaveData settings)
        {
            CurrentSettings = settings;

            // Resolution
            SetResolution(settings.ScreenWidth, settings.ScreenHeight, settings.FullscreenMode);

            // VSync
            QualitySettings.vSyncCount = settings.VSyncCount;

            // Target FPS
            Application.targetFrameRate = settings.TargetFPS;

            // Graphics quality
            SetGraphicsQuality(settings.GraphicsQuality);

            // Shadows
            SetShadowQuality(settings.ShadowQuality);

            // Anti-aliasing
            SetAntiAliasing(settings.AntiAliasing);

            // Render scale
            SetRenderScale(settings.RenderScale);

            // Motion blur
            SetMotionBlur(settings.MotionBlur);

            // Chromatic aberration
            SetChromaticAberration(settings.ChromaticAberration);

            // Volume
            SetMasterVolume(settings.MasterVolume);
            SetMusicVolume(settings.MusicVolume);
            SetSFXVolume(settings.SFXVolume);
            SetAmbienceVolume(settings.AmbienceVolume);

            // Screen shake
            Combat.CameraShake.SetGlobalIntensity(settings.ScreenShakeIntensity);

            // Vibration
            Core.GameManager.Instance?.SetVibration(settings.VibrationEnabled);

            OnSettingsChanged?.Invoke(settings);
        }

        #region Resolution & Display

        public void SetResolution(int width, int height, int fullscreenMode)
        {
            FullScreenMode mode = fullscreenMode == 0 ? FullScreenMode.Windowed :
                                  fullscreenMode == 1 ? FullScreenMode.FullScreenWindow :
                                  FullScreenMode.MaximizedWindow;

            Screen.SetResolution(width, height, mode);
            CurrentSettings.ScreenWidth = width;
            CurrentSettings.ScreenHeight = height;
            CurrentSettings.FullscreenMode = fullscreenMode;
        }

        public void SetVSync(int count)
        {
            QualitySettings.vSyncCount = count;
            CurrentSettings.VSyncCount = count;
        }

        public void SetTargetFPS(int fps)
        {
            Application.targetFrameRate = fps;
            CurrentSettings.TargetFPS = fps;
        }

        #endregion

        #region Graphics

        public void SetGraphicsQuality(int level)
        {
            QualitySettings.SetQualityLevel(level, true);
            CurrentSettings.GraphicsQuality = level;
        }

        public void SetShadowQuality(int level)
        {
            switch (level)
            {
                case 0: // No shadows
                    QualitySettings.shadows = 0;
                    break;
                case 1: // Hard shadows only
                    QualitySettings.shadows = 1;
                    QualitySettings.shadowResolution = 0;
                    break;
                case 2: // Soft shadows, medium
                    QualitySettings.shadows = 2;
                    QualitySettings.shadowResolution = 1;
                    break;
                case 3: // Soft shadows, high
                    QualitySettings.shadows = 2;
                    QualitySettings.shadowResolution = 2;
                    break;
            }
            CurrentSettings.ShadowQuality = level;
        }

        public void SetAntiAliasing(int level)
        {
            if (_cameraData != null)
            {
                _cameraData.antialiasing = level switch
                {
                    0 => AntialiasingMode.None,
                    1 => AntialiasingMode.FastApproximateAntialiasing,
                    2 => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    4 => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    _ => AntialiasingMode.None
                };
                _cameraData.antialiasingQuality = AntialiasingQuality.High;
            }
            CurrentSettings.AntiAliasing = level;
        }

        public void SetRenderScale(float scale)
        {
            if (_cameraData != null)
                _cameraData.renderScale = scale;
            CurrentSettings.RenderScale = scale;
        }

        public void SetMotionBlur(bool enabled)
        {
            CurrentSettings.MotionBlur = enabled;
        }

        public void SetChromaticAberration(bool enabled)
        {
            CurrentSettings.ChromaticAberration = enabled;
        }

        public void SetEffectsQuality(int level)
        {
            CurrentSettings.EffectsQuality = level;
        }

        #endregion

        #region Audio

        public void SetMasterVolume(float volume)
        {
            AudioListener.volume = volume;
            CurrentSettings.MasterVolume = volume;
        }

        public void SetMusicVolume(float volume)
        {
            CurrentSettings.MusicVolume = volume;
            // Applied by MusicSystem
        }

        public void SetSFXVolume(float volume)
        {
            CurrentSettings.SFXVolume = volume;
            // Applied by SFXManager
        }

        public void SetAmbienceVolume(float volume)
        {
            CurrentSettings.AmbienceVolume = volume;
        }

        #endregion

        #region Accessibility

        public void SetScreenShake(float intensity)
        {
            Combat.CameraShake.SetGlobalIntensity(intensity);
            CurrentSettings.ScreenShakeIntensity = intensity;
        }

        public void SetVibration(bool enabled)
        {
            CurrentSettings.VibrationEnabled = enabled;
        }

        public void SetAimAssist(float strength)
        {
            CurrentSettings.AimAssistStrength = strength;
        }

        public void SetTextSize(int size)
        {
            CurrentSettings.TextSize = size;
            // Applied by UI system
        }

        public void SetSubtitles(bool enabled)
        {
            CurrentSettings.SubtitlesEnabled = enabled;
        }

        public void SetHighContrastTelegraphs(bool enabled)
        {
            CurrentSettings.HighContrastTelegraphs = enabled;
        }

        public void SetColorblindMode(int type)
        {
            CurrentSettings.ColorblindType = type;
            CurrentSettings.ColorblindMode = type > 0;
        }

        #endregion

        /// <summary>
        /// Save current settings to disk.
        /// </summary>
        public void SaveSettings()
        {
            if (_saveManager != null && _saveManager.CurrentData != null)
            {
                _saveManager.CurrentData.Settings = CurrentSettings;
                _saveManager.SaveGame(_saveManager.CurrentData);
            }
        }

        /// <summary>
        /// Get a graphics quality preset string for display.
        /// </summary>
        public string GetGraphicsQualityName()
        {
            return CurrentSettings.GraphicsQuality switch
            {
                0 => "Low",
                1 => "Medium",
                2 => "High",
                _ => "Custom"
            };
        }

        /// <summary>
        /// Get all available resolutions.
        /// </summary>
        public Resolution[] GetAvailableResolutions()
        {
            return Screen.resolutions;
        }
    }
}
