namespace nexo.Extensions;

/// <summary>
/// Wires up cross-cutting middleware in the correct pipeline order, keeping Program.cs minimal.
/// </summary>
public static class WebApplicationExtensions
{
    public static WebApplication UseNexoRequestPipeline(this WebApplication app)
    {
        // Must run first so it can catch exceptions from everything downstream.
        app.UseExceptionHandler();

        app.UseHttpsRedirection();

        app.UseCors(ServiceCollectionExtensions.CorsPolicyName);

        app.UseRateLimiter();

        app.UseAuthorization();

        return app;
    }
}