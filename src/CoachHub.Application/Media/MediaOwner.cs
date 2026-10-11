namespace CoachHub.Application.Media;

/// <summary>Who a new file belongs to. See <see cref="Domain.Media.MediaAsset"/> for the visibility rule.</summary>
public sealed record MediaOwner(Guid? CoachId, Guid? ClientId, Guid? UploadedByUserId)
{
    /// <summary>Platform media such as catalog images: visible to every signed-in user.</summary>
    public static MediaOwner Platform(Guid? uploadedByUserId) => new(null, null, uploadedByUserId);

    /// <summary>A coach's library: visible to the coach and their clients.</summary>
    public static MediaOwner CoachLibrary(Guid coachId, Guid? uploadedByUserId) => new(coachId, null, uploadedByUserId);

    /// <summary>A client's own media: visible to the client and their coach.</summary>
    public static MediaOwner OfClient(Guid coachId, Guid clientId, Guid? uploadedByUserId) =>
        new(coachId, clientId, uploadedByUserId);
}
