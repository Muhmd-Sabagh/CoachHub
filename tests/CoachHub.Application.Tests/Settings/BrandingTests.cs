using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Media;
using CoachHub.Application.Settings;
using CoachHub.Domain.Media;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Tests.Settings;

public sealed class TenantResolverTests
{
    [Theory]
    [InlineData("coacha.coachhub.example", "coachhub.example", "coacha", null)]
    [InlineData("CoachA.CoachHub.Example:443", "coachhub.example", "coacha", null)]
    [InlineData("coachhub.example", "coachhub.example", null, null)]
    [InlineData("www.coachhub.example", "coachhub.example", null, null)]
    [InlineData("a.b.coachhub.example", "coachhub.example", null, null)]
    [InlineData("www.coacha.com", "coachhub.example", null, "www.coacha.com")]
    [InlineData("www.coacha.com.", null, null, "www.coacha.com")]
    [InlineData("localhost", "coachhub.example", null, null)]
    [InlineData("127.0.0.1", null, null, null)]
    [InlineData("", null, null, null)]
    public void Hosts_are_classified_as_subdomain_custom_domain_or_platform(
        string host, string? platformDomain, string? subdomain, string? customDomain)
    {
        Assert.Equal((subdomain, customDomain), TenantResolver.Classify(host, platformDomain));
    }

    [Fact]
    public async Task A_signed_in_coach_or_client_always_resolves_to_their_coach()
    {
        var coachId = Guid.NewGuid();
        var brands = new FakeBrands();
        var resolver = new TenantResolver(new FakeCurrentUser(TenantScope.Client, coachId, Guid.NewGuid()), brands,
            new BrandingOptions { PlatformDomain = "coachhub.example" });

        Assert.Equal(coachId, await resolver.ResolveCoachAsync("other.coachhub.example", CancellationToken.None));
        Assert.Equal(0, brands.HostLookups);
    }

    [Fact]
    public async Task Before_sign_in_the_host_decides()
    {
        var coachId = Guid.NewGuid();
        var brands = new FakeBrands { HostOwner = coachId };
        var resolver = new TenantResolver(new FakeCurrentUser(TenantScope.None), brands,
            new BrandingOptions { PlatformDomain = "coachhub.example" });

        Assert.Equal(coachId, await resolver.ResolveCoachAsync("coacha.coachhub.example", CancellationToken.None));
        Assert.Null(await resolver.ResolveCoachAsync("coachhub.example", CancellationToken.None));
        Assert.Equal(1, brands.HostLookups);
    }
}

public sealed class EffectiveBrandTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Without_any_brand_the_built_in_defaults_apply()
    {
        var brand = await Service(new FakeBrands()).EffectiveAsync(null, CancellationToken.None);

        Assert.Equal("CoachHub", brand.BrandName);
        Assert.Equal("#465fff", brand.PrimaryColor);
        Assert.Null(brand.CoachId);
    }

    [Fact]
    public async Task A_coach_brand_overrides_the_platform_and_falls_back_field_by_field()
    {
        var coachId = Guid.NewGuid();
        var platform = Brand.Create(null, "Platform", Now);
        platform.Update("Platform", "#111111", "#222222", "Platform login", null, "Platform welcome", null, "Platform footer", Now);
        var platformLogo = Guid.NewGuid();
        platform.SetImage(BrandImage.Logo, platformLogo, Now);
        var coach = Brand.Create(coachId, "Coach A", Now);
        coach.Update("Coach A", "#333333", null, null, null, "Coach welcome", null, null, Now);

        var brand = await Service(new FakeBrands { Platform = platform, Coach = coach }).EffectiveAsync(coachId, CancellationToken.None);

        Assert.Equal(coachId, brand.CoachId);
        Assert.Equal("Coach A", brand.BrandName);
        Assert.Equal("#333333", brand.PrimaryColor);
        Assert.Equal("#222222", brand.SecondaryColor);
        Assert.Equal("Platform login", brand.LoginHeadline);
        Assert.Equal("Coach welcome", brand.DashboardWelcome);
        Assert.Equal("Platform footer", brand.EmailFooter);
        Assert.Equal(platformLogo, brand.LogoMediaId);
    }

    [Fact]
    public async Task A_coach_without_a_brand_sees_the_platform_brand()
    {
        var platform = Brand.Create(null, "Platform", Now);

        var brand = await Service(new FakeBrands { Platform = platform }).EffectiveAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("Platform", brand.BrandName);
        Assert.Null(brand.CoachId);
    }

    private static BrandingService Service(FakeBrands brands) => new(
        brands,
        new TenantResolver(new FakeCurrentUser(TenantScope.None), brands, new BrandingOptions()),
        new FakeCoachRepository(),
        TestTenancy.CoachOwnership(),
        new FakeCurrentUser(TenantScope.None),
        new MediaService(new NullStorage(), new NullMedia()),
        new NullStorage(),
        TimeProvider.System);
}

public sealed class DomainValidationTests
{
    [Fact]
    public void Argument_errors_become_field_keyed_validation_errors()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            DomainValidation.Run(() => throw new ArgumentException("Use a color such as #465fff.", "PrimaryColor"), "brand"));

        Assert.Equal(["Use a color such as #465fff."], exception.Errors["primaryColor"]);
    }

    [Fact]
    public void Errors_without_a_parameter_use_the_fallback_field()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            DomainValidation.Run<int>(() => throw new ArgumentException("Broken."), "settings"));

        Assert.Contains("settings", exception.Errors);
    }
}

internal sealed class FakeBrands : IBrandRepository
{
    public Brand? Platform { get; set; }
    public Brand? Coach { get; set; }
    public Guid? HostOwner { get; set; }
    public int HostLookups { get; private set; }

    public Task<Brand?> FindPlatformAsync(CancellationToken cancellationToken) => Task.FromResult(Platform);
    public Task<Brand?> FindForCoachAsync(Guid coachId, CancellationToken cancellationToken) =>
        Task.FromResult(Coach?.CoachId == coachId ? Coach : null);
    public Task<Guid?> FindCoachByHostAsync(string? subdomain, string? customDomain, CancellationToken cancellationToken)
    {
        HostLookups++;
        return Task.FromResult(HostOwner);
    }
    public Task<bool> HostTakenAsync(string? subdomain, string? customDomain, Guid exceptBrandId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
    public Task<BrandImageFile?> FindBrandImageAsync(Guid mediaId, CancellationToken cancellationToken) =>
        Task.FromResult<BrandImageFile?>(null);
    public Task AddAsync(Brand brand, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class NullStorage : IMediaStorage
{
    public Task<StoredMedia> StoreAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken) =>
        Task.FromResult(new StoredMedia("stored-" + Guid.NewGuid().ToString("N")));
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class NullMedia : IMediaRepository
{
    public Task AddAsync(MediaAsset media, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<MediaAsset?>(null);
    public Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken) => Task.CompletedTask;
}
