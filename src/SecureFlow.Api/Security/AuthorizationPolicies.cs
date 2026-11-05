using Microsoft.AspNetCore.Authorization;

namespace SecureFlow.Api.Security;

public static class AuthorizationPolicies
{
    public const string Read = "workflow.read";
    public const string Write = "workflow.write";
    public const string Approve = "workflow.approve";
    public const string Audit = "workflow.audit";

    public static IServiceCollection AddSecureFlowAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Read, policy => ConfigureScopePolicy(policy, Read))
            .AddPolicy(Write, policy => ConfigureScopePolicy(policy, Write))
            .AddPolicy(Approve, policy => ConfigureScopePolicy(policy, Approve))
            .AddPolicy(Audit, policy => ConfigureScopePolicy(policy, Audit));

        return services;
    }

    private static void ConfigureScopePolicy(AuthorizationPolicyBuilder policy, string scope)
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("sub");
        policy.RequireAssertion(context => HasScope(context.User, scope));
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string requiredScope)
    {
        return user.Claims
            .Where(claim => claim.Type is "scope" or "scp")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);
    }
}
