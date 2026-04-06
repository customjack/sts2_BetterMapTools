using MegaCrit.Sts2.Core.Map;
using BetterMapTools.Features.MapRouting.Metrics;

namespace BetterMapTools.Features.MapRouting;

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

internal enum RoutingSelectionMode
{
    Lexicographic,
    Weighted
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
    public RoutingSelectionMode SelectionMode { get; init; } = RoutingSelectionMode.Lexicographic;
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
    private const double ScoreEpsilon = 0.000001d;

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

        return request.SelectionMode == RoutingSelectionMode.Weighted
            ? SolveWeighted(candidates, feasible, resolvedPriorities, metricRank)
            : SolveLexicographic(candidates, feasible, resolvedPriorities, metricRank);
    }

    public static string SelectionModeLabel(RoutingSelectionMode selectionMode)
    {
        return selectionMode switch
        {
            RoutingSelectionMode.Weighted => "Weighted Score",
            _ => "Lexicographic"
        };
    }

    private static RouteSolveResult SolveLexicographic(
        IReadOnlyList<RouteCandidate> candidates,
        IReadOnlyList<RouteCandidate> feasible,
        IReadOnlyList<RoutingPriorityRule> resolvedPriorities,
        IReadOnlyDictionary<RouteMetricType, int> metricRank)
    {
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

    private static RouteSolveResult SolveWeighted(
        IReadOnlyList<RouteCandidate> candidates,
        IReadOnlyList<RouteCandidate> feasible,
        IReadOnlyList<RoutingPriorityRule> resolvedPriorities,
        IReadOnlyDictionary<RouteMetricType, int> metricRank)
    {
        var activePriorities = resolvedPriorities
            .Where(rule => rule.Priority != 0)
            .OrderByDescending(rule => Math.Abs(rule.Priority))
            .ThenBy(rule => metricRank[rule.Metric])
            .ToList();

        if (activePriorities.Count == 0)
        {
            var unscoredRoutes = feasible
                .Select(candidate => (IReadOnlyList<MapPoint>)candidate.Path)
                .ToList();

            return new RouteSolveResult
            {
                Found = unscoredRoutes.Count > 0,
                Routes = unscoredRoutes,
                StatusMessage = $"Found {unscoredRoutes.Count} route(s) ({feasible.Count} feasible of {candidates.Count} total). No weights enabled.",
                TotalRoutes = candidates.Count,
                FeasibleRoutes = feasible.Count,
                MetricSummaries = BuildMetricSummaries(candidates, feasible, feasible)
            };
        }

        var scoredCandidates = feasible
            .Select(candidate => new ScoredCandidate(candidate, ComputeWeightedScore(candidate, activePriorities)))
            .ToList();
        var bestScore = scoredCandidates.Max(candidate => candidate.Score);
        var selected = scoredCandidates
            .Where(candidate => Math.Abs(candidate.Score - bestScore) <= ScoreEpsilon)
            .Select(candidate => candidate.Candidate)
            .ToList();

        var routes = selected
            .Select(candidate => (IReadOnlyList<MapPoint>)candidate.Path)
            .ToList();
        var summaries = activePriorities
            .Select(rule => $"{MetricLabel(rule.Metric)} x{rule.Priority}")
            .ToList();
        var summary =
            $"Found {routes.Count} route(s) ({feasible.Count} feasible of {candidates.Count} total) " +
            $"with best weighted score {bestScore:0.###} using {string.Join(", ", summaries)}.";

        return new RouteSolveResult
        {
            Found = routes.Count > 0,
            Routes = routes,
            StatusMessage = summary,
            TotalRoutes = candidates.Count,
            FeasibleRoutes = feasible.Count,
            MetricSummaries = BuildMetricSummaries(candidates, feasible, selected)
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

    private readonly record struct ScoredCandidate(RouteCandidate Candidate, double Score);

    private static double ComputeWeightedScore(
        RouteCandidate candidate,
        IReadOnlyList<RoutingPriorityRule> priorities)
    {
        var score = 0d;
        foreach (var priority in priorities)
        {
            score += candidate.Metrics[priority.Metric] * priority.Priority;
        }

        return score;
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
