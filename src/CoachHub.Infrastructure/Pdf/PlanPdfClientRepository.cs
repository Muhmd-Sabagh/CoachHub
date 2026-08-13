using CoachHub.Application.Pdf;
using CoachHub.Domain.Clients;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Pdf;

public sealed class PlanPdfClientRepository(CoachHubDbContext dbContext) : IPlanPdfClientRepository
{
    public Task<PdfClientInfo?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().AsNoTracking().Where(x => x.Id == id)
            .Select(x => new PdfClientInfo(x.Name, x.ClientCode))
            .SingleOrDefaultAsync(cancellationToken);
}
