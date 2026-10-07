using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Api;

public sealed class UserFacingErrorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (UserFacingException ex) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(ErrorBody(ex));
        }
    }

    private static object ErrorBody(UserFacingException ex) => ex switch
    {
        DuplicateDeviationException duplicate => new { error = ex.Message, existingSetId = duplicate.ExistingSetId },
        _ => new { error = ex.Message },
    };
}
