using System.Collections.ObjectModel;

namespace HydroSentinel.Services;

public interface IDemoMode
{
    bool Enabled { get; }
    string BannerText { get; }
    DemoSnapshot Snapshot { get; }
    void Reset();
    DemoDevice? AddDevice(string serial, string name, string location);
    bool RemoveDevice(string id);
    bool AcknowledgeAlert(string id);
    void MarkNotificationsRead();
    DemoTicket AddTicket(string subject, string category, string? deviceId, string description);
    void UpdateProfile(string fullName, string email, string phone, string company);
    void UpdatePreferences(bool billing, bool support);
    DemoPayment SimulatePayment(string planId, string outcome);
}

public sealed class DemoMode(IWebHostEnvironment environment, IConfiguration configuration) : IDemoMode
{
    private readonly object gate = new();
    private DemoSnapshot state = DemoSnapshot.CreateDefault();

    // Development only. Set Demo:Enabled=false to exercise the real backend locally.
    public bool Enabled => environment.IsDevelopment() && configuration.GetValue("Demo:Enabled", true);
    public string BannerText => "DEMO MODE — Sample data only. No hardware, payment, email, or backend service is contacted.";
    public DemoSnapshot Snapshot { get { lock (gate) return Enabled ? state.Clone() : DemoSnapshot.Empty; } }

    public void Reset()
    {
        if (!Enabled) return;
        lock (gate) state = DemoSnapshot.CreateDefault();
    }

    public DemoDevice? AddDevice(string serial, string name, string location)
    {
        if (!Enabled) return null;
        lock (gate)
        {
            if (state.Devices.Any(x => x.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase))) return null;
            var device = new DemoDevice(Guid.NewGuid().ToString("N"), string.IsNullOrWhiteSpace(name) ? "Demo HydroSentinel" : name.Trim(), "HS-100", serial.Trim(), string.IsNullOrWhiteSpace(location) ? "Demo installation" : location.Trim(), "Online", DateTimeOffset.Now.AddMinutes(-3), 76, 2.4m, "Running", "Good", "92%");
            state.Devices.Add(device);
            return device;
        }
    }

    public bool RemoveDevice(string id)
    {
        if (!Enabled) return false;
        lock (gate) return state.Devices.RemoveAll(x => x.Id == id) > 0;
    }

    public bool AcknowledgeAlert(string id)
    {
        if (!Enabled) return false;
        lock (gate)
        {
            var alert = state.Alerts.FirstOrDefault(x => x.Id == id);
            if (alert is null) return false;
            alert.Status = "Acknowledged";
            return true;
        }
    }

    public void MarkNotificationsRead()
    {
        if (!Enabled) return;
        lock (gate) foreach (var notification in state.Notifications) notification.IsRead = true;
    }

    public DemoTicket AddTicket(string subject, string category, string? deviceId, string description)
    {
        lock (gate)
        {
            var ticket = new DemoTicket($"DEMO-{state.Tickets.Count + 2401}", subject.Trim(), category.Trim(), deviceId?.Trim(), description.Trim(), "Open", "Normal", DateTimeOffset.Now, DateTimeOffset.Now, new List<DemoReply>());
            state.Tickets.Insert(0, ticket);
            state.Notifications.Insert(0, new DemoNotification(Guid.NewGuid().ToString("N"), "Support request created", $"Demo ticket {ticket.Reference} is ready for review.", "Support", DateTimeOffset.Now, false, "/Customer/Support/Details?id=" + ticket.Reference));
            return ticket;
        }
    }

    public void UpdateProfile(string fullName, string email, string phone, string company)
    {
        if (!Enabled) return;
        lock (gate) state.Profile = new DemoProfile(fullName.Trim(), email.Trim(), phone.Trim(), company.Trim());
    }

    public void UpdatePreferences(bool billing, bool support)
    {
        if (!Enabled) return;
        lock (gate) state.Preferences = new DemoPreferences(true, billing, support);
    }

    public DemoPayment SimulatePayment(string planId, string outcome)
    {
        lock (gate)
        {
            var plan = new PlanCatalog().Find(planId) ?? new PlanCatalog().Plans[0];
            var normalized = outcome is "pending" or "failed" or "cancelled" ? outcome : "success";
            var payment = new DemoPayment($"DEMO-PAY-{DateTime.Now:yyyyMMddHHmmss}", plan.Name, plan.MonthlyPrice, "NGN", normalized, DateTimeOffset.Now, "DEMONSTRATION ONLY");
            state.Payments.Insert(0, payment);
            if (normalized == "success") state.Subscription = new DemoSubscription(plan.Id, plan.Name, "Active (demo)", "Monthly", DateTimeOffset.Now, DateTimeOffset.Now.AddMonths(1), state.Devices.Count, "DEMONSTRATION ONLY");
            return payment;
        }
    }
}

