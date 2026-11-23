using CoachHub.Domain.FoodTracking;

namespace CoachHub.Application.FoodTracking;

public interface IFoodTrackingRepository
{
    /// <summary>
    /// The lineage of the given diet variants, meals and meal options, of any version (P22): stored day choices and
    /// logged meals match the same row of a later version through it.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> FindLineagesAsync(IReadOnlyCollection<Guid> rowIds, CancellationToken cancellationToken);

    /// <summary>The client's current coach, or null when the client is not visible to the caller.</summary>
    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>Every version of the diet plans assigned to the client.</summary>
    Task<IReadOnlyList<DietVersion>> ListDietVersionsAsync(Guid clientId, CancellationToken cancellationToken);

    Task<NutritionDay?> FindDayAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<NutritionDay>> ListDaysAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<FoodLogEntry>> ListEntriesAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<FoodLogEntry?> FindEntryAsync(Guid id, CancellationToken cancellationToken);

    void Add(NutritionDay day);
    void Add(IEnumerable<FoodLogEntry> entries);
    void Remove(FoodLogEntry entry);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
