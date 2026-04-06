using System;
using ModManagerSettings.Api;
using MegaCrit.Sts2.Core.Logging;

namespace BetterMapTools.Features.Settings;

internal static class RoutingSettingsRegistration
{
    private const string ModKey = "BetterMapTools";
    private const string LegacyModKey = "RoutingHelper";
    private const string UseSeparateResultsPanelKey = "use_separate_results_panel";
    private const string DrawingColorKey = "drawing_color";
    private const string ActivePresetKey = "preset_active_name";
    private const string PresetJsonKeyPrefix = "preset_json_";
    private const string PresetColorKeyPrefix = "preset_color_";
    private static bool _hydratedFromPersistedValues;

    public static void Register()
    {
        RoutingSettings.EnsureDefaultsInitialized();
        TryHydrateFromPersistedValues();

        ModSettingsRegistry.UpsertRegistration(new ModSettingsRegistration
        {
            ModPckName = ModKey,
            DisplayName = "BetterMapTools",
            Description = "Map tools host with route solver presets and route overlay drawing.",
            ExplorerDescription = "Route solver presets are editable under Settings/Preset/Presets/*, including per-preset route colors.",
            ShowSettingsButtonInModdingMenu = true,
            OnApply = null,
            OnRestoreDefaults = () =>
            {
                RoutingSettings.ResetAllToDefaults();
                RefreshPresetSettings();
            },
            ToggleSettings = new[]
            {
                new ModSettingToggleDefinition
                {
                    Key = UseSeparateResultsPanelKey,
                    Label = "Separate Results Panel",
                    Description = "If enabled, solver result table uses its own panel. If disabled, results are shown inline in the options scroll.",
                    Path = "Settings/Visuals",
                    AllowMultiplayerOverwrite = false,
                    DefaultValue = RoutingSettings.DefaultUseSeparateResultsPanel,
                    GetCurrentValue = () => RoutingSettings.UseSeparateResultsPanel,
                    OnApply = value =>
                    {
                        RoutingSettings.SetUseSeparateResultsPanel(value);
                    }
                }
            },
            ColorSettings = new[]
            {
                new ModSettingColorDefinition
                {
                    Key = DrawingColorKey,
                    Label = "Drawing Color",
                    Description = "Override color used for your map pencil tool. Leave empty to use your character's default color.",
                    Path = "Settings/Drawing",
                    AllowMultiplayerOverwrite = false,
                    PlaceholderText = "character default",
                    DefaultValue = string.Empty,
                    GetCurrentValue = () => RoutingSettings.SavedDrawingColorRaw ?? string.Empty,
                    OnApply = value =>
                    {
                        RoutingSettings.SetSavedDrawingColorRaw(value);
                    }
                }
            }
        });

        RefreshPresetSettings();
    }

    public static void EnsureHydratedAndRefreshIfNeeded()
    {
        if (_hydratedFromPersistedValues)
        {
            return;
        }

        if (!TryHydrateFromPersistedValues())
        {
            return;
        }

        RefreshPresetSettings();
    }

