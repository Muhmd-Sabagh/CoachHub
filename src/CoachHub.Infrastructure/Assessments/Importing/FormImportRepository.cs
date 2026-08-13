using CoachHub.Application.Assessments.Importing;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Clients;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoachHub.Infrastructure.Assessments.Importing;

public sealed class FormImportRepository(CoachHubDbContext dbContext) : IFormImportRepository
{
    public async Task<ImportProfileGraph?> FindProfileAsync(Guid id, CancellationToken token)
    {
        var profile = await dbContext.Set<FormImportProfile>().SingleOrDefaultAsync(item => item.Id == id, token);
        if (profile is null) return null;
        var mappings = await dbContext.Set<FormImportColumnMapping>().AsNoTracking()
            .Where(item => item.FormImportProfileId == id).OrderBy(item => item.ExternalColumnKey).ToArrayAsync(token);
        return new(profile, mappings);
    }
    public async Task AddProfileAsync(
        FormImportProfile profile, IReadOnlyList<FormImportColumnMapping> mappings, CancellationToken token)
    {
        dbContext.Add(profile); dbContext.AddRange(mappings); await dbContext.SaveChangesAsync(token);
    }
    public async Task ReplaceProfileAsync(
        FormImportProfile profile, IReadOnlyList<FormImportColumnMapping> mappings, CancellationToken token)
    {
        var existing = await dbContext.Set<FormImportColumnMapping>()
            .Where(item => item.FormImportProfileId == profile.Id).ToArrayAsync(token);
        dbContext.RemoveRange(existing); dbContext.AddRange(mappings); await dbContext.SaveChangesAsync(token);
    }
    public Task<Client?> FindClientByFormCodeAsync(string formCode, CancellationToken token) =>
        dbContext.Set<Client>().SingleOrDefaultAsync(item => item.FormCode == formCode, token);
    public Task<bool> ImportFingerprintExistsAsync(string fingerprint, CancellationToken token) =>
        dbContext.Set<FormSubmission>().AnyAsync(item => item.ImportFingerprint == fingerprint, token);

    public async Task<bool> TrySubmitAsync(
        FormSubmission submission, IReadOnlyList<FormAnswer> answers, CancellationToken token)
    {
        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational()) transaction = await dbContext.Database.BeginTransactionAsync(token);
        try
        {
            dbContext.Add(submission); dbContext.AddRange(answers);
            await dbContext.SaveChangesAsync(token);
            if (transaction is not null) await transaction.CommitAsync(token);
            return true;
        }
        catch (DbUpdateException)
        {
            if (transaction is not null) await transaction.RollbackAsync(token);
            dbContext.ChangeTracker.Clear();
            return false;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }
}