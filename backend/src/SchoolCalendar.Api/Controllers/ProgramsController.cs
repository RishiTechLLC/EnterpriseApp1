using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolCalendar.Application.Features;
using SchoolCalendar.Application.Features.Calendars;
using SchoolCalendar.Application.Features.Programs;

namespace SchoolCalendar.Api.Controllers;

[ApiController]
[Route("api/programs")]
public sealed class ProgramsController(ISender sender) : ControllerBase
{
    public sealed record CalendarRequest(string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, string? Description);

    [HttpGet("{programId:guid}")]
    public Task<ProgramDto> Get(Guid programId, CancellationToken ct) => sender.Send(new GetProgramByIdQuery(programId), ct);

    [HttpPut("{programId:guid}")]
    public async Task<IActionResult> Update(Guid programId, SchoolsController.ProgramRequest r, CancellationToken ct)
    {
        await sender.Send(new UpdateProgramCommand(programId, r.Name, r.Code, r.Description, r.GradeLevels), ct);
        return NoContent();
    }

    [HttpDelete("{programId:guid}")]
    public async Task<IActionResult> Delete(Guid programId, CancellationToken ct)
    {
        await sender.Send(new DeleteProgramCommand(programId), ct);
        return NoContent();
    }

    [HttpGet("{programId:guid}/calendars")]
    public Task<IReadOnlyList<CalendarSummaryDto>> GetCalendars(Guid programId, CancellationToken ct) =>
        sender.Send(new GetCalendarsByProgramQuery(programId), ct);

    [HttpPost("{programId:guid}/calendars")]
    public async Task<ActionResult<Guid>> CreateCalendar(Guid programId, CalendarRequest r, CancellationToken ct)
    {
        var id = await sender.Send(new CreateCalendarCommand(programId, r.Name, r.AcademicYear, r.StartDate, r.EndDate, r.Description), ct);
        return CreatedAtAction(nameof(CalendarsController.Get), "Calendars", new { calendarId = id }, id);
    }
}
