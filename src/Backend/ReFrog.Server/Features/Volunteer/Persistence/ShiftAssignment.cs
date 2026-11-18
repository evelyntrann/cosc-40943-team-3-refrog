namespace ReFrog.Server.Features.Volunteer.Persistence;

/// <summary>
/// A volunteer's assignment to a shift (docs/design/VOL.md data model).
/// ShiftId references ADM's Shift; VolunteerId references Authentication's AuthAccount.
/// </summary>
public sealed class ShiftAssignment
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Guid VolunteerId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
