using MegaCrit.Sts2.Core.Runs;

namespace BetterMapTools.Features.MapDrawing;

internal static class MapDrawingOperationHistoryService
{
    private sealed class SuppressScope : IDisposable
    {
        public void Dispose()
        {
            _suppressionDepth = Math.Max(0, _suppressionDepth - 1);
        }
    }

    private static readonly Dictionary<ulong, Stack<int>> HistoryByPlayer = [];
    private static int _suppressionDepth;

    public static IDisposable SuppressLocalStrokeRecording()
    {
        _suppressionDepth++;
        return new SuppressScope();
    }

    public static bool IsSuppressed => _suppressionDepth > 0;

    public static void RecordLocalOperation(int lineCount)
    {
        if (lineCount <= 0 || IsSuppressed || !TryGetLocalPlayerId(out var playerId))
        {
            return;
        }

        if (!HistoryByPlayer.TryGetValue(playerId, out var history))
        {
            history = new Stack<int>();
            HistoryByPlayer[playerId] = history;
        }

        history.Push(lineCount);
    }

    public static bool TryPopLocalOperation(out int lineCount)
    {
        lineCount = 0;
        if (!TryGetLocalPlayerId(out var playerId) ||
            !HistoryByPlayer.TryGetValue(playerId, out var history) ||
            history.Count == 0)
        {
            return false;
        }

        lineCount = history.Pop();
        return lineCount > 0;
    }

    public static void ClearLocalHistory()
    {
        if (!TryGetLocalPlayerId(out var playerId))
        {
            return;
        }

        HistoryByPlayer.Remove(playerId);
    }

    public static void ClearAllHistory()
    {
        HistoryByPlayer.Clear();
    }

    private static bool TryGetLocalPlayerId(out ulong playerId)
    {
        playerId = 0;
        var runManager = RunManager.Instance;
        if (!runManager.IsInProgress)
        {
            return false;
        }

        playerId = runManager.NetService.NetId;
        return true;
    }
}
