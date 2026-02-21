using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProformaFarm.API.Infrastructure.Correlation;
using ProformaFarm.API.Infrastructure.Validation;
using ProformaFarm.Application.Common;
using System.Threading.Tasks;

namespace ProformaFarm.API.Filters;

public sealed class ModelStateValidationFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ModelState.IsValid)
            return next();

        var correlationId = CorrelationIdResolver.Resolve(context.HttpContext);
        var errors = ModelStateMapper.ToFieldErrors(context.ModelState);

        var payload = ApiResponse<object>.Fail(
            code: "VALIDATION_ERROR",
            message: "Validation failed.",
            data: errors,
            correlationId: correlationId
        );

        context.Result = new BadRequestObjectResult(payload);
        return Task.CompletedTask;
    }
}
