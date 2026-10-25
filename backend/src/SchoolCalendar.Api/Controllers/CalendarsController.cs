using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolCalendar.Application.Features;
using SchoolCalendar.Application.Features.Calendars;
using SchoolCalendar.Application.Features.Events;
using SchoolCalendar.Domain.Enums;

namespace SchoolCalendar.Api.Controllers;

[ApiController]
[Route("api/calendars")]
public sealed class CalendarsController(ISender sender) : ControllerBase
{
    public sealed record EventRequest(string Title, string? Description, CalendarEventType Type, DateOnly StartDate, DateOnly EndDate, bool IsInstructionalDay);

    [HttpGet("{calendarId:guid}")]
    public Task<CalendarDetailDto> Get(Guid calendarId, CancellationToken ct) => sender.Send(new GetCalendarByIdQuery(calendarId), ct);

    [HttpPut("{calendarId:guid}")]
    public async Task<IActionResult> Update(Guid calendarId, ProgramsController.CalendarRequest r, CancellationToken ct)
    {
        await sender.Send(new UpdateCalendarCommand(calendarId, r.Name, r.AcademicYear, r.StartDate, r.EndDate, r.Description), ct);
        return NoContent();
    }

    [HttpDelete("{calendarId:guid}")]
    public async Task<IActionResult> Delete(Guid calendarId, CancellationToken ct)
    {
        await sender.Send(new DeleteCalendarCommand(calendarId), ct);
        return NoContent();
    }

    [HttpPost("{calendarId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid calendarId, CancellationToken ct)
    {
        await sender.Send(new SetCalendarPublishedCommand(calendarId, true), ct);
        return NoContent();
    }

    [HttpPost("{calendarId:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid calendarId, CancellationToken ct)
    {
        await sender.Send(new SetCalendarPublishedCommand(calendarId, false), ct);
        return NoContent();
    }

    [HttpGet("{calendarId:guid}/export.ics")]
    public async Task<IActionResult> ExportIcs(Guid calendarId, CancellationToken ct)
    {
        var ics = await sender.Send(new ExportCalendarIcsQuery(calendarId), ct);
        return File(Encoding.UTF8.GetBytes(ics), "text/calendar", $"calendar-{calendarId}.ics");
    }

    [HttpGet("{calendarId:guid}/events")]
    public Task<IReadOnlyList<CalendarEventDto>> GetEvents(Guid calendarId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] CalendarEventType? type, CancellationToken ct) =>
        sender.Send(new GetCalendarEventsQuery(calendarId, from, to, type), ct);

    [HttpPost("{calendarId:guid}/events")]
    public async Task<ActionResult<CalendarEventDto>> AddEvent(Guid calendarId, EventRequest r, CancellationToken ct)
    {
        var created = await sender.Send(new AddEventCommand(calendarId, r.Title, r.Description, r.Type, r.StartDate, r.EndDate, r.IsInstructionalDay), ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{calendarId:guid}/events/{eventId:guid}")]
    public async Task<IActionResult> UpdateEvent(Guid calendarId, Guid eventId, EventRequest r, CancellationToken ct)
    {
        await sender.Send(new UpdateEventCommand(calendarId, eventId, r.Title, r.Description, r.Type, r.StartDate, r.EndDate, r.IsInstructionalDay), ct);
        return NoContent();
    }

    [HttpDelete("{calendarId:guid}/events/{eventId:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid calendarId, Guid eventId, CancellationToken ct)
    {
        await sender.Send(new DeleteEventCommand(calendarId, eventId), ct);
        return NoContent();
    }
}
