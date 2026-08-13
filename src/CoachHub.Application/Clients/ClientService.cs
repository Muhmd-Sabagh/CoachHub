using System.Net.Mail;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Clients;

public sealed class ClientService(
    IClientRepository repository,
    IClientCodeGenerator codeGenerator,
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
        return new ClientDetailResponse(
            Map(client, today),
            client.Subscriptions
                .OrderByDescending(subscription => subscription.StartDate)
                .Select(subscription => SubscriptionService.Map(subscription, today))
                .ToArray());
    }

    public async Task<ClientResponse> CreateAsync(
        ClientCreateInput input,
        CancellationToken cancellationToken)
    {
        Validate(input.Name, input.Phone, input.Email);
        var clientCode = await UniqueCodeAsync(true, cancellationToken);
        var formCode = await UniqueCodeAsync(false, cancellationToken);
        var client = Client.Create(
            clientCode,
            formCode,
            input.Name,
            input.Phone,
            input.Email,
            input.JoinDate ?? Today());
        await repository.AddAsync(client, cancellationToken);
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
        return Map(client, Today());
    }

    public async Task<string> RegenerateFormCodeAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var client = await FindRequiredAsync(id, cancellationToken);
        var formCode = await UniqueCodeAsync(false, cancellationToken);
        client.RegenerateFormCode(formCode);
        await repository.SaveChangesAsync(cancellationToken);
        return formCode;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await FindRequiredAsync(id, cancellationToken);
        if (client.Subscriptions.Any(subscription => subscription.Renewals.Count > 0))
        {
            throw new ConflictException(
                "A client with subscription renewal history cannot be deleted.");
        }
        await repository.DeleteAsync(client, cancellationToken);
    }

    internal static ClientResponse Map(Client client, DateOnly today) => new(
        client.Id,
        client.ClientCode,
        client.FormCode,
        client.Name,
        client.Phone,
        client.Email,
        client.JoinDate,
        client.GetSubscriptionStatus(today),
        client.DietStatus,
        client.WorkoutStatus,
        client.IsActive,
        client.Subscriptions.Count);

    private async Task<string> UniqueCodeAsync(bool clientCode, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = clientCode
                ? codeGenerator.GenerateClientCode()
                : codeGenerator.GenerateFormCode();
            var exists = clientCode
                ? await repository.ClientCodeExistsAsync(code, cancellationToken)
                : await repository.FormCodeExistsAsync(code, cancellationToken);
            if (!exists) return code;
        }
        throw new ConflictException("Unable to allocate a unique client access code.");
    }

    private async Task<Client> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken)
        ?? throw new NotFoundException("Client", id);

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

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