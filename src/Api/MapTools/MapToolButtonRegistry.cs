
namespace BetterMapTools.Api.MapTools;

public static class MapToolButtonRegistry
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, MapToolButtonDefinition> Buttons =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Register(MapToolButtonDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);

        lock (Sync)
        {
            Buttons[definition.Id] = definition;
        }

    }

    public static IReadOnlyList<MapToolButtonDefinition> GetAllOrdered()
    {
        lock (Sync)
        {
            return Buttons.Values
                .OrderBy(button => button.Order)
                .ThenBy(button => button.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
