using CoachHub.Application.Coaches;
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
    private static FoodInput Chicken(decimal protein = 31, Guid? coachId = null) =>
        new("Chicken", null, Guid.NewGuid(), FoodBaseUnit.Gram, 165, protein, 0, 3.6m, null, CoachId: coachId);

    private static (FoodService Service, FakeFoodRepository Foods) Create(
        FakeCurrentUser user, FoodCategory? category = null, FoodItem? existing = null)
    {
        var foods = new FakeFoodRepository(existing);
        var service = new FoodService(foods,
            new FakeCategoryRepository(category ?? FoodCategory.Create("Protein", null)),
            new FakeMediaRepository(), new CoachOwnership(user, new FakeCoachRepository()), user);
        return (service, foods);
    }

    [Fact]
    public async Task Negative_macro_is_rejected_before_persistence()
    {
        var (service, foods) = Create(FakeCurrentUser.Administrator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Chicken(protein: -1), CancellationToken.None));

        Assert.Contains("proteinPer100", exception.Errors);
        Assert.Null(foods.Added);
    }

    [Fact]
    public async Task Missing_category_is_reported_as_validation()
    {
        var foods = new FakeFoodRepository(null);
        var user = FakeCurrentUser.Administrator();
        var service = new FoodService(foods, new FakeCategoryRepository(null), new FakeMediaRepository(),
            new CoachOwnership(user, new FakeCoachRepository()), user);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Chicken(), CancellationToken.None));

        Assert.Contains("foodCategoryId", exception.Errors);
    }

    [Fact]
    public async Task Administrators_create_platform_foods_and_coaches_their_own()
    {
        var (admin, adminFoods) = Create(FakeCurrentUser.Administrator());
        var platform = await admin.CreateAsync(Chicken(), CancellationToken.None);
        Assert.False(platform.IsCustom);
        Assert.Null(adminFoods.Added!.CoachId);
        Assert.Equal(100m, platform.ServingSize);

        var coachId = Guid.NewGuid();
        var (coach, coachFoods) = Create(FakeCurrentUser.ForCoach(coachId));
        var custom = await coach.CreateAsync(Chicken(), CancellationToken.None);
        Assert.True(custom.IsCustom);
        Assert.Equal(coachId, coachFoods.Added!.CoachId);
        await Assert.ThrowsAsync<ForbiddenException>(() => coach.CreateAsync(Chicken(coachId: Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Coaches_cannot_change_or_delete_platform_foods()
    {
        var platform = FoodItem.Create(null, new FoodValues("Chicken", null, Guid.NewGuid(), FoodBaseUnit.Gram, 100, 165, 31, 0, 3.6m));
        var (coach, _) = Create(FakeCurrentUser.ForCoach(Guid.NewGuid()), existing: platform);

        await Assert.ThrowsAsync<ForbiddenException>(() => coach.UpdateAsync(platform.Id, Chicken(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => coach.DeleteAsync(platform.Id, CancellationToken.None));

        var (admin, _) = Create(FakeCurrentUser.Administrator(), existing: platform);
        Assert.Equal(40m, (await admin.UpdateAsync(platform.Id, Chicken(protein: 40), CancellationToken.None)).ProteinPer100);
    }

    [Fact]
    public async Task Piece_foods_default_to_one_piece_per_serving()
    {
        var (service, _) = Create(FakeCurrentUser.Administrator());
        var toast = await service.CreateAsync(Chicken() with { BaseUnit = FoodBaseUnit.Piece, PieceNameEn = "slice" }, CancellationToken.None);
        Assert.Equal((1m, "slice"), (toast.ServingSize, toast.BaseUnitLabel));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(Chicken() with { BaseUnit = FoodBaseUnit.Piece }, CancellationToken.None));
    }

    private sealed class FakeFoodRepository(FoodItem? existing) : IFoodRepository
    {
        public FoodItem? Added { get; private set; }
        public Task<PagedResult<FoodItem>> ListAsync(FoodQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<FoodItem>([], query.PageNumber, query.PageSize, 0));
        public Task<FoodItem?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(existing?.Id == id ? existing : null);
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
        public Task<bool> KeyExistsAsync(string key, Guid? excludingId, Guid? coachId, CancellationToken cancellationToken) => Task.FromResult(false);
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
