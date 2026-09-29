namespace PersonalCalendar.Domain.Events;

public readonly record struct EventId(Guid Value)
{
    public static EventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
