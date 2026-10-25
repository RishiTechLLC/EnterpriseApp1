namespace SchoolCalendar.Domain.Entities;

public class School
{
    private readonly List<AcademicProgram> _programs = [];

    private School() { }

    public School(string name, string code, string? address, string timeZone, string createdBy)
    {
        Id = Guid.NewGuid();
        Name = name;
        Code = code;
        Address = address;
        TimeZone = timeZone;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Address { get; private set; }
    public string TimeZone { get; private set; } = default!;
    public string CreatedBy { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<AcademicProgram> Programs => _programs;

    public void Update(string name, string code, string? address, string timeZone)
    {
        Name = name;
        Code = code;
        Address = address;
        TimeZone = timeZone;
        UpdatedAt = DateTime.UtcNow;
    }
}
