// ------------------------------------------------------------------------------------------------
// <copyright file="OidcTokenFetcher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text.Json;

using CodeCoverage.Uploader.Output;
using CodeCoverage.Uploader.Serialization;

namespace CodeCoverage.Uploader.Http;

/// <summary>Gets a GitHub Actions OIDC token when no upload secret is given (§5.3-6).</summary>
/// <param name="client">The HTTP client.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="reporter">Prints the messages.</param>
internal sealed class OidcTokenFetcher(HttpClient client, Func<string, string?> environment, ConsoleReporter reporter)
{
  #region Fields

  private const string RequestUrlVariable = "ACTIONS_ID_TOKEN_REQUEST_URL";

  private const string RequestTokenVariable = "ACTIONS_ID_TOKEN_REQUEST_TOKEN";

  #endregion

  #region Methods

  /// <summary>Asks GitHub Actions for a token.</summary>
  /// <param name="audience">The audience, the address of the server.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The JWT, or <see langword="null" /> outside GitHub Actions or when it cannot be obtained.</returns>
  public async Task<string?> FetchAsync(string audience, CancellationToken cancellationToken)
  {
    var url = environment(RequestUrlVariable);
    var token = environment(RequestTokenVariable);

    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
    {
      return null;
    }

    var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';

    using var request = new HttpRequestMessage(HttpMethod.Get, $"{url}{separator}audience={Uri.EscapeDataString(audience)}");

    request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);

    try
    {
      using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

      if (!response.IsSuccessStatusCode)
      {
        reporter.Warning($"GitHub Actions refused an OIDC token: {(int)response.StatusCode}.");

        return null;
      }

      var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      var json = JsonSerializer.Deserialize(body, UploaderJsonContext.Default.JsonElement);

      return json.TryGetProperty("value", out var value) ? value.GetString() : null;
    }
    catch (Exception exception) when (exception is HttpRequestException or JsonException)
    {
      reporter.Warning($"The OIDC token could not be obtained: {exception.Message}");

      return null;
    }
  }

  #endregion
}
