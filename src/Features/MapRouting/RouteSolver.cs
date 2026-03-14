using MegaCrit.Sts2.Core.Map;
using RoutingHelper.Features.MapRouting.Metrics;

namespace RoutingHelper.Features.MapRouting;

internal enum RouteMetricType
{
    Elite,
    Monster,
    RestSite,
    Shop,
    Unknown
}

internal enum RouteObjectiveMode
{
    None,
    Min,
    Max
}

internal sealed class RoutingConstraint
{
    public required RouteMetricType Metric { get; init; }
    public int Min { get; init; } = 0;
    public int Max { get; init; } = 99;
}

internal sealed class RoutingPriorityRule
{
    public required RouteMetricType Metric { get; init; }
    public RouteObjectiveMode Mode { get; init; } = RouteObjectiveMode.None;
    public int Priority { get; init; }
}

internal sealed class RoutingRequest
{
    public IReadOnlyList<RoutingConstraint> Constraints { get; init; } = Array.Empty<RoutingConstraint>();
    public IReadOnlyList<RoutingPriorityRule> Priorities { get; init; } = Array.Empty<RoutingPriorityRule>();
}

internal sealed class RouteSolveResult
{
    public bool Found { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public IReadOnlyList<IReadOnlyList<MapPoint>> Routes { get; init; } = Array.Empty<IReadOnlyList<MapPoint>>();
    public int TotalRoutes { get; init; }
    public int FeasibleRoutes { get; init; }
    public IReadOnlyList<RouteMetricSummary> MetricSummaries { get; init; } = Array.Empty<RouteMetricSummary>();
}

internal sealed class RouteMetricSummary
{
    public required RouteMetricType Metric { get; init; }
    public int SelectedMin { get; init; }
    public int SelectedMax { get; init; }
    public int GlobalMin { get; init; }
    public int GlobalMax { get; init; }
    public int FeasibleMin { get; init; }
    public int FeasibleMax { get; init; }
}

internal static class RouteSolver
{
    public static IReadOnlyList<RouteMetricType> MetricOrder => RouteMetricRegistry.MetricOrder;

    public static RouteSolveResult Solve(MapPoint start, MapPoint boss, RoutingRequest request)
    {
        RouteMetricRegistry.RegisterDefaults();
        var definitions = RouteMetricRegistry.Definitions;
        var metricOrder = MetricOrder;
        var metricRank = metricOrder
            .Select((metric, idx) => (metric, idx))
            .ToDictionary(pair => pair.metric, pair => pair.idx);

        var allPaths = EnumerateRoutes(start, boss);
        if (allPaths.Count == 0)
        {
            return new RouteSolveResult
            {
                Found = false,
                TotalRoutes = 0,
                FeasibleRoutes = 0,
                StatusMessage = "No routes found from current map position to boss.",
                MetricSummaries = Array.Empty<RouteMetricSummary>()
            };
        }

        var candidates = allPaths.Select(path => new RouteCandidate(path, CountMetrics(path))).ToList();
        var constraintsByMetric = request.Constraints.ToDictionary(c => c.Metric);
        var resolvedConstraints = new List<RouteMetricConstraint>(definitions.Count);
        foreach (var definition in definitions)
        {
            if (constraintsByMetric.TryGetValue(definition.Type, out var requestConstraint))
            {
                resolvedConstraints.Add(definition.CreateConstraint(requestConstraint.Min, requestConstraint.Max));
            }
            else
            {
                resolvedConstraints.Add(definition.CreateConstraint(definition.DefaultMin, definition.DefaultMax));
            }
        }

        var feasible = candidates.Where(candidate => SatisfiesConstraints(candidate, resolvedConstraints)).ToList();
        if (feasible.Count == 0)
        {
            var emptySummaries = BuildMetricSummaries(candidates, Array.Empty<RouteCandidate>(), Array.Empty<RouteCandidate>());
            return new RouteSolveResult
            {
                Found = false,
                TotalRoutes = candidates.Count,
                FeasibleRoutes = 0,
                StatusMessage = $"No route found. 0 routes satisfy constraints out of {candidates.Count} total route(s).",
                MetricSummaries = emptySummaries
            };
        }

        var requestPrioritiesByMetric = request.Priorities.ToDictionary(p => p.Metric);
        var resolvedPriorities = new List<RoutingPriorityRule>(definitions.Count);
        foreach (var definition in definitions)
        {
            if (requestPrioritiesByMetric.TryGetValue(definition.Type, out var requestPriority))
            {
                resolvedPriorities.Add(requestPriority);
            }
            else
            {
                resolvedPriorities.Add(new RoutingPriorityRule
                {
                    Metric = definition.Type,
                    Mode = definition.DefaultObjectiveMode,
                    Priority = definition.DefaultPriority
                });
            }
        }

        var activePriorities = resolvedPriorities
            .Where(rule => rule.Mode != RouteObjectiveMode.None)
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => metricRank[rule.Metric])
            .ToList();

        var current = feasible;
        var summaries = new List<string>();

