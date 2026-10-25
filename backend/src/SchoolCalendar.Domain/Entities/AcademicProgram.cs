namespace SchoolCalendar.Domain.Entities;

/// <summary>
/// A program offered by a school (e.g. Elementary, Middle School, IB Diploma, Adult Education).
/// Each program owns its own set of calendars.
/// </summary>
public class AcademicProgram
{
    private readonly List<AcademicCalendar> _calendars = [];

    private AcademicProgram() { }

    public AcademicProgram(Guid schoolId, string name, string code, string? description, string? gradeLevels)
    {
        Id = Guid.NewGuid();
        SchoolId = schoolId;
        Name = name;
        Code = code;
        Description = description;
        GradeLevels = gradeLevels;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid SchoolId { get; private set; }
    public School School { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? GradeLevels { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<AcademicCalendar> Calendars => _calendars;

    public void Update(string name, string code, string? description, string? gradeLevels)
    {
        Name = name;
        Code = code;
        Description = description;
        GradeLevels = gradeLevels;
        UpdatedAt = DateTime.UtcNow;
    }
}
