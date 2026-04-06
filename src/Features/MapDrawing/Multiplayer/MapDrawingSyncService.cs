using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using BetterMapTools.Features.Settings;

namespace BetterMapTools.Features.MapDrawing.Multiplayer;

internal static class MapDrawingSyncService
{
    private static readonly object Sync = new();
    private static readonly Dictionary<INetGameService, MessageHandlerDelegate<MapDrawingColorOverrideMessage>> ColorHandlers = [];
    private static readonly Dictionary<INetGameService, MessageHandlerDelegate<MapDrawingColorSnapshotMessage>> SnapshotHandlers = [];
    private static readonly Dictionary<INetGameService, MessageHandlerDelegate<MapDrawingUndoMessage>> UndoHandlers = [];
    private static readonly Dictionary<INetGameService, Action<NetErrorInfo>> DisconnectHandlers = [];

    public static void EnsureAttached(INetGameService netService, string source)
    {
        lock (Sync)
        {
            if (ColorHandlers.ContainsKey(netService))
            {
                return;
            }

            MessageHandlerDelegate<MapDrawingColorOverrideMessage> colorHandler = (message, senderId) =>
                HandleColorOverride(netService, message, senderId);
            MessageHandlerDelegate<MapDrawingColorSnapshotMessage> snapshotHandler = (message, senderId) =>
                HandleColorSnapshot(netService, message, senderId);
            MessageHandlerDelegate<MapDrawingUndoMessage> undoHandler = (message, senderId) =>
                HandleUndo(netService, message, senderId);
            Action<NetErrorInfo> disconnectedHandler = _ => OnDisconnected(netService);

            ColorHandlers[netService] = colorHandler;
            SnapshotHandlers[netService] = snapshotHandler;
            UndoHandlers[netService] = undoHandler;
            DisconnectHandlers[netService] = disconnectedHandler;

            netService.RegisterMessageHandler(colorHandler);
            netService.RegisterMessageHandler(snapshotHandler);
            netService.RegisterMessageHandler(undoHandler);
            netService.Disconnected += disconnectedHandler;
        }

    }

    public static void BroadcastLocalColor(string source)
    {
        if (!TryGetMultiplayerNetService(out var netService))
        {
            return;
        }

        EnsureAttached(netService, source);
        var message = new MapDrawingColorOverrideMessage();
        if (MapDrawingColorOverrideService.TryGetLocalOverride(out var overrideColor))
        {
            message.HasOverride = true;
            message.ColorRaw = MapDrawingColorOverrideService.ToColorRaw(overrideColor);
        }

        netService.SendMessage(message);
    }

    public static void BroadcastUndo(int lineCount, string source)
    {
        if (lineCount <= 0 || !TryGetMultiplayerNetService(out var netService))
        {
            return;
        }

        EnsureAttached(netService, source);
        netService.SendMessage(new MapDrawingUndoMessage
        {
            LineCount = lineCount
        });
    }

    public static void SendSnapshotTo(INetGameService netService, ulong targetPlayerId, string source)
    {
        if (netService.Type != NetGameType.Host || !netService.IsConnected)
        {
            return;
        }

        EnsureAttached(netService, source);
        var message = new MapDrawingColorSnapshotMessage
        {
            Values = MapDrawingColorOverrideService.BuildSnapshot().ToList()
        };
        netService.SendMessage(message, targetPlayerId);
    }

    private static void HandleColorOverride(INetGameService netService, MapDrawingColorOverrideMessage message, ulong senderId)
    {
        if (senderId == netService.NetId)
        {
            return;
        }

        if (message.HasOverride)
        {
            if (!RoutingSettings.TryResolveColor(message.ColorRaw, out var color))
            {
                Log.Warn($"[BetterMapTools] Ignoring invalid drawing color '{message.ColorRaw}' from player {senderId}.");
                return;
            }

            MapDrawingColorOverrideService.ApplyRemoteOverride(senderId, color);
            return;
        }

        MapDrawingColorOverrideService.ApplyRemoteOverride(senderId, null);
    }

    private static void HandleColorSnapshot(INetGameService netService, MapDrawingColorSnapshotMessage message, ulong senderId)
    {
        if (netService.Type != NetGameType.Client)
        {
            return;
        }

        MapDrawingColorOverrideService.ClearRemoteOverridesExceptLocal();
        foreach (var value in message.Values ?? [])
        {
            if (value.PlayerId == netService.NetId)
            {
                continue;
            }

            if (!value.HasOverride)
            {
                MapDrawingColorOverrideService.ApplyRemoteOverride(value.PlayerId, null);
                continue;
            }

            if (!RoutingSettings.TryResolveColor(value.ColorRaw, out var color))
            {
                Log.Warn($"[BetterMapTools] Ignoring invalid snapshot drawing color '{value.ColorRaw}' for player {value.PlayerId}.");
                continue;
            }

            MapDrawingColorOverrideService.ApplyRemoteOverride(value.PlayerId, color);
        }

    }