    public static void RefreshPresetSettings()
    {
        if (!ModSettingsRegistry.TryGet(ModKey, out _))
        {
            return;
        }

        foreach (var definition in ModSettingsRegistry.GetAllSettings(ModKey))
        {
            if (definition.Key.Equals(ActivePresetKey, StringComparison.OrdinalIgnoreCase) ||
                definition.Key.StartsWith(PresetJsonKeyPrefix, StringComparison.OrdinalIgnoreCase) ||
                definition.Key.StartsWith(PresetColorKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                ModSettingsRegistry.RemoveSetting(ModKey, definition.Key);
            }
        }

        var presetNames = RoutingSettings.GetPresetNames();
        ModSettingsRegistry.UpsertSetting(ModKey, new ModSettingChoiceDefinition
        {
            Key = ActivePresetKey,
            Label = "Active Preset",
            Description = "Preset loaded by default in map routing popup.",
            Path = "Settings/Preset/Default",
            AllowMultiplayerOverwrite = false,
            Options = presetNames,
            DefaultValue = RoutingSettings.DefaultPresetName,
            GetCurrentValue = () => RoutingSettings.ActivePresetName,
            OnApply = value =>
            {
                if (RoutingSettings.SetActivePreset(value))
                {
                }
            }
        });

        foreach (var name in presetNames)
        {
            var pathName = RoutingSettings.PathSafePresetName(name);
            var pathKey = pathName.ToLowerInvariant();

            ModSettingsRegistry.UpsertSetting(ModKey, new ModSettingColorDefinition
            {
                Key = PresetColorKeyPrefix + pathKey,
                Label = $"Route Color ({name})",
                Description = "Color used for this preset's route lines. Format: #RRGGBB, #RRGGBBAA, or r,g,b,a.",
                Path = $"Settings/Preset/Presets/{pathName}",
                AllowMultiplayerOverwrite = false,
                PlaceholderText = RoutingSettings.DefaultHighlightColor,
                DefaultValue = RoutingSettings.DefaultHighlightColor,
                GetCurrentValue = () => RoutingSettings.GetPresetHighlightColorRaw(name),
                OnApply = value =>
                {
                    if (RoutingSettings.SetPresetHighlightColorRaw(name, value))
                    {
                    }
                }
            });

            ModSettingsRegistry.UpsertSetting(ModKey, new ModSettingTextDefinition
            {
                Key = PresetJsonKeyPrefix + pathKey,
                Label = $"Preset JSON ({name})",
                Description = "Advanced: edit JSON then press Apply to mutate this preset.",
                Path = $"Settings/Preset/Presets/{pathName}",
                AllowMultiplayerOverwrite = false,
                DefaultValue = RoutingSettings.SerializePresetToJson(name),
                GetCurrentValue = () => RoutingSettings.SerializePresetToJson(name),
                PlaceholderText = "{\"Name\":\"Preset\",\"Metrics\":{...}}",
                OnApply = json =>
                {
                    if (RoutingSettings.ApplyPresetJson(name, json))
                    {
                        RefreshPresetSettings();
                    }
                    else
                    {
                        Log.Warn($"[BetterMapTools] Invalid preset JSON for '{name}'.");
                    }
                }
            });
        }

        if (ModSettingsRegistry.IsPersistenceReady())
        {
            ModSettingsRegistry.PersistCurrentRegistrationValues(ModKey);
        }
    }

    public static void PersistCurrentValuesIfReady()
    {
        if (!ModSettingsRegistry.IsPersistenceReady())
        {
            return;
        }

        ModSettingsRegistry.PersistCurrentRegistrationValues(ModKey);
    }

    private static bool TryHydrateFromPersistedValues()
    {
        if (_hydratedFromPersistedValues)
        {
            return true;
        }

        if (!ModSettingsRegistry.IsPersistenceReady())
        {
            return false;
        }

        var persisted = ModSettingsRegistry.GetPersistedSettingValues(ModKey);
        if (persisted.Count == 0)
        {
            persisted = ModSettingsRegistry.GetPersistedSettingValues(LegacyModKey);
        }
        if (persisted.Count == 0)
        {
            _hydratedFromPersistedValues = true;
            return true;
        }

        foreach (var pair in persisted)
        {
            if (!pair.Key.StartsWith(PresetJsonKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var keySuffix = pair.Key[PresetJsonKeyPrefix.Length..];
            var resolvedName = ResolvePresetNameFromPersistedKeySuffix(keySuffix);
            RoutingSettings.ApplyPresetJson(resolvedName, pair.Value);
        }

        foreach (var pair in persisted)
        {
            if (!pair.Key.StartsWith(PresetColorKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var keySuffix = pair.Key[PresetColorKeyPrefix.Length..];
            var resolvedName = ResolvePresetNameFromPersistedKeySuffix(keySuffix);
            RoutingSettings.SetPresetHighlightColorRaw(resolvedName, pair.Value);
        }

        if (persisted.TryGetValue(ActivePresetKey, out var activePreset) && !string.IsNullOrWhiteSpace(activePreset))
        {
            RoutingSettings.SetActivePreset(activePreset);
        }

        if (persisted.TryGetValue(UseSeparateResultsPanelKey, out var separateResultsPanelRaw) &&
            bool.TryParse(separateResultsPanelRaw, out var separateResultsPanel))
        {
            RoutingSettings.SetUseSeparateResultsPanel(separateResultsPanel);
        }

        if (persisted.TryGetValue(DrawingColorKey, out var drawingColorRaw))
        {
            RoutingSettings.SetSavedDrawingColorRaw(drawingColorRaw);
        }

        _hydratedFromPersistedValues = true;
        return true;
    }

    private static string ResolvePresetNameFromPersistedKeySuffix(string keySuffix)
    {
        var safeSuffix = keySuffix ?? string.Empty;
        var normalizedSuffix = safeSuffix.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSuffix))
        {
            return safeSuffix;
        }

        foreach (var presetName in RoutingSettings.GetPresetNames())
        {
            var presetKey = RoutingSettings.PathSafePresetName(presetName).ToLowerInvariant();
            if (presetKey == normalizedSuffix)
            {
                return presetName;
            }
        }

        return safeSuffix;
    }
}
