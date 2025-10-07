using FluentResults;
using Microsoft.AspNetCore.Mvc;
using MicroservicesBase.Core.Errors;

namespace MicroservicesBase.API.ProblemDetails
{
    /// <summary>
    /// Extension methods for mapping FluentResults to ProblemDetails responses.
    /// Provides a clean way to convert Result.Fail() into appropriate HTTP responses.
    /// </summary>
    public static class ResultExtensions
    {
        /// <summary>
        /// Converts a FluentResults Result to ProblemDetails.
        /// Maps error codes to appropriate HTTP status codes.
        /// </summary>
        public static Microsoft.AspNetCore.Mvc.ProblemDetails ToProblemDetails(
            this ResultBase result,
            HttpContext httpContext)
        {
            if (result.IsSuccess)
            {
                throw new InvalidOperationException("Cannot convert successful result to ProblemDetails.");
            }

            // Get the first error (or use generic error if none)
            var firstError = result.Errors.FirstOrDefault();
            var errorCode = firstError?.Message ?? ErrorCatalog.INTERNAL_ERROR;

            // Create ProblemDetails with error code
            return ProblemDetailsFactory.Create(
                httpContext,
                errorCode,
                detail: GetErrorDetail(result),
                instance: httpContext.Request.Path);
        }

        /// <summary>
        /// Matches a Result and executes the appropriate action.
        /// Success → executes onSuccess with value
        /// Failure → executes onFailure with ProblemDetails
        /// </summary>
        public static async Task<IResult> MatchToResult<T>(
            this Result<T> result,
            HttpContext httpContext,
            Func<T, Task<IResult>> onSuccess,
            Func<Microsoft.AspNetCore.Mvc.ProblemDetails, Task<IResult>>? onFailure = null)
        {
            if (result.IsSuccess)
            {
                return await onSuccess(result.Value);
            }

            var problemDetails = result.ToProblemDetails(httpContext);

            if (onFailure != null)
            {
                return await onFailure(problemDetails);
            }

            // Default: return appropriate status code with ProblemDetails
            return Results.Json(
                problemDetails,
                statusCode: problemDetails.Status ?? 500,
                contentType: "application/problem+json");
        }

        /// <summary>
        /// Creates a ProblemDetails IResult from a failed Result.
        /// Useful for FastEndpoints responses.
        /// </summary>
        public static IResult ToProblemResult(this ResultBase result, HttpContext httpContext)
        {
            var problemDetails = result.ToProblemDetails(httpContext);
            return Results.Json(
                problemDetails,
                statusCode: problemDetails.Status ?? 500,
                contentType: "application/problem+json");
        }

        /// <summary>
        /// Gets detailed error message from Result errors.
        /// </summary>
        private static string GetErrorDetail(ResultBase result)
        {
            if (!result.Errors.Any())
            {
                return "An error occurred.";
            }

            // If single error, return its message
            if (result.Errors.Count == 1)
            {
                var error = result.Errors.First();
                return string.IsNullOrWhiteSpace(error.Message)
                    ? "An error occurred."
                    : error.Message;
            }

            // If multiple errors, combine them
            var errorMessages = result.Errors
                .Where(e => !string.IsNullOrWhiteSpace(e.Message))
                .Select(e => e.Message)
                .ToList();

            return errorMessages.Any()
                ? string.Join("; ", errorMessages)
                : "Multiple errors occurred.";
        }
    }
}

