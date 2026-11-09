// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubOidcTokenValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Identity;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Identity;

/// <summary>Verifies the OIDC tokens of GitHub Actions against the keys published by GitHub.</summary>
/// <param name="configurationManager">The cache of the discovery document and of the signing keys of GitHub.</param>
/// <param name="options">The upload settings, read at each validation so that a new audience applies at once.</param>
/// <param name="configuration">The configuration, which carries the public URL of the Api.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   The keys come from the cache of <paramref name="configurationManager" />, so a validation makes no network call once
///   they are known. Only the keys are taken from it: the issuer and the audience are compared to values this instance
///   holds, and the manager is kept out of the validation parameters, where the library would accept any issuer that the
///   discovery document announces.
/// </remarks>
internal sealed partial class GitHubOidcTokenValidator(
  IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
  IOptionsMonitor<UploadOptions> options,
  IConfiguration configuration,
  ILogger<GitHubOidcTokenValidator> logger) : IOidcTokenValidator
{
  #region Fields

  /// <summary>The name of the HTTP client that downloads the discovery document and the signing keys.</summary>
  public const string HttpClientName = "github-oidc";

  /// <summary>The only issuer accepted, the OIDC provider of GitHub Actions.</summary>
  public const string Issuer = "https://token.actions.githubusercontent.com";

  /// <summary>The address of the discovery document of the issuer.</summary>
  public const string MetadataAddress = Issuer + "/.well-known/openid-configuration";

  /// <summary>How long the download of the discovery document or of the signing keys may take.</summary>
  public static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(10);

  private const string RepositoryClaim = "repository";

  private const string RepositoryIdClaim = "repository_id";

  private const string RepositoryOwnerClaim = "repository_owner";

  private const string RepositoryOwnerIdClaim = "repository_owner_id";

  private const string RefClaim = "ref";

  private const string EventNameClaim = "event_name";

  /// <summary>The tolerance on the expiry and on the start of validity of a token.</summary>
  private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

  /// <summary>The only signature algorithm accepted, the one GitHub announces in its discovery document.</summary>
  private static readonly string[] SignatureAlgorithms = [SecurityAlgorithms.RsaSha256];

  private static readonly JsonWebTokenHandler Handler = new();

  #endregion

  #region Methods

  /// <summary>Creates the cache of the discovery document and of the signing keys of GitHub.</summary>
  /// <param name="httpClient">The client that downloads them, kept for the life of the cache.</param>
  /// <returns>A cache that reads over HTTPS only and refreshes itself in the background.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="httpClient" /> is <see langword="null" />.</exception>
  public static IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(HttpClient httpClient)
  {
    ArgumentNullException.ThrowIfNull(httpClient);

    return new ConfigurationManager<OpenIdConnectConfiguration>(
      MetadataAddress,
      new OpenIdConnectConfigurationRetriever(),
      new HttpDocumentRetriever(httpClient) { RequireHttps = true }
    );
  }

  /// <summary>Builds the rules a token must satisfy.</summary>
  /// <param name="audience">The expected audience.</param>
  /// <param name="signingKeys">The signing keys published by GitHub.</param>
  /// <returns>The validation parameters, each rule stated even where it matches the default of the library.</returns>
  private static TokenValidationParameters CreateParameters(string audience, ICollection<SecurityKey> signingKeys) =>
    new()
    {
      ValidateIssuer = true,
      ValidIssuer = Issuer,
      ValidateAudience = true,
      RequireAudience = true,
      ValidAudience = audience,
      ValidateLifetime = true,
      RequireExpirationTime = true,
      ClockSkew = ClockSkew,
      RequireSignedTokens = true,
      ValidAlgorithms = SignatureAlgorithms,
      ValidateIssuerSigningKey = true,
      IssuerSigningKeys = signingKeys,
      // The key must be the one the token names: no attempt with the others, and an unknown key reveals a rotation.
      TryAllIssuerSigningKeys = false,
    };

  /// <summary>Reads a claim that holds text.</summary>
  /// <param name="token">The verified token.</param>
  /// <param name="claim">The name of the claim.</param>
  /// <returns>The value, or <see langword="null" /> when the claim is absent, blank or does not hold text.</returns>
  private static string? ReadClaim(JsonWebToken token, string claim) =>
    token.TryGetPayloadValue<string>(claim, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

  [LoggerMessage(EventId = 2601, Level = LogLevel.Information, Message = "A GitHub Actions OIDC token was rejected: {Reason}")]
  private static partial void LogRejected(ILogger logger, string reason);

  /// <summary>Tells which audience the tokens must carry.</summary>
  /// <returns><c>Uploads:OidcAudience</c>, or else the public URL of the Api.</returns>
  /// <exception cref="InvalidOperationException">Neither setting is configured.</exception>
  private string ResolveAudience()
  {
    var audience = options.CurrentValue.OidcAudience;

    if (string.IsNullOrWhiteSpace(audience))
    {
      audience = configuration[SettingDefinitions.UrlsApiPublicUrl];
    }

    InvalidOperationException.ThrowIf(
      string.IsNullOrWhiteSpace(audience),
      $"Neither '{SettingDefinitions.UploadsOidcAudience}' nor '{SettingDefinitions.UrlsApiPublicUrl}' is configured."
    );

    return audience;
  }

  /// <summary>Gets the signing keys of GitHub, from the cache once they are known.</summary>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The signing keys.</returns>
  /// <exception cref="BadGatewayException">The keys were never obtained and GitHub does not provide them.</exception>
  private async Task<ICollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
  {
    try
    {
      var metadata = await configurationManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);

      return metadata.SigningKeys;
    }
    catch (InvalidOperationException exception)
    {
      throw new BadGatewayException(GitHubErrors.Unavailable, "The signing keys of GitHub Actions cannot be obtained.", exception);
    }
  }

  /// <summary>Reads the identity a verified token proves.</summary>
  /// <param name="token">The verified token.</param>
  /// <returns>The identity, or <see langword="null" /> when a claim that identifies the repository is missing.</returns>
  private GitHubOidcIdentity? ToIdentity(JsonWebToken token)
  {
    var repository = ReadClaim(token, RepositoryClaim);
    var repositoryId = ReadClaim(token, RepositoryIdClaim);
    var owner = ReadClaim(token, RepositoryOwnerClaim);
    var ownerId = ReadClaim(token, RepositoryOwnerIdClaim);

    if (repository is null || repositoryId is null || owner is null || ownerId is null)
    {
      LogRejected(logger, "a repository claim is missing");

      return null;
    }

    return new GitHubOidcIdentity(
      repository,
      repositoryId,
      owner,
      ownerId,
      ReadClaim(token, RefClaim),
      ReadClaim(token, EventNameClaim)
    );
  }

  #endregion

  #region Implementation of IOidcTokenValidator

  /// <inheritdoc />
  /// <exception cref="BadGatewayException">The signing keys were never obtained and GitHub does not provide them.</exception>
  public async Task<GitHubOidcIdentity?> ValidateAsync(string token, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(token);

    if (!Handler.CanReadToken(token))
    {
      LogRejected(logger, "the token is malformed");

      return null;
    }

    var audience = ResolveAudience();
    var signingKeys = await GetSigningKeysAsync(cancellationToken).ConfigureAwait(false);
    var result = await Handler.ValidateTokenAsync(token, CreateParameters(audience, signingKeys)).ConfigureAwait(false);

    if (result.IsValid)
    {
      return ToIdentity((JsonWebToken)result.SecurityToken);
    }

    // A refresh runs in the background and at most once per refresh interval: this token stays rejected, and a stream of
    // unknown keys cannot turn into a stream of calls to GitHub.
    if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
    {
      configurationManager.RequestRefresh();
    }

    // The type alone: the message of the library may quote parts of the token.
    LogRejected(logger, result.Exception?.GetType().Name ?? "unknown");

    return null;
  }

  #endregion
}