        foreach (var rule in activePriorities)
        {
            var metric = rule.Metric;
            var best = rule.Mode == RouteObjectiveMode.Max
                ? current.Max(candidate => candidate.Metrics[metric])
                : current.Min(candidate => candidate.Metrics[metric]);

            var globalExtreme = rule.Mode == RouteObjectiveMode.Max
                ? feasible.Max(candidate => candidate.Metrics[metric])
                : feasible.Min(candidate => candidate.Metrics[metric]);

            current = rule.Mode == RouteObjectiveMode.Max
                ? current.Where(candidate => candidate.Metrics[metric] == best).ToList()
                : current.Where(candidate => candidate.Metrics[metric] == best).ToList();

            var extremeLabel = rule.Mode == RouteObjectiveMode.Max ? "global max" : "global min";
            summaries.Add($"{MetricLabel(metric)}={best} ({extremeLabel} {globalExtreme})");
        }

        var routes = current
            .Select(candidate => (IReadOnlyList<MapPoint>)candidate.Path)
            .ToList();

        var summary = activePriorities.Count == 0
            ? $"Found {routes.Count} route(s) ({feasible.Count} feasible of {candidates.Count} total). No priorities enabled."
            : $"Found {routes.Count} route(s) ({feasible.Count} feasible of {candidates.Count} total) with {string.Join(", ", summaries)}.";

        return new RouteSolveResult
        {
            Found = routes.Count > 0,
            Routes = routes,
            StatusMessage = summary,
            TotalRoutes = candidates.Count,
            FeasibleRoutes = feasible.Count,
            MetricSummaries = BuildMetricSummaries(candidates, feasible, current)
        };
    }

    public static string MetricLabel(RouteMetricType metric)
    {
        RouteMetricRegistry.RegisterDefaults();
        if (RouteMetricRegistry.TryGet(metric, out var definition))
        {
            return definition.Label;
        }

        return metric.ToString().ToLowerInvariant();
    }

    private static List<List<MapPoint>> EnumerateRoutes(MapPoint start, MapPoint boss)
    {
        var routes = new List<List<MapPoint>>();
        var path = new List<MapPoint>();
        var visited = new HashSet<MapCoord>();
        Dfs(start, boss, path, visited, routes);
        return routes;
    }

    private static void Dfs(
        MapPoint node,
        MapPoint boss,
        List<MapPoint> path,
        HashSet<MapCoord> visited,
        List<List<MapPoint>> routes)
    {
        if (!visited.Add(node.coord))
        {
            return;
        }

        path.Add(node);

        if (node.coord == boss.coord)
        {
            routes.Add(new List<MapPoint>(path));
        }
        else
        {
            foreach (var child in node.Children)
            {
                Dfs(child, boss, path, visited, routes);
            }
        }

        path.RemoveAt(path.Count - 1);
        visited.Remove(node.coord);
    }

    private static Dictionary<RouteMetricType, int> CountMetrics(IReadOnlyList<MapPoint> path)
    {
        var result = new Dictionary<RouteMetricType, int>();
        foreach (var definition in RouteMetricRegistry.Definitions)
        {
            result[definition.Type] = definition.Measure(path);
        }

        return result;
    }

    private static bool SatisfiesConstraints(RouteCandidate candidate, IReadOnlyList<RouteMetricConstraint> constraints)
    {
        foreach (var constraint in constraints)
        {
            if (!constraint.IsSatisfied(candidate.Metrics))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class RouteCandidate
    {
        public RouteCandidate(List<MapPoint> path, Dictionary<RouteMetricType, int> metrics)
        {
            Path = path;
            Metrics = metrics;
        }

        public List<MapPoint> Path { get; }
        public Dictionary<RouteMetricType, int> Metrics { get; }
    }

    private static IReadOnlyList<RouteMetricSummary> BuildMetricSummaries(
        IReadOnlyList<RouteCandidate> global,
        IReadOnlyList<RouteCandidate> feasible,
        IReadOnlyList<RouteCandidate> selected)
    {
        var metricOrder = MetricOrder;
        var summaries = new List<RouteMetricSummary>(metricOrder.Count);
        foreach (var metric in metricOrder)
        {
            summaries.Add(new RouteMetricSummary
            {
                Metric = metric,
                SelectedMin = MinFor(selected, metric),
                SelectedMax = MaxFor(selected, metric),
                GlobalMin = MinFor(global, metric),
                GlobalMax = MaxFor(global, metric),
                FeasibleMin = MinFor(feasible, metric),
                FeasibleMax = MaxFor(feasible, metric)
            });
        }

        return summaries;
    }

    private static int MinFor(IReadOnlyList<RouteCandidate> candidates, RouteMetricType metric)
    {
        if (candidates.Count == 0)
        {
            return 0;
        }

        return candidates.Min(candidate => candidate.Metrics[metric]);
    }

    private static int MaxFor(IReadOnlyList<RouteCandidate> candidates, RouteMetricType metric)
    {
        if (candidates.Count == 0)
        {
            return 0;
        }

        return candidates.Max(candidate => candidate.Metrics[metric]);
    }
}
