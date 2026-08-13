using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Media;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.Tests.Nutrition;

public sealed class FoodServiceTests
{
    [Fact]
    public async Task Negative_macro_is_rejected_before_persistence()
    {
        var foodRepository = new FakeFoodRepository();
        var service = new FoodService(
            foodRepository,
            new FakeCategoryRepository(FoodCategory.Create("Protein", null)),
            new FakeMediaRepository());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(
                new FoodInput(
                    "Chicken",
                    null,
                    Guid.NewGuid(),
                    "gram",
                    100,
                    -1,
                    0,
                    0,
                    null),
                CancellationToken.None));

        Assert.Contains("proteinPer100", exception.Errors);
        Assert.Null(foodRepository.Added);
    }

    [Fact]
    public async Task Missing_category_is_reported_as_validation()
    {
        var service = new FoodService(
            new FakeFoodRepository(),
            new FakeCategoryRepository(null),
            new FakeMediaRepository());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(
                new FoodInput("Rice", null, Guid.NewGuid(), "gram", 130, 2.7m, 28, 0.3m, null),
                CancellationToken.None));

        Assert.Contains("foodCategoryId", exception.Errors);
    }

    private sealed class FakeFoodRepository : IFoodRepository
    {
        public FoodItem? Added { get; private set; }
        public Task<PagedResult<FoodItem>> ListAsync(FoodQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<FoodItem>([], query.PageNumber, query.PageSize, 0));
        public Task<FoodItem?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<FoodItem?>(null);
        public Task AddAsync(FoodItem food, CancellationToken cancellationToken) { Added = food; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(FoodItem food, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<LegacyFoodImportRecord?> FindLegacyImportAsync(int legacyId, CancellationToken cancellationToken) => Task.FromResult<LegacyFoodImportRecord?>(null);
        public Task<FoodCategory> GetOrCreateCategoryAsync(string name, CancellationToken cancellationToken) => Task.FromResult(FoodCategory.Create(name, null));
        public Task AddImportedAsync(FoodItem food, LegacyFoodImportRecord importRecord, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCategoryRepository(FoodCategory? category) : IReferenceRepository<FoodCategory>
    {
        public Task<PagedResult<FoodCategory>> ListAsync(ReferenceListQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<FoodCategory>([], 1, 20, 0));
        public Task<FoodCategory?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(category);
        public Task<bool> KeyExistsAsync(string key, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(FoodCategory entity, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(FoodCategory entity, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMediaRepository : IMediaRepository
    {
        public Task AddAsync(MediaAsset media, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<MediaAsset?>(null);
        public Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}