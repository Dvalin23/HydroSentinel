namespace HydroSentinel.Services;

public interface IAdminPortalProjection
{
    bool IsDevelopmentDemo { get; }
    AdminPortalSnapshot Snapshot { get; }
    IReadOnlyList<PlanDefinition> Plans { get; }
}

/// <summary>
/// Presentation-only projection for the first admin vertical slice.
/// Replace this service with authenticated admin queries when backend contracts exist.
/// </summary>
public sealed class AdminPortalProjection(IWebHostEnvironment environment, IDemoMode demo, PlanCatalog catalog) : IAdminPortalProjection
{
    public bool IsDevelopmentDemo => environment.IsDevelopment() && demo.Enabled;
    public IReadOnlyList<PlanDefinition> Plans => catalog.Plans;

    public AdminPortalSnapshot Snapshot
    {
        get
        {
            if (!IsDevelopmentDemo) return AdminPortalSnapshot.Unavailable;

            var source = demo.Snapshot;
            return new AdminPortalSnapshot(
                "Development demo workspace",
                source.Devices.Count,
                source.Devices.Count(x => x.Status.Equals("Online", StringComparison.OrdinalIgnoreCase)),
                source.Alerts.Count(x => x.Status.Equals("Open", StringComparison.OrdinalIgnoreCase)),
                source.Tickets.Count(x => x.Status.Equals("Open", StringComparison.OrdinalIgnoreCase)),
                source,
                true);
        }
    }
}

public sealed record AdminPortalSnapshot(
    string WorkspaceLabel,
    int DeviceCount,
    int OnlineDeviceCount,
    int OpenAlertCount,
    int OpenTicketCount,
    DemoSnapshot Data,
    bool IsAvailable)
{
    public static AdminPortalSnapshot Unavailable => new(
        "Backend integration required",
        0, 0, 0, 0, DemoSnapshot.Empty, false);
}
