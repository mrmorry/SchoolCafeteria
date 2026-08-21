using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SchoolCafeteria.Application.Abstractions;
using SchoolCafeteria.Application.Common;

namespace SchoolCafeteria.Infrastructure.Adapters;

public class EntraIdOptions
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>
/// Validates Entra ID ID tokens against the tenant's own OIDC discovery document and signing
/// keys — the standard way to verify a token minted by Entra ID without trusting the client. No
/// real tenant is configured in this build (TenantId/ClientId are empty placeholders in
/// appsettings); IsConfigured lets AuthService fail with a clear business error instead of an
/// opaque network exception until an administrator fills in a real tenant (see
/// docs/06-runbook.md "Conectar un tenant real de Entra ID").
/// </summary>
public class EntraIdTokenValidator : IEntraIdTokenValidator
{
    private readonly EntraIdOptions _options;
    private readonly Lazy<ConfigurationManager<OpenIdConnectConfiguration>> _configManager;

    public EntraIdTokenValidator(IConfiguration configuration)
    {
        _options = configuration.GetSection("EntraId").Get<EntraIdOptions>() ?? new EntraIdOptions();
        _configManager = new Lazy<ConfigurationManager<OpenIdConnectConfiguration>>(() => new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_options.Instance.TrimEnd('/')}/{_options.TenantId}/v2.0/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true }));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.TenantId) && !string.IsNullOrWhiteSpace(_options.ClientId);

    public async Task<ExternalIdentityClaims> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new BusinessRuleException("auth.entra_not_configured", "El inicio de sesión con Microsoft Entra ID no está configurado en este colegio.");

        var oidcConfig = await _configManager.Value.GetConfigurationAsync(ct);

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = oidcConfig.Issuer,
            ValidAudience = _options.ClientId,
            IssuerSigningKeys = oidcConfig.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(idToken, validationParameters, out _);
        }
        catch (Exception ex)
        {
            throw new BusinessRuleException("auth.entra_invalid_token", $"Token de Microsoft Entra ID inválido: {ex.Message}");
        }

        var objectId = principal.FindFirst("oid")?.Value ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? throw new BusinessRuleException("auth.entra_invalid_token", "El token de Entra ID no contiene el claim 'oid'.");
        var email = principal.FindFirst("preferred_username")?.Value ?? principal.FindFirst("email")?.Value
            ?? throw new BusinessRuleException("auth.entra_invalid_token", "El token de Entra ID no contiene un correo electrónico.");
        var name = principal.FindFirst("name")?.Value ?? email;

        return new ExternalIdentityClaims(objectId, email, name);
    }
}
