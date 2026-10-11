using CoachHub.Infrastructure;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Persistence;

public sealed class CoachHubDbContextTests
{
    [Fact]
    public void Model_contains_identity_and_media_but_no_legacy_business_entities()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().Select(type => type.ClrType).ToArray();

        Assert.Contains(typeof(User), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Coaches.Coach), entityTypes);
        Assert.Contains(typeof(Role), entityTypes);
        Assert.Contains(typeof(UserRole), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Auditing.AuditEntry), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Media.MediaAsset), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.ReferenceData.Package), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.ReferenceData.Currency), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.ReferenceData.PaymentAccount), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.ReferenceData.FoodCategory), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.ReferenceData.ExerciseCategory), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Nutrition.FoodItem), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Nutrition.LegacyFoodImportRecord), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Training.Exercise), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Training.LegacyExerciseImportRecord), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Clients.Client), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Clients.Subscription), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Clients.SubscriptionRenewal), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Billing.Invoice), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Billing.Payment), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Billing.Refund), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Communications.Notification), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.PlanDelivery.DeliveredPlan), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Assessments.FormDefinition), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Assessments.FormVersion), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Assessments.FormQuestion), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Assessments.FormSubmission), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.Assessments.FormAnswer), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.DietPlanning.DietPlan), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.DietPlanning.DietPlanVersion), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.DietPlanning.Meal), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.DietPlanning.MealFoodItem), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.DietPlanning.DietPlanNote), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.DietPlanning.DietReplacementGroup), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.DietPlanning.DietReplacementOption), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.WorkoutPlanning.WorkoutPlan), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.WorkoutPlanning.WorkoutPlanNote), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.WorkoutPlanning.WorkoutDay), entityTypes);
        Assert.Contains(typeof(CoachHub.Domain.WorkoutPlanning.WorkoutExercise), entityTypes);
        Assert.DoesNotContain(entityTypes, type => type.Name is
            "ClientAssessment" or "ClientUpdate" or "GymDbContext");
    }

    [Fact]
    public void Infrastructure_registers_the_sql_server_context()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:" + DatabaseOptions.ConnectionStringName] =
                DatabaseOptions.DevelopmentConnectionString,
            ["Media:Provider"] = "FileSystem",
            ["Media:StorageRoot"] = Path.Combine(
                Path.GetTempPath(),
                "coachhub-media-registration-tests")
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration, allowLocalMediaStorage: true);

        using var provider = services.BuildServiceProvider();
        using var context = provider.GetRequiredService<CoachHubDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
    }

    [Fact]
    public void Every_coach_owned_entity_has_a_tenant_filter_and_a_coach_foreign_key()
    {
        using var context = CreateContext();
        var owned = context.Model.GetEntityTypes()
            .Where(type => typeof(CoachHub.Domain.Common.ITenantOwned).IsAssignableFrom(type.ClrType) ||
                           typeof(CoachHub.Domain.Common.IOptionalTenantOwned).IsAssignableFrom(type.ClrType))
            .ToArray();

        Assert.Equal(15, owned.Length);
        Assert.All(owned, type =>
        {
            Assert.NotEmpty(type.GetDeclaredQueryFilters());
            Assert.Contains(type.GetForeignKeys(), key =>
                key.PrincipalEntityType.ClrType == typeof(CoachHub.Domain.Coaches.Coach) &&
                key.Properties.Single().Name == "CoachId" &&
                key.DeleteBehavior == Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        });
    }

    private static CoachHubDbContext CreateContext()
    {
        var factory = new CoachHubDbContextFactory();
        return factory.CreateDbContext([]);
    }
}