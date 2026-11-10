using HydroSentinel.Data;
using HydroSentinel.Services;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Backend;

public sealed class SupportTicketService(AppDbContext db, AuditService audit, NotificationService notifications)
{
    public async Task<List<SupportTicket>> CustomerTicketsAsync(string userId, CancellationToken ct = default)
        => await db.SupportTickets.AsNoTracking().Where(t => t.UserId == userId)
            .OrderByDescending(t => t.UpdatedAt).ToListAsync(ct);

    public async Task<SupportTicket?> CustomerTicketAsync(string userId, string reference, CancellationToken ct = default)
        => await db.SupportTickets.AsNoTracking().Include(t => t.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Reference == reference, ct);

    public async Task<SupportTicket> CreateAsync(string userId, string subject, string category, Guid? deviceId,
        string initialMessage, CancellationToken ct = default)
    {
        if (deviceId is not null && !await db.Devices.AnyAsync(d => d.Id == deviceId && d.OwnerId == userId, ct))
            throw new UnownedSupportDeviceException();

        var now = DateTimeOffset.UtcNow;
        var ticket = new SupportTicket
        {
            Reference = "HS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            UserId = userId,
            Subject = subject.Trim(),
            Category = category.Trim(),
            DeviceId = deviceId,
            Status = SupportTicketStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
            Messages =
            [
                new SupportMessage { AuthorId = userId, Body = initialMessage.Trim(), CreatedAt = now }
            ]
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(userId, "support.ticket.created", ticket.Reference, ticket.Category, ct);
        return ticket;
    }

    public async Task<bool> CustomerReplyAsync(string userId, string reference, string message, CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.UserId == userId && t.Reference == reference, ct);
        if (ticket is null) return false;

        var now = DateTimeOffset.UtcNow;
        db.SupportMessages.Add(new SupportMessage { TicketId = ticket.Id, AuthorId = userId, Body = message.Trim(), CreatedAt = now });
        ticket.Status = SupportTicketStatus.Open;
        ticket.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(userId, "support.ticket.customer_reply", ticket.Reference, null, ct);
        return true;
    }

    public async Task<List<SupportTicket>> AllTicketsAsync(CancellationToken ct = default)
        => await db.SupportTickets.AsNoTracking().OrderBy(t => t.Status == SupportTicketStatus.Resolved)
            .ThenByDescending(t => t.UpdatedAt).ToListAsync(ct);

    public async Task<SupportTicket?> TicketForStaffAsync(string reference, CancellationToken ct = default)
        => await db.SupportTickets.Include(t => t.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(t => t.Reference == reference, ct);

    public async Task<bool> StaffReplyAsync(string staffId, string reference, string message, CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Reference == reference, ct);
        if (ticket is null) return false;

        var now = DateTimeOffset.UtcNow;
        var reply = new SupportMessage { TicketId = ticket.Id, AuthorId = staffId, IsStaffReply = true, Body = message.Trim(), CreatedAt = now };
        db.SupportMessages.Add(reply);
        if (ticket.Status != SupportTicketStatus.Resolved) ticket.Status = SupportTicketStatus.WaitingOnCustomer;
        ticket.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(staffId, "support.ticket.staff_reply", ticket.Reference, null, ct);
        await notifications.AddAsync(ticket.UserId, "Firmlon replied to your support request", $"There is a new reply on support request {ticket.Reference}.",
            "Support", $"support-reply:{reply.Id}", $"/Customer/Support/Details?id={Uri.EscapeDataString(ticket.Reference)}", ct);
        return true;
    }

    public async Task<bool> SetStatusAsync(string staffId, string reference, string status, CancellationToken ct = default)
    {
        if (status is not (SupportTicketStatus.Open or SupportTicketStatus.InProgress or SupportTicketStatus.WaitingOnCustomer or SupportTicketStatus.Resolved))
            return false;
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Reference == reference, ct);
        if (ticket is null) return false;
        ticket.Status = status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(staffId, "support.ticket.status_changed", ticket.Reference, status, ct);
        return true;
    }
}

public sealed class UnownedSupportDeviceException : Exception { }
