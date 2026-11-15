namespace AircraftMRO.Web.Models;

/// <summary>Edit and delete icon links for one record; <paramref name="Label"/> names it for screen readers.</summary>
public sealed record RowActionsViewModel(Guid Id, string Label);
