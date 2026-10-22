using CoachHub.Application.Clients;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Domain.Media;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Tests.Clients;

public sealed class ClientServiceTests
{
    private readonly FakeRepository _repository = new();
    private readonly FakeAccounts _accounts = new();

    private ClientService Service() => new(
        _repository, TestTenancy.CoachOwnership(), new FakeCoachRepository(),
        new MediaService(new NoMediaStorage(), new NoMediaRepository()), _accounts, TimeProvider.System);

    [Fact]
    public async Task Creating_a_client_assigns_the_coach_and_starts_on_track()
    {
        var result = await Service().CreateAsync(
            new ClientCreateInput("Client", null, null, new DateOnly(2026, 1, 1)),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 1, 1), result.JoinDate);
        Assert.Equal(TestTenancy.CoachId, result.CoachId);
        Assert.Equal(ClientStatus.OnTrack, result.Status);
        Assert.Empty(_accounts.Invited);
    }

    [Fact]
    public async Task Creating_with_an_invitation_invites_and_needs_an_email()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(
            new ClientCreateInput("No email", null, null, null, Invite: true), CancellationToken.None));

        var created = await Service().CreateAsync(
            new ClientCreateInput("Invited", null, "invited@example.test", null, Invite: true), CancellationToken.None);

        Assert.Equal(created.Id, Assert.Single(_accounts.Invited).Id);
    }

    [Fact]
    public async Task Updating_a_client_keeps_the_login_in_step()
    {
        var created = await Service().CreateAsync(new ClientCreateInput("Client", null, null, null), CancellationToken.None);

        await Service().UpdateAsync(created.Id, new ClientUpdateInput(
            "Client", null, null, false), CancellationToken.None);

        Assert.False(Assert.Single(_accounts.Synced).IsActive);
    }

    [Fact]
    public async Task The_status_can_be_overridden_and_cleared()
    {
        var created = await Service().CreateAsync(new ClientCreateInput("Client", null, null, null), CancellationToken.None);

        var overridden = await Service().SetStatusAsync(created.Id, new ClientStatusInput(ClientStatus.NeedsReview, null), CancellationToken.None);
        var cleared = await Service().SetStatusAsync(created.Id, null, CancellationToken.None);

        Assert.Equal(ClientStatus.NeedsReview, overridden.Status);
        Assert.Equal(ClientStatus.NeedsReview, overridden.StatusOverride);
        Assert.Equal(ClientStatus.OnTrack, cleared.Status);
        await Assert.ThrowsAsync<ValidationException>(() => Service().SetStatusAsync(created.Id,
            new ClientStatusInput(ClientStatus.OnTrack, new DateOnly(2000, 1, 1)), CancellationToken.None));
    }

    private sealed class FakeAccounts : IClientAccounts
    {
        public List<Client> Invited { get; } = [];
        public List<Client> Synced { get; } = [];
        public Task<ClientAccountResponse> GetAsync(Guid clientId, CancellationToken cancellationToken) =>
            Task.FromResult(new ClientAccountResponse(ClientAccountState.None, null, null));
        public Task<ClientAccountResponse> InviteAsync(Client client, CancellationToken cancellationToken)
        {
            Invited.Add(client);
            return Task.FromResult(new ClientAccountResponse(ClientAccountState.Invited, client.Email, null));
        }
        public Task SyncAsync(Client client, CancellationToken cancellationToken)
        {
            Synced.Add(client);
            return Task.CompletedTask;
        }
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

    private sealed class FakeRepository : IClientRepository
    {
        public Task<PagedResult<Client>> ListAsync(ClientQuery query, DateOnly today, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<Client>([], 1, 20, 0));
        public Task<Client?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Clients.SingleOrDefault(client => client.Id == id));
        public List<Client> Clients { get; } = [];
        public Task<ClientProfile?> FindProfileAsync(Guid clientId, CancellationToken cancellationToken) => Task.FromResult<ClientProfile?>(null);
        public Task<decimal?> FindLatestWeightKgAsync(Guid clientId, CancellationToken cancellationToken) => Task.FromResult<decimal?>(null);
        public Task AddProfileAsync(ClientProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddAsync(Client client, CancellationToken cancellationToken)
        {
            Clients.Add(client);
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> DeleteAsync(Client client, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> HasProtectedHistoryAsync(Client client, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task TransferAsync(Client client, Guid toCoachId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Subscription?> FindSubscriptionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Subscription?>(null);
        public Task<IReadOnlyDictionary<Guid, SubscriptionPaymentStatus>> SubscriptionPaymentsAsync(IReadOnlyCollection<Guid> subscriptionIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, SubscriptionPaymentStatus>>(new Dictionary<Guid, SubscriptionPaymentStatus>());
        public Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddRenewalAsync(SubscriptionRenewal renewal, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}