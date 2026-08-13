using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.Tests.ReferenceData;

public sealed class PackageServiceTests
{
    [Fact]
    public async Task Create_accepts_optional_arabic_name_and_maps_response()
    {
        var repository = new FakeRepository();
        var service = new PackageService(repository);

        var response = await service.CreateAsync(
            new PackageInput("Premium", null, "Coaching", true),
            CancellationToken.None);

        Assert.Equal("Premium", response.NameEn);
        Assert.Null(response.NameAr);
        Assert.NotNull(repository.Added);
    }

    [Fact]
    public async Task Create_rejects_missing_english_name()
    {
        var repository = new FakeRepository();
        var service = new PackageService(repository);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(
                new PackageInput(" ", "العربية", null, true),
                CancellationToken.None));

        Assert.Contains("nameEn", exception.Errors);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task Duplicate_english_name_is_a_conflict()
    {
        var repository = new FakeRepository { KeyExists = true };
        var service = new PackageService(repository);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(
                new PackageInput("Premium", null, null, true),
                CancellationToken.None));
    }

    private sealed class FakeRepository : IReferenceRepository<Package>
    {
        public Package? Added { get; private set; }
        public bool KeyExists { get; init; }

        public Task<PagedResult<Package>> ListAsync(ReferenceListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Package>([], query.PageNumber, query.PageSize, 0));
        public Task<Package?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Package?>(null);
        public Task<bool> KeyExistsAsync(string key, Guid? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(KeyExists);
        public Task AddAsync(Package entity, CancellationToken cancellationToken)
        {
            Added = entity;
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Package entity, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}