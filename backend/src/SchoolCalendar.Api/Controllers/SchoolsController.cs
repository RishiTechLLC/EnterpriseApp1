using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SchoolCalendar.Application.Features;
using SchoolCalendar.Application.Features.Programs;
using SchoolCalendar.Application.Features.Schools;
using SchoolCalendar.Infrastructure.Permify;

namespace SchoolCalendar.Api.Controllers;

[ApiController]
[Route("api/schools")]
public sealed class SchoolsController(ISender sender, IOptions<PermifyOptions> permify) : ControllerBase
{
    public sealed record SchoolRequest(string Name, string Code, string? Address, string TimeZone);
    public sealed record ProgramRequest(string Name, string Code, string? Description, string? GradeLevels);

    [HttpGet]
    public Task<IReadOnlyList<SchoolDto>> GetMine(CancellationToken ct) => sender.Send(new GetMySchoolsQuery(), ct);

    [HttpGet("{schoolId:guid}")]
    public Task<SchoolDto> Get(Guid schoolId, CancellationToken ct) => sender.Send(new GetSchoolByIdQuery(schoolId), ct);

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(SchoolRequest r, CancellationToken ct)
    {
        var id = await sender.Send(new CreateSchoolCommand(permify.Value.OrganizationId, r.Name, r.Code, r.Address, r.TimeZone), ct);
        return CreatedAtAction(nameof(Get), new { schoolId = id }, id);
    }

    [HttpPut("{schoolId:guid}")]
    public async Task<IActionResult> Update(Guid schoolId, SchoolRequest r, CancellationToken ct)
    {
        await sender.Send(new UpdateSchoolCommand(schoolId, r.Name, r.Code, r.Address, r.TimeZone), ct);
        return NoContent();
    }

    [HttpDelete("{schoolId:guid}")]
    public async Task<IActionResult> Delete(Guid schoolId, CancellationToken ct)
    {
        await sender.Send(new DeleteSchoolCommand(schoolId), ct);
        return NoContent();
    }

    [HttpGet("{schoolId:guid}/programs")]
    public Task<IReadOnlyList<ProgramDto>> GetPrograms(Guid schoolId, CancellationToken ct) =>
        sender.Send(new GetProgramsBySchoolQuery(schoolId), ct);

    [HttpPost("{schoolId:guid}/programs")]
    public async Task<ActionResult<Guid>> CreateProgram(Guid schoolId, ProgramRequest r, CancellationToken ct)
    {
        var id = await sender.Send(new CreateProgramCommand(schoolId, r.Name, r.Code, r.Description, r.GradeLevels), ct);
        return CreatedAtAction(nameof(ProgramsController.Get), "Programs", new { programId = id }, id);
    }
}
