namespace SelfControl.Core;

/// <summary>Matches one specific date, or a set of weekdays. With neither, matches nothing.</summary>
public record DateRule(DateOnly? Date, DayOfWeek[] Days)
{
    public bool Matches(DateOnly d) =>
        Date is { } only ? d == only : Array.IndexOf(Days, d.DayOfWeek) >= 0;

    public static DateRule On(DateOnly date) => new(date, Array.Empty<DayOfWeek>());

    public static DateRule Every(params DayOfWeek[] days) => new(null, days);

    public static DateRule Weekdays() => Every(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);

    public static DateRule Weekend() => Every(DayOfWeek.Saturday, DayOfWeek.Sunday);
}
