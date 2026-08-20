namespace AircraftMRO.Infrastructure.Persistence;

internal sealed class SystemFeatureRecord
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IconKey { get; set; } = null!;
    public string? ControllerName { get; set; }
    public string? ActionName { get; set; }
    public string StatusText { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; }
}
