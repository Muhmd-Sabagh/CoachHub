using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.ReferenceData;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
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
    public async Task<ActionResult<TResponse>> Create(
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
    public Task<TResponse> Update(
        Guid id,
        [FromBody] TInput input,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(id, input, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

[Route("api/reference-data/packages")]
public sealed class PackagesController(PackageService service)
    : ReferenceDataController<Package, PackageInput, PackageResponse>(service);

[Route("api/reference-data/currencies")]
public sealed class CurrenciesController(CurrencyService service)
    : ReferenceDataController<Currency, CurrencyInput, CurrencyResponse>(service);

[Route("api/reference-data/payment-accounts")]
public sealed class PaymentAccountsController(PaymentAccountService service)
    : ReferenceDataController<PaymentAccount, PaymentAccountInput, PaymentAccountResponse>(service);

[Route("api/reference-data/food-categories")]
public sealed class FoodCategoriesController(FoodCategoryService service)
    : ReferenceDataController<FoodCategory, BilingualReferenceInput, BilingualReferenceResponse>(service);

[Route("api/reference-data/exercise-categories")]
public sealed class ExerciseCategoriesController(ExerciseCategoryService service)
    : ReferenceDataController<ExerciseCategory, BilingualReferenceInput, BilingualReferenceResponse>(service);