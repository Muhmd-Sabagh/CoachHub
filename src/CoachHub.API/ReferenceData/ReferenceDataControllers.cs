using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.ReferenceData;

[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
public abstract class ReferenceDataController<TEntity, TInput, TResponse>(
    ReferenceDataService<TEntity, TInput, TResponse> service) : ControllerBase
    where TEntity : ActiveReferenceEntity
    where TResponse : IReferenceResponse
{
    [HttpGet]
    public Task<PagedResult<TResponse>> List(
        [FromQuery] ReferenceListQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TResponse> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public virtual async Task<ActionResult<TResponse>> Create(
        [FromBody] TInput input,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public virtual Task<TResponse> Update(
        Guid id,
        [FromBody] TInput input,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(id, input, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

/// <summary>Platform reference data: every coach reads it, only administrators change it (D7).</summary>
public abstract class PlatformReferenceDataController<TEntity, TInput, TResponse>(
    ReferenceDataService<TEntity, TInput, TResponse> service)
    : ReferenceDataController<TEntity, TInput, TResponse>(service)
    where TEntity : ActiveReferenceEntity
    where TResponse : IReferenceResponse
{
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public override Task<ActionResult<TResponse>> Create([FromBody] TInput input, CancellationToken cancellationToken) =>
        base.Create(input, cancellationToken);

    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public override Task<TResponse> Update(Guid id, [FromBody] TInput input, CancellationToken cancellationToken) =>
        base.Update(id, input, cancellationToken);

    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public override Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        base.Delete(id, cancellationToken);
}

/// <summary>Packages belong to the coach who sells them (D7).</summary>
[Route(ApiRoutes.V1 + "/reference-data/packages")]
public sealed class PackagesController(PackageService service)
    : ReferenceDataController<Package, PackageInput, PackageResponse>(service);

[Route(ApiRoutes.V1 + "/reference-data/currencies")]
public sealed class CurrenciesController(CurrencyService service)
    : PlatformReferenceDataController<Currency, CurrencyInput, CurrencyResponse>(service);

/// <summary>Payment accounts belong to the coach who receives the money (D7).</summary>
[Route(ApiRoutes.V1 + "/reference-data/payment-accounts")]
public sealed class PaymentAccountsController(PaymentAccountService service)
    : ReferenceDataController<PaymentAccount, PaymentAccountInput, PaymentAccountResponse>(service);

[Route(ApiRoutes.V1 + "/reference-data/food-categories")]
public sealed class FoodCategoriesController(FoodCategoryService service)
    : PlatformReferenceDataController<FoodCategory, BilingualReferenceInput, BilingualReferenceResponse>(service);

[Route(ApiRoutes.V1 + "/reference-data/equipment")]
public sealed class EquipmentController(EquipmentService service)
    : PlatformReferenceDataController<Equipment, BilingualReferenceInput, BilingualReferenceResponse>(service);

[Route(ApiRoutes.V1 + "/reference-data/exercise-categories")]
public sealed class ExerciseCategoriesController(ExerciseCategoryService service)
    : PlatformReferenceDataController<ExerciseCategory, BilingualReferenceInput, BilingualReferenceResponse>(service);