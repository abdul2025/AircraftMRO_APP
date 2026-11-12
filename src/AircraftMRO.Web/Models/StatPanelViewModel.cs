using System.Globalization;

namespace AircraftMRO.Web.Models;

/// <summary>
/// The list-page summary panel: a total, one share-filled row per status (the same segments the
/// doughnut draws, so counts, links, and colors always agree), a few highlight figures, and a
/// footer naming the facet the list is filtered to. Render it with <c>_StatOverview</c> to get the
/// panel and its doughnut together, or <c>_StatPanel</c> for the panel alone.
/// </summary>
/// <param name="Label">Accessible name of the panel, e.g. "Work order summary".</param>
/// <param name="AllUrl">The list URL with every facet cleared (search and scope kept).</param>
/// <param name="FacetLabel">What the rows and highlights currently filter to, or null for everything.</param>
public sealed record StatPanelViewModel(
    string Label,
    string TotalLabel,
    string AllUrl,
    DonutChartViewModel Breakdown,
    IReadOnlyList<StatHighlight> Highlights,
    string? FacetLabel)
{
    public bool IsAllSelected => FacetLabel is null;

    /// <summary>Row fill width as a CSS percentage; exact, unlike the rounded label.</summary>
    public static string FillWidth(double share) =>
        (share * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}

/// <summary>
/// A figure beside the total. <paramref name="Value"/> is already formatted; a highlight with no
/// <paramref name="Url"/> is informational and does not filter the list.
/// </summary>
public sealed record StatHighlight(
    string Label,
    string Value,
    string Role,
    string? Url = null,
    bool IsSelected = false,
    string? Title = null);
