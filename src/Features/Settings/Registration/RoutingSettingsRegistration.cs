using System;
using ModManagerSettings.Api;
using MegaCrit.Sts2.Core.Logging;

namespace RoutingHelper.Features.Settings;

internal static class RoutingSettingsRegistration
{
    private const string ModKey = "RoutingHelper";
    private const string ActivePresetKey = "preset_active_name";
    private const string PresetJsonKeyPrefix = "preset_json_";
    private static bool _hydratedFromPersistedValues;

    public static void Register()
    {
        RoutingSettings.EnsureDefaultsInitialized();
        TryHydrateFromPersistedValues();

        ModSettingsRegistry.UpsertRegistration(new ModSettingsRegistration
        {
            ModPckName = ModKey,
            DisplayName = "RoutingHelper",
            Description = "Route highlighting and route-solver preset management.",
            ExplorerDescription = "Routing presets are editable objects under Settings/Preset/Presets/*.",
            ColorSettings =
            [
                new ModSettingColorDefinition
                {
                    Key = "route_highlight_color",
                    Label = "Route Highlight Color",
                    Description = "Used for highlighted route nodes and lines. Format: #RRGGBB, #RRGGBBAA, or r,g,b,a.",
                    Path = "Settings/Visuals",
                    PlaceholderText = RoutingSettings.DefaultHighlightColor,
                    DefaultValue = RoutingSettings.DefaultHighlightColor,
                    GetCurrentValue = () => RoutingSettings.HighlightColorRaw,
                    OnApply = value =>
                    {
                        RoutingSettings.HighlightColorRaw = value;
                        Log.Info($"[RoutingHelper] Setting applied: route_highlight_color='{value}'.");
                    }
                }
            ],
            OnApply = () =>
            {
                Log.Info($"[RoutingHelper] Registration apply: route_highlight_color='{RoutingSettings.HighlightColorRaw}', active_preset='{RoutingSettings.ActivePresetName}'.");
            },
            OnRestoreDefaults = () =>
            {
                RoutingSettings.ResetAllToDefaults();
                RefreshPresetSettings();
                Log.Info("[RoutingHelper] Restored all settings to defaults.");
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
        Log.Info("[RoutingHelper] Late hydration completed after profile-scoped persistence became ready.");
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
                definition.Key.StartsWith(PresetJsonKeyPrefix, StringComparison.OrdinalIgnoreCase))
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
            Options = presetNames,
            DefaultValue = RoutingSettings.DefaultPresetName,
            GetCurrentValue = () => RoutingSettings.ActivePresetName,
            OnApply = value =>
            {
                if (RoutingSettings.SetActivePreset(value))
                {
                    Log.Info($"[RoutingHelper] Applied active preset: '{value}'.");
                }
            }
        });

        foreach (var name in presetNames)
        {
            var pathName = RoutingSettings.PathSafePresetName(name);
            ModSettingsRegistry.UpsertSetting(ModKey, new ModSettingTextDefinition
            {
                Key = PresetJsonKeyPrefix + pathName.ToLowerInvariant(),
                Label = $"Preset JSON ({name})",
                Description = "Advanced: edit JSON then press Apply to mutate this preset.",
                Path = $"Settings/Preset/Presets/{pathName}",
                DefaultValue = RoutingSettings.SerializePresetToJson(name),
                GetCurrentValue = () => RoutingSettings.SerializePresetToJson(name),
                PlaceholderText = "{\"Name\":\"Preset\",\"Metrics\":{...}}",
                OnApply = json =>
                {
                    if (RoutingSettings.ApplyPresetJson(name, json))
                    {
                        Log.Info($"[RoutingHelper] Applied preset JSON for '{name}'.");
                        RefreshPresetSettings();
                    }
                    else
                    {
                        Log.Warn($"[RoutingHelper] Invalid preset JSON for '{name}'.");
                    }
                }
            });
        }

        if (ModSettingsRegistry.IsPersistenceReady())
        {
            ModSettingsRegistry.PersistCurrentRegistrationValues(ModKey);
        }
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
            _hydratedFromPersistedValues = true;
            return true;
        }

        if (persisted.TryGetValue("route_highlight_color", out var color) && !string.IsNullOrWhiteSpace(color))
        {
            RoutingSettings.HighlightColorRaw = color;
        }

        foreach (var pair in persisted)
        {
            if (!pair.Key.StartsWith(PresetJsonKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fallbackName = pair.Key[PresetJsonKeyPrefix.Length..];
            RoutingSettings.ApplyPresetJson(fallbackName, pair.Value);
        }

        if (persisted.TryGetValue(ActivePresetKey, out var activePreset) && !string.IsNullOrWhiteSpace(activePreset))
        {
            RoutingSettings.SetActivePreset(activePreset);
        }

        _hydratedFromPersistedValues = true;
        return true;
    }
}
