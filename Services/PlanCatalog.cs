namespace HydroSentinel.Services;

public sealed record PlanDefinition(
    string Id,
    string Name,
    decimal HardwarePrice,
    decimal MonthlyPrice,
    string DeviceAllowance,
    string Description);

public sealed class PlanCatalog
{
    public const string Source = "wwwroot/images/flyer.jpeg";
    public const string Currency = "NGN";

    public IReadOnlyList<PlanDefinition> Plans { get; } = new[]
    {
        new PlanDefinition("basic", "Basic", 79000m, 5000m, "1 unit", "For a single residential water system."),
        new PlanDefinition("pro", "Pro", 169000m, 15000m, "3 units", "For growing homes and smaller business contexts."),
        new PlanDefinition("max", "Max", 749000m, 50000m, "More than 3 units", "For larger or more involved installations.")
    };

    public const string OfferNote = "The product flyer mentions a launch offer of 10% off plus the first month free. The offer is not applied at checkout; confirm its timing and eligibility with Firmlon.";

    public PlanDefinition? Find(string? id) => Plans.FirstOrDefault(plan => string.Equals(plan.Id, id, StringComparison.OrdinalIgnoreCase));
}
