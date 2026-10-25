using Microsoft.EntityFrameworkCore;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Domain.Entities;

namespace SchoolCalendar.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<AcademicProgram> Programs => Set<AcademicProgram>();
    public DbSet<AcademicCalendar> Calendars => Set<AcademicCalendar>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<School>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
            e.Property(x => x.CreatedBy).HasMaxLength(128).IsRequired();
            e.HasMany(x => x.Programs).WithOne(p => p.School).HasForeignKey(p => p.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Programs).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<AcademicProgram>(e =>
        {
            e.ToTable("Programs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.GradeLevels).HasMaxLength(100);
            e.HasMany(x => x.Calendars).WithOne(c => c.Program).HasForeignKey(c => c.ProgramId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Calendars).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<AcademicCalendar>(e =>
        {
            e.ToTable("Calendars");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.AcademicYear).HasMaxLength(9).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.ProgramId, x.AcademicYear });
            e.HasMany(x => x.Events).WithOne().HasForeignKey(ev => ev.CalendarId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<CalendarEvent>(e =>
        {
            e.ToTable("CalendarEvents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            e.HasIndex(x => new { x.CalendarId, x.StartDate });
        });
    }
}
