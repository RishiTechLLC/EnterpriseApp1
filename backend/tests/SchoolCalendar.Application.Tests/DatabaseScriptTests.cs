using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SchoolCalendar.Domain.Enums;
using SchoolCalendar.Infrastructure.Persistence;

namespace SchoolCalendar.Application.Tests;

/// <summary>
/// Guards the grate scripts in /db/sql: every script must be valid T-SQL, and the tables built by the
/// one-time "up" scripts must match the EF Core model exactly. Changing an entity without adding a
/// migration script (or vice versa) fails here instead of at runtime.
/// </summary>
public class DatabaseScriptTests
{
    private static readonly string SqlRoot = FindSqlRoot();

    public static TheoryData<string> AllScripts()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(SqlRoot, "*.sql", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            data.Add(Path.GetRelativePath(SqlRoot, file));
        return data;
    }

    [Theory]
    [MemberData(nameof(AllScripts))]
    public void Script_is_valid_tsql(string relativePath)
    {
        Parse(Path.Combine(SqlRoot, relativePath), out var errors);
        Assert.True(errors.Count == 0,
            string.Join(Environment.NewLine, errors.Select(e => $"{relativePath}({e.Line},{e.Column}): {e.Message}")));
    }

    [Fact]
    public void Anytime_scripts_are_rerunnable()
    {
        // grate re-runs functions/views/sprocs whenever they change, so they must use CREATE OR ALTER.
        foreach (var folder in new[] { "functions", "views", "sprocs" })
        foreach (var file in Directory.EnumerateFiles(Path.Combine(SqlRoot, folder), "*.sql"))
        {
            var script = Parse(file, out _);
            var creates = script.Batches.SelectMany(b => b.Statements)
                .Where(s => s is CreateFunctionStatement or CreateViewStatement or CreateProcedureStatement);
            Assert.Empty(creates.Select(_ => Path.GetFileName(file)));
        }
    }

    [Fact]
    public void Up_scripts_produce_the_tables_the_ef_model_expects()
    {
        var expected = ExpectedColumnsFromEfModel();
        var actual = ColumnsFromUpScripts();

        var missing = expected.Except(actual).ToList();
        var unexpected = actual.Except(expected).ToList();

        Assert.True(missing.Count == 0 && unexpected.Count == 0,
            $"Database scripts and EF model differ.{Environment.NewLine}" +
            $"In EF model but not in db/sql/up: {string.Join(", ", missing)}{Environment.NewLine}" +
            $"In db/sql/up but not in EF model: {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void Check_constraints_allow_every_enum_value()
    {
        var upScripts = string.Join('\n', Directory.EnumerateFiles(Path.Combine(SqlRoot, "up"), "*.sql").Select(File.ReadAllText));
        var missing = Enum.GetNames<CalendarEventType>().Concat(Enum.GetNames<CalendarStatus>())
            .Where(name => !upScripts.Contains($"N'{name}'"))
            .ToList();
        Assert.True(missing.Count == 0, $"Enum values not allowed by a CHECK constraint: {string.Join(", ", missing)}");
    }

    private static HashSet<string> ExpectedColumnsFromEfModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=unused").Options;
        using var db = new AppDbContext(options);
        var model = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;

        return model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties().Select(p => Describe(
                entity.GetTableName()!,
                p.GetColumnName(),
                p.GetColumnType(),
                p.IsColumnNullable())))
            .ToHashSet();
    }

    /// <summary>Replays CREATE TABLE / ALTER TABLE ADD / ALTER COLUMN / DROP COLUMN from the up scripts in grate's order.</summary>
    private static HashSet<string> ColumnsFromUpScripts()
    {
        var tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(Path.Combine(SqlRoot, "up"), "*.sql").Order(StringComparer.Ordinal))
        foreach (var statement in Parse(file, out _).Batches.SelectMany(b => b.Statements))
        {
            switch (statement)
            {
                case CreateTableStatement create:
                    var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in create.Definition.ColumnDefinitions)
                        columns[c.ColumnIdentifier.Value] = Describe(create.SchemaObjectName.BaseIdentifier.Value, c);
                    tables[create.SchemaObjectName.BaseIdentifier.Value] = columns;
                    break;

                case AlterTableAddTableElementStatement add:
                    var table = add.SchemaObjectName.BaseIdentifier.Value;
                    foreach (var c in add.Definition.ColumnDefinitions)
                        tables[table][c.ColumnIdentifier.Value] = Describe(table, c);
                    break;

                case AlterTableAlterColumnStatement alter when alter.DataType is not null:
                    var t = alter.SchemaObjectName.BaseIdentifier.Value;
                    tables[t][alter.ColumnIdentifier.Value] = Describe(t, alter.ColumnIdentifier.Value, TypeName(alter.DataType),
                        alter.AlterTableAlterColumnOption != AlterTableAlterColumnOption.NotNull);
                    break;

                case AlterTableDropTableElementStatement drop:
                    foreach (var element in drop.AlterTableDropTableElements.Where(e => e.TableElementType == TableElementType.Column))
                        tables[drop.SchemaObjectName.BaseIdentifier.Value].Remove(element.Name.Value);
                    break;

                case DropTableStatement dropTable:
                    foreach (var name in dropTable.Objects) tables.Remove(name.BaseIdentifier.Value);
                    break;
            }
        }

        return tables.Values.SelectMany(t => t.Values).ToHashSet();
    }

    private static string Describe(string table, ColumnDefinition c) =>
        Describe(table, c.ColumnIdentifier.Value, TypeName(c.DataType),
            c.Constraints.OfType<NullableConstraintDefinition>().FirstOrDefault()?.Nullable ?? true);

    private static string Describe(string table, string column, string? type, bool nullable) =>
        $"{table}.{column} {type?.ToLowerInvariant()} {(nullable ? "NULL" : "NOT NULL")}";

    private static string TypeName(DataTypeReference type)
    {
        var name = type.Name.BaseIdentifier.Value.ToLowerInvariant();
        if (type is not ParameterizedDataTypeReference { Parameters.Count: > 0 } p) return name;
        return $"{name}({string.Join(",", p.Parameters.Select(x => x.Value.ToLowerInvariant()))})";
    }

    private static TSqlScript Parse(string path, out IList<ParseError> errors)
    {
        using var reader = new StreamReader(path);
        // SQL Server 2022 / Azure SQL (compatibility level 160)
        return (TSqlScript)new TSql160Parser(initialQuotedIdentifiers: true).Parse(reader, out errors);
    }

    private static string FindSqlRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "db", "sql");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Could not find db/sql above the test output directory.");
    }
}
