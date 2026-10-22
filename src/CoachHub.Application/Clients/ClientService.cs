using System.Net.Mail;
using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Clients;

public sealed class ClientService(
    IClientRepository repository,
    IClientCodeGenerator codeGenerator,
    CoachOwnership ownership,
    ICoachRepository coaches,
    MediaService media,
    IClientAccounts accounts,
    TimeProvider timeProvider)
{
    public async Task<PagedResult<ClientResponse>> ListAsync(
        ClientQuery query,
        CancellationToken cancellationToken)
    {
        var today = Today();
        var page = await repository.ListAsync(query.Normalize(), today, cancellationToken);
        return new PagedResult<ClientResponse>(
            page.Items.Select(client => Map(client, today)).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<ClientDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await FindRequiredAsync(id, cancellationToken);
        var today = Today();
        var payments = await repository.SubscriptionPaymentsAsync(client.Subscriptions.Select(x => x.Id).ToArray(), cancellationToken);
        return new ClientDetailResponse(
            Map(client, today),
            client.Subscriptions
                .OrderByDescending(subscription => subscription.StartDate)
                .Select(subscription => SubscriptionService.Map(subscription, today,
                    payments.GetValueOrDefault(subscription.Id, SubscriptionPaymentStatus.NoInvoice)))
                .ToArray(),
            await accounts.GetAsync(client.Id, cancellationToken));
    }

    /// <summary>Creates a client (§3); with <see cref="ClientCreateInput.Invite"/>, also their login and invitation.</summary>
    public async Task<ClientResponse> CreateAsync(
        ClientCreateInput input,
        CancellationToken cancellationToken)
    {
        Validate(input.Name, input.Phone, input.Email);
        if (input.Invite && string.IsNullOrWhiteSpace(input.Email))
            throw Validation("email", "An email address is needed to invite the client.");
        var coachId = await ownership.ForNewRecordAsync(input.CoachId, cancellationToken);
        var clientCode = await UniqueClientCodeAsync(cancellationToken);
        var client = Client.Create(
            coachId,
            clientCode,
            input.Name,
            input.Phone,
            input.Email,
            input.JoinDate ?? Today());
        await repository.AddAsync(client, cancellationToken);
        if (input.Invite) await accounts.InviteAsync(client, cancellationToken);
        return Map(client, Today());
    }

    public async Task<ClientResponse> UpdateAsync(
        Guid id,
        ClientUpdateInput input,
        CancellationToken cancellationToken)
    {
        Validate(input.Name, input.Phone, input.Email);
        var client = await FindRequiredAsync(id, cancellationToken);
        client.Update(
            input.Name,
            input.Phone,
            input.Email,
            input.DietStatus,
            input.WorkoutStatus,
            input.IsActive);
        await repository.SaveChangesAsync(cancellationToken);
        // Deactivating a client also disables their login and signs them out everywhere (§2).
        await accounts.SyncAsync(client, cancellationToken);
        return Map(client, Today());
    }

    /// <summary>Creates the client's login if needed and (re)sends the set-password invitation.</summary>
    public async Task<ClientAccountResponse> InviteAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await FindRequiredAsync(id, cancellationToken);
        if (!client.IsActive) throw new ConflictException("Activate the client before inviting them.");
        if (string.IsNullOrWhiteSpace(client.Email))
            throw Validation("email", "Add the client's email address before inviting them.");
        return await accounts.InviteAsync(client, cancellationToken);
    }

    /// <summary>Sets the client's status by hand (§39); <c>null</c> input clears the override.</summary>
    public async Task<ClientResponse> SetStatusAsync(Guid id, ClientStatusInput? input, CancellationToken cancellationToken)
    {
        var client = await FindRequiredAsync(id, cancellationToken);
        if (input is null) client.ClearStatusOverride();
        else DomainValidation.Run(() => client.OverrideStatus(input.Status, input.Until, Today()), "status");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(client, Today());
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await FindRequiredAsync(id, cancellationToken);
        if (await repository.HasProtectedHistoryAsync(client, cancellationToken))
        {
            throw new ConflictException(
                "A client with invoices, renewals, or another coach's subscriptions cannot be deleted. Deactivate the client instead.");
        }
        var storageKeys = await repository.DeleteAsync(client, cancellationToken);
        await media.DeleteContentAsync(storageKeys, CancellationToken.None);
    }

    public async Task<ClientResponse> TransferAsync(
        Guid id,
        ClientTransferInput input,
        CancellationToken cancellationToken)
    {
        var client = await FindRequiredAsync(id, cancellationToken);
        var target = await coaches.FindAsync(input.CoachId, cancellationToken)
            ?? throw new NotFoundException("Coach", input.CoachId);
        if (!target.IsActive) throw new ConflictException("The selected coach is inactive.");
        if (target.Id == client.CoachId) throw new ConflictException("The client already belongs to this coach.");
        await repository.TransferAsync(client, target.Id, cancellationToken);
        var transferred = await FindRequiredAsync(id, cancellationToken);
        return Map(transferred, Today());
    }

    internal static ClientResponse Map(Client client, DateOnly today) => new(
        client.Id,
        client.CoachId,
        client.ClientCode,
        client.Name,
        client.Phone,
        client.Email,
        client.JoinDate,
        client.GetSubscriptionStatus(today),
        client.DietStatus,
        client.WorkoutStatus,
        client.IsActive,
        client.Subscriptions.Count,
        client.GetStatus(today),
        client.StatusOverride,
        client.StatusOverrideUntil);

    private async Task<string> UniqueClientCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = codeGenerator.GenerateClientCode();
            if (!await repository.ClientCodeExistsAsync(code, cancellationToken)) return code;
        }
        throw new ConflictException("Unable to allocate a unique client code.");
    }

    private async Task<Client> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken)
        ?? throw new NotFoundException("Client", id);

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static void Validate(string name, string? phone, string? email)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["A client name is required."];
        else if (name.Trim().Length > 255) errors["name"] = ["Name cannot exceed 255 characters."];
        if (!string.IsNullOrWhiteSpace(phone) && phone.Trim().Length > 50)
            errors["phone"] = ["Phone cannot exceed 50 characters."];
        if (!string.IsNullOrWhiteSpace(email) &&
            (!MailAddress.TryCreate(email.Trim(), out var address) ||
             !address.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)))
            errors["email"] = ["A valid email address is required."];
        if (errors.Count > 0) throw new ValidationException(errors);
    }
}
