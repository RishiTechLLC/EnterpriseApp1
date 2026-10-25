using SchoolCalendar.Domain.Entities;
using SchoolCalendar.Domain.Enums;
using SchoolCalendar.Domain.Exceptions;

namespace SchoolCalendar.Application.Tests;

public class AcademicCalendarTests
{
    private static AcademicCalendar NewCalendar() =>
        new(Guid.NewGuid(), "HS 2026-2027", "2026-2027", new(2026, 8, 17), new(2027, 6, 2), null);

    [Fact]
    public void Event_outside_calendar_range_is_rejected()
    {
        var calendar = NewCalendar();
        Assert.Throws<DomainException>(() =>
            calendar.AddEvent("Summer camp", null, CalendarEventType.SchoolEvent, new(2027, 7, 1), new(2027, 7, 5), true));
    }

    [Fact]
    public void Non_instructional_events_reduce_instructional_days()
    {
        var calendar = new AcademicCalendar(Guid.NewGuid(), "Week", "2026-2027", new(2026, 9, 7), new(2026, 9, 13), null); // Mon..Sun
        Assert.Equal(5, calendar.CountInstructionalDays());

        calendar.AddEvent("Labor Day", null, CalendarEventType.Holiday, new(2026, 9, 7), new(2026, 9, 7), false);
        calendar.AddEvent("Assembly", null, CalendarEventType.SchoolEvent, new(2026, 9, 8), new(2026, 9, 8), true);

        Assert.Equal(4, calendar.CountInstructionalDays());
    }

    [Fact]
    public void Shrinking_range_past_existing_events_is_rejected()
    {
        var calendar = NewCalendar();
        calendar.AddEvent("Graduation", null, CalendarEventType.SchoolEvent, new(2027, 6, 2), new(2027, 6, 2), true);
        Assert.Throws<DomainException>(() => calendar.Update("HS", "2026-2027", new(2026, 8, 17), new(2027, 5, 31), null));
    }

    [Fact]
    public void Foreign_event_ids_are_rejected()
    {
        var calendar = NewCalendar();
        Assert.Throws<DomainException>(() => calendar.RemoveEvent(Guid.NewGuid()));
    }

    [Fact]
    public void Publish_and_unpublish_toggle_status()
    {
        var calendar = NewCalendar();
        calendar.Publish();
        Assert.Equal(CalendarStatus.Published, calendar.Status);
        Assert.Throws<DomainException>(calendar.Publish);
        calendar.Unpublish();
        Assert.Equal(CalendarStatus.Draft, calendar.Status);
    }
}
