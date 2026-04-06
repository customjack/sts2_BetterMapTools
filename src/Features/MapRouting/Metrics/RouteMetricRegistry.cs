namespace BetterMapTools.Features.MapRouting.Metrics;

internal static class RouteMetricRegistry
{
    private static readonly List<RouteMetricDefinition> DefinitionsInternal = new();
    private static readonly Dictionary<RouteMetricType, RouteMetricDefinition> DefinitionByType = new();

    private static bool _defaultsRegistered;

    public static IReadOnlyList<RouteMetricDefinition> Definitions => DefinitionsInternal;

    public static IReadOnlyList<RouteMetricType> MetricOrder => DefinitionsInternal.Select(def => def.Type).ToList();

    public static void RegisterDefaults()
    {
        if (_defaultsRegistered)
        {
            return;
        }

        _defaultsRegistered = true;

        EliteRouteMetric.Register();
        MonsterRouteMetric.Register();
        RestSiteRouteMetric.Register();
        ShopRouteMetric.Register();
        UnknownRouteMetric.Register();
    }

    public static void Register(RouteMetricDefinition definition)
    {
        if (DefinitionByType.ContainsKey(definition.Type))
        {
            return;
        }

        DefinitionsInternal.Add(definition);
        DefinitionByType[definition.Type] = definition;
    }

    public static bool TryGet(RouteMetricType type, out RouteMetricDefinition definition)
    {
        return DefinitionByType.TryGetValue(type, out definition!);
    }
}
