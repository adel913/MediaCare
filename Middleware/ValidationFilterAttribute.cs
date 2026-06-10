using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Middleware
{
    public class ValidationFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState.Values.Where(v => v.Errors.Count > 0)
                        .SelectMany(v => v.Errors)
                        .Select(v => v.ErrorMessage)
                        .ToList();

                var response = ApiResponse.Failure("There are errors in the submitted data", 400, errors);

                context.Result = new BadRequestObjectResult(response);
            }
        }
    }
}
