using CoachHub.Domain.FoodTracking;

namespace CoachHub.Application.FoodTracking;

public interface IFoodTrackingRepository
{
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