public sealed class DemoSnapshot
{
    public DemoProfile Profile { get; set; } = new("", "", "", "");
    public DemoPreferences Preferences { get; set; } = new(true, true, true);
    public DemoSubscription Subscription { get; set; } = new("pro", "Pro", "Active (demo)", "Monthly", DateTimeOffset.Now.AddMonths(-2), DateTimeOffset.Now.AddMonths(1), 2, "DEMONSTRATION ONLY");
    public List<DemoDevice> Devices { get; set; } = [];
    public List<DemoReading> Readings { get; set; } = [];
    public List<DemoAlert> Alerts { get; set; } = [];
    public List<DemoNotification> Notifications { get; set; } = [];
    public List<DemoPayment> Payments { get; set; } = [];
    public List<DemoTicket> Tickets { get; set; } = [];

    public static DemoSnapshot Empty => new()
    {
        Profile = new("", "", "", ""),
        Subscription = new("", "Unavailable", "Unavailable", "Unavailable", DateTimeOffset.MinValue, DateTimeOffset.MinValue, 0, ""),
        Devices = [], Readings = [], Alerts = [], Notifications = [], Payments = [], Tickets = []
    };

    public static DemoSnapshot CreateDefault() => new()
    {
        Profile = new("Ada Demo Customer", "ada.demo@example.test", "+234 800 000 0000", "Demo Waterworks"),
        Devices = [
            new("demo-tank-1", "HydroSentinel Tank 1", "HS-100", "HS-001234", "Demo installation", "Online", DateTimeOffset.Now.AddMinutes(-3), 76, 2.4m, "Running", "Good", "92%"),
            new("demo-pump-2", "Pump Unit 1", "HS-300", "HS-001235", "Demo installation", "Offline", DateTimeOffset.Now.AddHours(-2), 42, 1.1m, "Stopped", "Fair", "68%")
        ],
        Readings = Enumerable.Range(0, 12).Select(i => new DemoReading(DateTimeOffset.Now.AddHours(-i), 65 + (i % 4) * 2, 2.0m + (i % 3) * .2m, i % 4 == 0 ? "Idle" : "Running")).Reverse().ToList(),
        Alerts = [
            new("demo-alert-1", "Low water level", "Warning", "Open", "demo-tank-1", "Tank level is below the demo warning threshold.", DateTimeOffset.Now.AddHours(-2)),
            new("demo-alert-2", "Device offline", "Info", "Open", "demo-pump-2", "The demo device has not reported recently.", DateTimeOffset.Now.AddDays(-1))
        ],
        Notifications = [
            new("demo-notification-1", "Low water level", "HydroSentinel Tank 1 needs attention.", "Device alert", DateTimeOffset.Now.AddHours(-2), false, "/Customer/Alerts/Details?id=demo-alert-1"),
            new("demo-notification-2", "Subscription renewed", "Your Pro demonstration subscription was renewed.", "Billing", DateTimeOffset.Now.AddDays(-4), true, "/Customer/Subscription")
        ],
        Payments = [new("DEMO-PAY-20261001", "Pro", 15000, "NGN", "success", DateTimeOffset.Now.AddDays(-28), "DEMONSTRATION ONLY")],
        Tickets = [new("DEMO-2401", "Demo support request", "Device support", "demo-tank-1", "This is an example support conversation.", "Open", "Normal", DateTimeOffset.Now.AddDays(-3), DateTimeOffset.Now.AddDays(-2), [new("Demo response", "This sample response is demonstration content only.", DateTimeOffset.Now.AddDays(-2))])]
    };

