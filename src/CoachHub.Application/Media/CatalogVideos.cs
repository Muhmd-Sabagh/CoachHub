using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;

namespace CoachHub.Application.Media;

/// <summary>A file sent with a request: its content, name, declared type and size.</summary>
public sealed record MediaUpload(Stream Content, string FileName, string ContentType, long SizeBytes);

/// <summary>
/// Uploaded videos of catalog entries (P20: exercises §17, tests §34). A platform entry's video is platform media,
/// visible to every signed-in user; a coach's entry's video sits in that coach's library, visible to the coach and
/// their clients. Both play through <c>/media/{id}</c>.
/// </summary>
public sealed class CatalogVideos(MediaService media, ICurrentUser currentUser)
{
    public Task<MediaMetadata> UploadAsync(Guid? coachId, MediaUpload upload, CancellationToken cancellationToken)
    {
        var owner = coachId is { } coach
            ? MediaOwner.CoachLibrary(coach, currentUser.UserId)
            : MediaOwner.Platform(currentUser.UserId);
        return media.UploadAsync(upload.Content, upload.FileName, upload.ContentType, upload.SizeBytes, owner,
            MediaUploadPolicy.Video, cancellationToken);
    }

    /// <summary>Removes a video no entry uses any more; a file already gone is ignored.</summary>
    public async Task DeleteAsync(Guid? mediaId, CancellationToken cancellationToken)
    {
        if (mediaId is not { } id) return;
        try
        {
            await media.DeleteAsync(id, cancellationToken);
        }
        catch (NotFoundException)
        {
            // Already removed.
        }
    }
}
