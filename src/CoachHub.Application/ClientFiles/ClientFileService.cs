using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Media;
using CoachHub.Domain.ClientFiles;
using CoachHub.Domain.Media;

namespace CoachHub.Application.ClientFiles;

public sealed record ClientFileInput(string Title, string? Description = null);

public sealed record ClientFileResponse(
    Guid Id,
    Guid ClientId,
    Guid MediaId,
    string Title,
    string? Description,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset SharedAt);

public interface IClientFileRepository
{
    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<(ClientFile File, MediaAsset Media)>> ListAsync(Guid clientId, CancellationToken cancellationToken);
    Task<(ClientFile File, MediaAsset Media)?> FindAsync(Guid clientId, Guid id, CancellationToken cancellationToken);
    void Add(ClientFile file);
    void Remove(ClientFile file);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Files the coach shares with a client (§43): PDFs, images, videos and documents, stored as the client's media and
/// opened through <c>/media/{id}</c>. The coach shares, renames and removes them; the client lists and opens them.
/// </summary>
public sealed class ClientFileService(
    IClientFileRepository repository,
    MediaService media,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ClientFileResponse>> ListAsync(Guid clientId, CancellationToken cancellationToken)
    {
        await CoachOfAsync(clientId, cancellationToken);
        return (await repository.ListAsync(clientId, cancellationToken))
            .OrderByDescending(x => x.File.SharedAt).Select(x => Map(x.File, x.Media)).ToArray();
    }

    public async Task<ClientFileResponse> ShareAsync(
        Guid clientId, Stream content, string fileName, string contentType, long sizeBytes, ClientFileInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var coachId = await CoachOfAsync(clientId, cancellationToken);
        var title = string.IsNullOrWhiteSpace(input.Title) ? Path.GetFileNameWithoutExtension(fileName) : input.Title;
        // Check the description first, so an invalid one stores nothing.
        DomainValidation.Run(() => ClientFile.Share(coachId, clientId, Guid.NewGuid(), title, input.Description, currentUser.UserId,
            timeProvider.GetUtcNow()), "title");
        var uploaded = await media.UploadAsync(content, fileName, contentType, sizeBytes,
            MediaOwner.OfClient(coachId, clientId, currentUser.UserId), MediaUploadPolicy.Shared, cancellationToken);
        var file = ClientFile.Share(coachId, clientId, uploaded.Id, title, input.Description, currentUser.UserId, timeProvider.GetUtcNow());
        repository.Add(file);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await media.DeleteAsync(uploaded.Id, CancellationToken.None);
            throw;
        }
        return new ClientFileResponse(file.Id, clientId, uploaded.Id, file.Title, file.Description, uploaded.OriginalFileName,
            uploaded.ContentType, uploaded.SizeBytes, file.SharedAt);
    }

    public async Task<ClientFileResponse> UpdateAsync(Guid clientId, Guid id, ClientFileInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (file, asset) = await FindAsync(clientId, id, cancellationToken);
        DomainValidation.Run(() => file.Describe(input.Title, input.Description), "title");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(file, asset);
    }

    /// <summary>Stops sharing the file and deletes it.</summary>
    public async Task DeleteAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        var (file, asset) = await FindAsync(clientId, id, cancellationToken);
        repository.Remove(file);
        await repository.SaveChangesAsync(cancellationToken);
        try
        {
            await media.DeleteAsync(asset.Id, cancellationToken);
        }
        catch (ConflictException)
        {
            // The file is also attached to a chat message; the content stays with the message.
        }
    }

    private static ClientFileResponse Map(ClientFile file, MediaAsset asset) => new(
        file.Id, file.ClientId, file.MediaId, file.Title, file.Description, asset.OriginalFileName, asset.ContentType, asset.SizeBytes, file.SharedAt);

    private async Task<(ClientFile File, MediaAsset Media)> FindAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        await CoachOfAsync(clientId, cancellationToken);
        return await repository.FindAsync(clientId, id, cancellationToken) ?? throw new NotFoundException("File", id);
    }

    private async Task<Guid> CoachOfAsync(Guid clientId, CancellationToken cancellationToken) =>
        await repository.FindClientCoachIdAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);
}
