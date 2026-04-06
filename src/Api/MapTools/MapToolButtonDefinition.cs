using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterMapTools.Api.MapTools;

public sealed class MapToolButtonDefinition
{
    public required string Id { get; init; }

    public required string TooltipTitle { get; init; }

    public string? TooltipTitleController { get; init; }

    public required string TooltipDescription { get; init; }

    public required Func<Texture2D?> IconFactory { get; init; }

    public Func<Texture2D?>? HoverIconFactory { get; init; }

    public required Action<NMapScreen> OnPressed { get; init; }

    public int Order { get; init; } = 1000;

    public float NormalScale { get; init; } = 1.1f;

    public float HoverScale { get; init; } = 1.2f;

    public Color InactiveColor { get; init; } = new(1f, 1f, 1f, 0.8f);

    public Color ActiveColor { get; init; } = Colors.White;
}
