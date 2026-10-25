namespace SchoolCalendar.Domain.Enums;

public enum CalendarStatus
{
    Draft = 0,
    Published = 1
}

public enum CalendarEventType
{
    TermStart = 0,
    TermEnd = 1,
    Holiday = 2,
    Break = 3,
    Exam = 4,
    ProfessionalDevelopment = 5,
    EarlyDismissal = 6,
    ParentTeacherConference = 7,
    SchoolEvent = 8,
    Other = 9
}
