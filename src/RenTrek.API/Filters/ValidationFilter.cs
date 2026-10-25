using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using AppValidationException = RenTrek.BLL.Exceptions.ValidationException;

namespace RenTrek.API.Filters;

/// <summary>
/// Tự động chạy FluentValidation validator (nếu có) cho mọi tham số của action.
/// Lỗi được ném ra dưới dạng ValidationException để middleware trả 400 chuẩn.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                var errors = result.Errors
                    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                throw new AppValidationException(errors);
            }
        }

        await next();
    }
}
