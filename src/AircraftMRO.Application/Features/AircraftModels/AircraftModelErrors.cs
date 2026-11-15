using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.AircraftModels;

public static class AircraftModelErrors
{
    public const string NotFound = "AircraftModel.NotFound";
    public const string Validation = AircraftModel.ValidationErrorCode;
    public const string DuplicateModelCode = "AircraftModel.DuplicateModelCode";
    public const string ConcurrencyConflict = "AircraftModel.ConcurrencyConflict";
    public const string InUse = "AircraftModel.InUse";
    public const string ManufacturerNotFound = "AircraftModel.ManufacturerNotFound";
    public const string ManufacturerInactive = "AircraftModel.ManufacturerInactive";

    public const string NotFoundMessage = "The aircraft model was not found.";
    public const string DuplicateModelCodeMessage = "This manufacturer already has a model with this code.";
    public const string ConcurrencyConflictMessage =
        "This aircraft model was changed by someone else after you opened it. Reload and try again.";
    public const string InUseMessage =
        "Aircraft are recorded against this model. Change them or deactivate the model instead.";
    public const string ManufacturerNotFoundMessage = "The selected manufacturer was not found.";
    public const string ManufacturerInactiveMessage =
        "The selected manufacturer is inactive. Choose an active manufacturer.";
}