    private static void HandleUndo(INetGameService netService, MapDrawingUndoMessage message, ulong senderId)
    {
        if (senderId == netService.NetId)
        {
            return;
        }

        var drawings = NMapScreen.Instance?.Drawings;
        if (drawings == null || message.LineCount <= 0)
        {
            return;
        }

        var removed = MapDrawingReflection.RemoveLastLines(drawings, senderId, message.LineCount);
    }

    private static void OnDisconnected(INetGameService netService)
    {
        MapDrawingColorOverrideService.ClearRemoteOverridesExceptLocal();
    }

    private static bool TryGetMultiplayerNetService(out INetGameService netService)
    {
        netService = null!;
        var runManager = RunManager.Instance;
        if (!runManager.IsInProgress)
        {
            return false;
        }

        var candidate = runManager.NetService;
        if (!candidate.IsConnected || candidate.Type == NetGameType.Singleplayer)
        {
            return false;
        }

        netService = candidate;
        return true;
    }
}

[HarmonyPatch(typeof(NetClientGameService))]
internal static class NetClientGameServiceAttachMapDrawingPatch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    public static void Postfix(NetClientGameService __instance)
    {
        MapDrawingSyncService.EnsureAttached(__instance, "NetClientGameService::.ctor");
    }
}

[HarmonyPatch(typeof(NetHostGameService))]
internal static class NetHostGameServiceAttachMapDrawingPatch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    public static void Postfix(NetHostGameService __instance)
    {
        MapDrawingSyncService.EnsureAttached(__instance, "NetHostGameService::.ctor");
    }
}

[HarmonyPatch(typeof(StartRunLobby), "HandleClientLobbyJoinRequestMessage")]
internal static class StartRunLobbyJoinDrawingColorSnapshotPatch
{
    [HarmonyPostfix]
    public static void Postfix(StartRunLobby __instance, ulong senderId)
    {
        if (__instance.NetService.Type != NetGameType.Host || !__instance.Players.Exists(player => player.id == senderId))
        {
            return;
        }

        MapDrawingSyncService.SendSnapshotTo(__instance.NetService, senderId, "StartRunLobby join");
    }
}

[HarmonyPatch(typeof(LoadRunLobby), "HandleClientLoadJoinRequestMessage")]
internal static class LoadRunLobbyJoinDrawingColorSnapshotPatch
{
    [HarmonyPostfix]
    public static void Postfix(LoadRunLobby __instance, ulong senderId)
    {
        if (__instance.NetService.Type != NetGameType.Host || !__instance.ConnectedPlayerIds.Contains(senderId))
        {
            return;
        }

        MapDrawingSyncService.SendSnapshotTo(__instance.NetService, senderId, "LoadRunLobby join");
    }
}

[HarmonyPatch(typeof(RunLobby), "HandleClientRejoinRequestMessage")]
internal static class RunLobbyRejoinDrawingColorSnapshotPatch
{
    [HarmonyPostfix]
    public static void Postfix(RunLobby __instance, ulong senderId)
    {
        var netService = Traverse.Create(__instance).Field("_netService").GetValue<INetGameService>();
        if (netService == null || netService.Type != NetGameType.Host || !__instance.ConnectedPlayerIds.Contains(senderId))
        {
            return;
        }

        MapDrawingSyncService.SendSnapshotTo(netService, senderId, "RunLobby rejoin");
    }
}

[HarmonyPatch(typeof(StartRunLobby), "HandlePlayerReadyMessage")]
internal static class StartRunLobbyReadyDrawingColorSnapshotPatch
{
    [HarmonyPostfix]
    public static void Postfix(StartRunLobby __instance, ulong senderId)
    {
        if (__instance.NetService.Type != NetGameType.Host || !__instance.Players.Exists(player => player.id == senderId))
        {
            return;
        }

        MapDrawingSyncService.SendSnapshotTo(__instance.NetService, senderId, "StartRunLobby ready");
    }
}

[HarmonyPatch(typeof(LoadRunLobby), "HandlePlayerReadyMessage")]
internal static class LoadRunLobbyReadyDrawingColorSnapshotPatch
{
    [HarmonyPostfix]
    public static void Postfix(LoadRunLobby __instance, ulong senderId)
    {
        if (__instance.NetService.Type != NetGameType.Host || !__instance.ConnectedPlayerIds.Contains(senderId))
        {
            return;
        }

        MapDrawingSyncService.SendSnapshotTo(__instance.NetService, senderId, "LoadRunLobby ready");
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Initialize))]
internal static class NMapScreenInitializeDrawingColorBroadcastPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        MapDrawingColorOverrideService.ReapplyVisibleOverrides(__instance.Drawings);
        MapDrawingSyncService.BroadcastLocalColor("NMapScreen.Initialize");
    }
}
