using K12.Application.Abstractions;

namespace K12.Api.Endpoints;

public static class ResultExtensions
{
    /// <summary>Maps a use-case result to an HTTP response with RFC 9457 problem details for errors.</summary>
    public static IResult ToHttp<T>(this Result<T> result) => result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Problem(
            title: result.Error!.Code,
            detail: result.Error.Message,
            statusCode: result.Error.Kind switch
            {
                ErrorKind.NotFound => StatusCodes.Status404NotFound,
                ErrorKind.Validation => StatusCodes.Status400BadRequest,
                ErrorKind.Conflict => StatusCodes.Status409Conflict,
                ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError,
            });
}
