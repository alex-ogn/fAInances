using FAInances.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FAInances.Web.Infrastructure;

/// <summary>Turns service-layer exceptions into HTTP problem responses for API controllers.</summary>
public class ServiceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            BusinessRuleException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            _ => (int?)null
        };
        if (status is null)
            return;

        context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = context.Exception.Message })
        {
            StatusCode = status
        };
        context.ExceptionHandled = true;
    }
}
