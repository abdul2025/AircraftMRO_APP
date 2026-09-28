using System.Globalization;

namespace AircraftMRO.Web.Models;

/// <summary>The words a doughnut chart uses; <see cref="Id"/> prefixes its element ids.</summary>
public sealed record DonutChartText(
    string Id,
    string Title,
    string Subtitle,
    string Category,
    string Caption,
    string UnitSingular,
    string UnitPlural);

/// <summary>One slice as a feature defines it: its label, count, color, and the list URL it filters to.</summary>
public sealed record DonutSlice(string Label, int Count, string ColorClass, string Url, bool IsSelected = false);

/// <summary>
/// Doughnut geometry shared by the list-page charts. Segments are circles drawn with
/// stroke-dasharray, in the order the slices are given, so each category keeps its color.
/// </summary>
public sealed record DonutChartViewModel(DonutChartText Text, int Total, IReadOnlyList<DonutSegment> Segments)
{
    public const double Size = 160;
    public const double Radius = 62;
    public const double Thickness = 22;
    public const double Center = Size / 2;

    /// <summary>Surface-colored gap between touching segments.</summary>
    public const double Gap = 2;

    public static readonly double Circumference = 2 * Math.PI * Radius;

    public bool HasSelection => Segments.Any(segment => segment.IsSelected);

    public static DonutChartViewModel Create(DonutChartText text, IReadOnlyList<DonutSlice> slices)
    {
        var total = slices.Sum(slice => slice.Count);
        var nonEmpty = slices.Count(slice => slice.Count > 0);
        var gap = nonEmpty > 1 ? Gap : 0;

        var segments = new List<DonutSegment>();
        var offset = 0d;
        foreach (var slice in slices)
        {
            var share = total == 0 ? 0 : (double)slice.Count / total;
            var arc = share * Circumference;
            var drawn = slice.Count == 0 ? 0 : Math.Max(arc - gap, 0.5);

            segments.Add(new DonutSegment(slice, share, drawn, offset));
            offset += arc;
        }

        return new DonutChartViewModel(text, total, segments);
    }

    public string CountText(int count) => $"{count} {(count == 1 ? Text.UnitSingular : Text.UnitPlural)}";

    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed record DonutSegment(DonutSlice Slice, double Share, double Length, double StartOffset)
{
    public string Label => Slice.Label;

    public int Count => Slice.Count;

    public string ColorClass => Slice.ColorClass;

    public string Url => Slice.Url;

    public bool IsSelected => Slice.IsSelected;

    public string Percent => Share.ToString("0%", CultureInfo.InvariantCulture);

    public string DashArray =>
        $"{DonutChartViewModel.Number(Length)} {DonutChartViewModel.Number(DonutChartViewModel.Circumference - Length)}";

    /// <summary>Negative offset moves the dash start clockwise to where the previous segment ended.</summary>
    public string DashOffset => DonutChartViewModel.Number(-StartOffset);
}
