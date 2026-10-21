using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.FitnessTests;
using CoachHub.Domain.Clients;
using CoachHub.Domain.FitnessTests;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.FitnessTests;

public sealed class TestDefinitionConfiguration : IEntityTypeConfiguration<TestDefinition>
{
    public void Configure(EntityTypeBuilder<TestDefinition> builder)
    {
        builder.ToTable("TestDefinitions", table => table.HasCheckConstraint("CK_TestDefinitions_CutOffs",
            "([GoodThreshold] IS NULL AND [PoorThreshold] IS NULL) OR ([GoodThreshold] IS NOT NULL AND [PoorThreshold] IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(TestDefinition.MaximumNameLength).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(TestDefinition.MaximumNameLength);
        builder.Property(x => x.InstructionsEn).HasMaxLength(TestDefinition.MaximumTextLength);
        builder.Property(x => x.InstructionsAr).HasMaxLength(TestDefinition.MaximumTextLength);
        builder.Property(x => x.YouTubeUrl).HasMaxLength(500);
        builder.Property(x => x.GoodThreshold).HasPrecision(9, 2);
        builder.Property(x => x.PoorThreshold).HasPrecision(9, 2);
        builder.HasIndex(x => new { x.CoachId, x.Kind, x.NameEn });
        builder.HasData(TestDefinitionSeed.All.Select(x => new
        {
            x.Id,
            CoachId = (Guid?)null,
            x.Kind,
            x.NameEn,
            NameAr = (string?)x.NameAr,
            x.Unit,
            x.HigherIsBetter,
            GoodThreshold = (decimal?)null,
            PoorThreshold = (decimal?)null,
            InstructionsEn = (string?)x.InstructionsEn,
            InstructionsAr = (string?)null,
            YouTubeUrl = (string?)null,
            IsActive = true
        }));
    }
}

/// <summary>
/// The platform tests every installation starts with (§34–§36); their identifiers are fixed. They carry no cut-offs,
/// because norms depend on age and sex: administrators add cut-offs, or coaches classify by hand.
/// </summary>
public static class TestDefinitionSeed
{
    public static readonly (Guid Id, TestKind Kind, string NameEn, string NameAr, TestUnit Unit, bool HigherIsBetter, string InstructionsEn)[] All =
    [
        (new("7d1f3a20-0001-4b6e-8c2d-1a9e5f000001"), TestKind.Mobility, "Sit and reach", "الجلوس والوصول", TestUnit.Centimeters, true,
            "Sit with legs straight and feet against the box. Reach forward slowly and hold for two seconds. Record the best of three reaches."),
        (new("7d1f3a20-0002-4b6e-8c2d-1a9e5f000002"), TestKind.Mobility, "Overhead squat", "القرفصاء مع رفع الذراعين", TestUnit.Points, true,
            "Hold a dowel overhead with straight arms and squat as deep as you can. Score 0 (pain) to 3 (full depth, heels down, torso upright)."),
        (new("7d1f3a20-0003-4b6e-8c2d-1a9e5f000003"), TestKind.Mobility, "Shoulder reach", "مرونة الكتف", TestUnit.Centimeters, false,
            "Reach one hand over the shoulder and the other up the back. Measure the gap between the fists; record 0 when they touch."),
        (new("7d1f3a20-0004-4b6e-8c2d-1a9e5f000004"), TestKind.Mobility, "Knee to wall", "الركبة إلى الحائط", TestUnit.Centimeters, true,
            "Lunge facing a wall and push the knee to the wall with the heel down. Record the longest toe-to-wall distance."),
        (new("7d1f3a20-0005-4b6e-8c2d-1a9e5f000005"), TestKind.Strength, "Bench press 1RM", "ضغط البنش - أقصى تكرار واحد", TestUnit.Kilograms, true,
            "Warm up, then work up to the heaviest single repetition with full range and a spotter."),
        (new("7d1f3a20-0006-4b6e-8c2d-1a9e5f000006"), TestKind.Strength, "Back squat 1RM", "القرفصاء الخلفي - أقصى تكرار واحد", TestUnit.Kilograms, true,
            "Warm up, then work up to the heaviest single repetition to at least parallel depth, with safety bars set."),
        (new("7d1f3a20-0007-4b6e-8c2d-1a9e5f000007"), TestKind.Strength, "Deadlift 1RM", "الرفعة المميتة - أقصى تكرار واحد", TestUnit.Kilograms, true,
            "Warm up, then work up to the heaviest single repetition with a neutral spine."),
        (new("7d1f3a20-0008-4b6e-8c2d-1a9e5f000008"), TestKind.Strength, "Push-ups (max reps)", "تمرين الضغط - أقصى عدد", TestUnit.Repetitions, true,
            "Do as many full push-ups as you can without resting, chest to a fist's height from the floor."),
        (new("7d1f3a20-0009-4b6e-8c2d-1a9e5f000009"), TestKind.Strength, "Pull-ups (max reps)", "العقلة - أقصى عدد", TestUnit.Repetitions, true,
            "From a dead hang, pull the chin over the bar as many times as you can without kipping."),
        (new("7d1f3a20-000a-4b6e-8c2d-1a9e5f00000a"), TestKind.Endurance, "2.4 km run", "جري 2.4 كم", TestUnit.Seconds, false,
            "Run 2.4 km (1.5 miles) on a flat course as fast as you can. Record the time in seconds."),
        (new("7d1f3a20-000b-4b6e-8c2d-1a9e5f00000b"), TestKind.Endurance, "12-minute run", "جري 12 دقيقة", TestUnit.Meters, true,
            "Run or walk as far as you can in 12 minutes on a flat course. Record the distance in metres."),
        (new("7d1f3a20-000c-4b6e-8c2d-1a9e5f00000c"), TestKind.Endurance, "1.6 km walk", "مشي 1.6 كم", TestUnit.Seconds, false,
            "Walk 1.6 km (1 mile) as fast as you can without running. Record the time in seconds."),
        (new("7d1f3a20-000d-4b6e-8c2d-1a9e5f00000d"), TestKind.Endurance, "Push-ups in 1 minute", "تمرين الضغط في دقيقة", TestUnit.Repetitions, true,
            "Do as many full push-ups as you can in 60 seconds; resting in the top position is allowed."),
        (new("7d1f3a20-000e-4b6e-8c2d-1a9e5f00000e"), TestKind.Endurance, "Plank hold", "ثبات البلانك", TestUnit.Seconds, true,
            "Hold a forearm plank with a straight line from head to heels. Stop when the hips drop; record the time in seconds."),
        (new("7d1f3a20-000f-4b6e-8c2d-1a9e5f00000f"), TestKind.Endurance, "3-minute step test", "اختبار الصعود 3 دقائق", TestUnit.BeatsPerMinute, false,
            "Step up and down a 30 cm step at 96 beats per minute for 3 minutes, sit, and count the heart rate for one minute."),
    ];
}

public sealed class TestAssignmentConfiguration : IEntityTypeConfiguration<TestAssignment>
{
    public void Configure(EntityTypeBuilder<TestAssignment> builder)
    {
        builder.ToTable("TestAssignments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ClientId, x.TestDefinitionId }).IsUnique();
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TestDefinition>().WithMany().HasForeignKey(x => x.TestDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TestResultConfiguration : IEntityTypeConfiguration<TestResult>
{
    public void Configure(EntityTypeBuilder<TestResult> builder)
    {
        builder.ToTable("TestResults", table => table.HasCheckConstraint("CK_TestResults_Value", "[Value] >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Classification).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RecordedBy).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(TestDefinition.MaximumNameLength).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(TestDefinition.MaximumNameLength);
        builder.Property(x => x.Notes).HasMaxLength(TestResult.MaximumNotesLength);
        builder.Property(x => x.Value).HasPrecision(9, 2);
        builder.Property(x => x.GoodThreshold).HasPrecision(9, 2);
        builder.Property(x => x.PoorThreshold).HasPrecision(9, 2);
        builder.HasIndex(x => new { x.ClientId, x.TestDefinitionId, x.Date });
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        // A result keeps its test after a transfer (it may be the previous coach's custom test), so the key restricts.
        builder.HasOne<TestDefinition>().WithMany().HasForeignKey(x => x.TestDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FitnessTestRepository(CoachHubDbContext dbContext) : IFitnessTestRepository
{
    public async Task<PagedResult<TestDefinition>> ListDefinitionsAsync(TestDefinitionQuery query, CancellationToken cancellationToken)
    {
        IQueryable<TestDefinition> tests = dbContext.Set<TestDefinition>().AsNoTracking();
        if (query.SearchTerm is { } term)
            tests = tests.Where(x => x.NameEn.Contains(term) || (x.NameAr != null && x.NameAr.Contains(term)));
        // The query filter already limits custom tests to the caller's coach.
        if (query.Scope == TestScope.Platform) tests = tests.Where(x => x.CoachId == null);
        else if (query.Scope == TestScope.Custom) tests = tests.Where(x => x.CoachId != null);
        if (query.Kind is { } kind) tests = tests.Where(x => x.Kind == kind);
        if (query.IsActive is { } active) tests = tests.Where(x => x.IsActive == active);
        var ordered = tests.OrderBy(x => x.Kind).ThenBy(x => x.NameEn).ThenBy(x => x.Id);
        var total = await ordered.LongCountAsync(cancellationToken);
        var page = await ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new PagedResult<TestDefinition>(page, query.PageNumber, query.PageSize, total);
    }

    public Task<TestDefinition?> FindDefinitionAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<TestDefinition>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TestDefinition>> FindDefinitionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? [] : await dbContext.Set<TestDefinition>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToArrayAsync(cancellationToken);

    // Deliberately cross-tenant: after a transfer another coach's client may still hold results of this test, and
    // those rows are hidden from its owner. Standalone existence checks, no tenant data is returned.
    public async Task<bool> IsDefinitionInUseAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Set<TestResult>().IgnoreQueryFilters().AnyAsync(x => x.TestDefinitionId == id, cancellationToken) ||
        await dbContext.Set<TestAssignment>().IgnoreQueryFilters().AnyAsync(x => x.TestDefinitionId == id, cancellationToken);

    public Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => (Guid?)x.CoachId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TestAssignment>> ListAssignmentsAsync(Guid clientId, CancellationToken cancellationToken) =>
        await dbContext.Set<TestAssignment>().Where(x => x.ClientId == clientId).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<TestResult>> ListResultsAsync(Guid clientId, CancellationToken cancellationToken) =>
        await dbContext.Set<TestResult>().AsNoTracking().Where(x => x.ClientId == clientId).ToArrayAsync(cancellationToken);

    public Task<TestResult?> FindResultAsync(Guid clientId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<TestResult>().SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, cancellationToken);

    public void Add(TestDefinition test) => dbContext.Set<TestDefinition>().Add(test);
    public void Add(TestAssignment assignment) => dbContext.Set<TestAssignment>().Add(assignment);
    public void Add(TestResult result) => dbContext.Set<TestResult>().Add(result);
    public void Remove(TestAssignment assignment) => dbContext.Set<TestAssignment>().Remove(assignment);
    public void Remove(TestResult result) => dbContext.Set<TestResult>().Remove(result);

    public async Task DeleteDefinitionAsync(TestDefinition test, CancellationToken cancellationToken)
    {
        dbContext.Set<TestDefinition>().Remove(test);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("The test is assigned to clients or has results. Deactivate it instead.");
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