    public DemoSnapshot Clone() => new()
    {
        Profile = Profile with { }, Preferences = Preferences with { }, Subscription = Subscription with { },
        Devices = Devices.Select(x => x with { }).ToList(), Readings = Readings.Select(x => x with { }).ToList(),
        Alerts = Alerts.Select(x => x with { }).ToList(), Notifications = Notifications.Select(x => x with { }).ToList(),
        Payments = Payments.Select(x => x with { }).ToList(), Tickets = Tickets.Select(x => new DemoTicket(x.Reference, x.Subject, x.Category, x.DeviceId, x.Description, x.Status, x.Priority, x.Created, x.Updated, x.Replies.Select(r => r with { }).ToList())).ToList()
    };
}

public sealed record DemoProfile(string FullName, string Email, string Phone, string Company);
public sealed record DemoPreferences(bool DeviceAlerts, bool BillingUpdates, bool SupportUpdates);
public sealed record DemoSubscription(string PlanId, string PlanName, string Status, string BillingInterval, DateTimeOffset Started, DateTimeOffset NextBilling, int CoveredDevices, string Label);
public sealed record DemoDevice(string Id, string Name, string Model, string Serial, string Location, string Status, DateTimeOffset LastSeen, int WaterLevel, decimal Pressure, string PumpStatus, string Signal, string Battery);
public sealed record DemoReading(DateTimeOffset Timestamp, int WaterLevel, decimal Pressure, string PumpStatus);
public sealed record DemoAlert
{
    public DemoAlert(string id, string title, string severity, string status, string deviceId, string description, DateTimeOffset occurred) => (Id, Title, Severity, Status, DeviceId, Description, Occurred) = (id, title, severity, status, deviceId, description, occurred);
    public string Id { get; init; } = ""; public string Title { get; init; } = ""; public string Severity { get; init; } = ""; public string Status { get; set; } = ""; public string DeviceId { get; init; } = ""; public string Description { get; init; } = ""; public DateTimeOffset Occurred { get; init; }
}
public sealed record DemoNotification
{
    public DemoNotification(string id, string title, string message, string category, DateTimeOffset created, bool isRead, string? destination) => (Id, Title, Message, Category, Created, IsRead, Destination) = (id, title, message, category, created, isRead, destination);
    public string Id { get; init; } = ""; public string Title { get; init; } = ""; public string Message { get; init; } = ""; public string Category { get; init; } = ""; public DateTimeOffset Created { get; init; } public bool IsRead { get; set; } public string? Destination { get; init; }
}
public sealed record DemoPayment(string Reference, string Plan, decimal Amount, string Currency, string Status, DateTimeOffset Date, string Label);
public sealed class DemoTicket(string reference, string subject, string category, string? deviceId, string description, string status, string priority, DateTimeOffset created, DateTimeOffset updated, List<DemoReply> replies)
{
    public string Reference { get; } = reference; public string Subject { get; } = subject; public string Category { get; } = category; public string? DeviceId { get; } = deviceId; public string Description { get; } = description; public string Status { get; set; } = status; public string Priority { get; } = priority; public DateTimeOffset Created { get; } = created; public DateTimeOffset Updated { get; } = updated; public List<DemoReply> Replies { get; set; } = replies;
}
public sealed record DemoReply(string Author, string Message, DateTimeOffset Created);
