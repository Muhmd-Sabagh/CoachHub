using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Domain.Media;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Tests.Clients;

public sealed class ClientServiceTests
{
    [Fact]
    public async Task Client_code_generation_retries_collisions_and_keeps_codes_distinct()
    {
        var repository = new FakeRepository();
        var generator = new FakeGenerator();
        var service = new ClientService(
            repository, generator, TestTenancy.CoachOwnership(), new FakeCoachRepository(),
            new MediaService(new NoMediaStorage(), new NoMediaRepository()), TimeProvider.System);

        var result = await service.CreateAsync(
            new ClientCreateInput("Client", null, null, new DateOnly(2026, 1, 1)),
            CancellationToken.None);

        Assert.Equal("BBBBBBBB", result.ClientCode);
        Assert.Equal("CCCCCCCCCC", result.FormCode);
        Assert.Equal(2, repository.ClientCodeChecks);
        Assert.NotEqual(result.ClientCode, result.FormCode);
        Assert.Equal(TestTenancy.CoachId, result.CoachId);
    }

    private sealed class NoMediaStorage : IMediaStorage
    {
        public Task<StoredMedia> StoreAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoMediaRepository : IMediaRepository
    {
        public Task AddAsync(MediaAsset media, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<MediaAsset?>(null);
        public Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeGenerator : IClientCodeGenerator
    {
        private int _clientCalls;
        public string GenerateClientCode() => ++_clientCalls == 1 ? "AAAAAAAA" : "BBBBBBBB";
        public string GenerateFormCode() => "CCCCCCCCCC";
    }

    private sealed class FakeRepository : IClientRepository
    {
        public int ClientCodeChecks { get; private set; }
        public Task<PagedResult<Client>> ListAsync(ClientQuery query, DateOnly today, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<Client>([], 1, 20, 0));
        public Task<Client?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Client?>(null);
        public Task<bool> ClientCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(ClientCodeChecks++ == 0);
        public Task<bool> FormCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(Client client, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> DeleteAsync(Client client, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> HasProtectedHistoryAsync(Client client, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task TransferAsync(Client client, Guid toCoachId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Subscription?> FindSubscriptionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Subscription?>(null);
        public Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddRenewalAsync(SubscriptionRenewal renewal, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}