// ------------------------------------------------------------------------------------------------
// <copyright file="UploadClient.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

using CodeCoverage.Uploader.Output;
using CodeCoverage.Uploader.Serialization;

namespace CodeCoverage.Uploader.Http;

/// <summary>Sends an upload to the server: multipart with gzip reports, <c>Idempotency-Key</c> and retries.</summary>
/// <param name="client">The HTTP client.</param>
/// <param name="retry">The retries.</param>
/// <param name="oidc">The fallback on an OIDC token.</param>
/// <param name="reporter">Prints the messages.</param>
internal sealed class UploadClient(HttpClient client, RetryPolicy retry, OidcTokenFetcher oidc, ConsoleReporter reporter)
{
  #region Fields

  private const string UploadsPath = "api/v1/uploads";

  #endregion

  #region Properties

  /// <summary>Gets the version of the uploader, sent with each upload.</summary>
  public static string? Version => typeof(UploadClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

  #endregion

  #region Methods

  /// <summary>Sends an upload.</summary>
  /// <param name="server">The address of the server.</param>
  /// <param name="token">The upload credential, or <see langword="null" /> to ask GitHub Actions for one.</param>
  /// <param name="request">What the upload sends.</param>
  /// <param name="cancellationToken">A token to cancel the run.</param>
  /// <returns>What the server answered when it accepted the upload; otherwise <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="server" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  public async Task<UploadReceipt?> SendAsync(Uri server, string? token, UploadRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(server);
    ArgumentNullException.ThrowIfNull(request);

    var credential = token ?? await oidc.FetchAsync(server.ToString().TrimEnd('/'), cancellationToken).ConfigureAwait(false);

    if (credential is null)
    {
      reporter.Error("No credential: pass --token, set COVERAGE_TOKEN or CODECOV_TOKEN, or grant id-token: write on GitHub Actions.");

      return null;
    }

    var key = await IdempotencyKeyFactory.CreateAsync(request, cancellationToken).ConfigureAwait(false);
    var uploads = new Uri(new Uri(server.ToString().TrimEnd('/') + "/"), UploadsPath);

    reporter.Detail($"POST {uploads} (Idempotency-Key {key}, {(token is null ? "OIDC token of GitHub Actions" : "upload token")})");

    using var response = await retry.SendAsync(
                           async token2 =>
                           {
                             using var message = new HttpRequestMessage(HttpMethod.Post, uploads) { Content = MultipartBuilder.Build(request) };

                             message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                             message.Headers.Add("Idempotency-Key", key);

                             return await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token2).ConfigureAwait(false);
                           },
                           cancellationToken
                         ).ConfigureAwait(false);

    reporter.Detail($"The server answered {(int)response.StatusCode}.");

    return await ReportAsync(response, server, credential, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Prints the outcome of an upload.</summary>
  /// <param name="response">The answer of the server.</param>
  /// <param name="server">The address of the server.</param>
  /// <param name="credential">The credential the upload was sent with.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>What the server answered when it accepted the upload; otherwise <see langword="null" />.</returns>
  private async Task<UploadReceipt?> ReportAsync(HttpResponseMessage response, Uri server, string credential, CancellationToken cancellationToken)
  {
    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

    if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted)
    {
      var commitUrl = Read(body, "commitUrl");

      reporter.Info(response.StatusCode is HttpStatusCode.OK ? "This upload had already been received." : "The upload was accepted.");

      if (commitUrl is not null)
      {
        reporter.Info($"Commit: {commitUrl}");
      }

      return new UploadReceipt(ReadStatusUrl(server, body), credential);
    }

    var code = Read(body, "errorCode");

    reporter.Error($"The server refused the upload: {(int)response.StatusCode}{(code is null ? string.Empty : $" ({code})")}.");

    return null;
  }

  /// <summary>Reads the address the server gives to follow the processing.</summary>
  /// <param name="server">The address of the server.</param>
  /// <param name="body">The answer of the server.</param>
  /// <returns>The address, or <see langword="null" /> when the server gave none that can be asked safely.</returns>
  /// <remarks>
  ///   The address is taken as the server wrote it, never built here. It receives the credential: it must
  ///   be an absolute HTTP address, and never leave HTTPS when the upload itself was sent over HTTPS.
  /// </remarks>
  private Uri? ReadStatusUrl(Uri server, string body)
  {
    var text = Read(body, "statusUrl");

    if (text is null)
    {
      return null;
    }

    if (!Uri.TryCreate(text, UriKind.Absolute, out var statusUrl) || statusUrl.Scheme is not ("http" or "https"))
    {
      reporter.Warning("The server gave an address to follow the processing that is not an absolute HTTP address: it is not used.");

      return null;
    }

    if (server.Scheme is "https" && statusUrl.Scheme is not "https")
    {
      reporter.Warning("The server gave an address to follow the processing that leaves HTTPS: it is not used.");

      return null;
    }

    return statusUrl;
  }

  /// <summary>Reads a text property of a JSON body.</summary>
  /// <param name="body">The body.</param>
  /// <param name="name">The name of the property.</param>
  /// <returns>The value, or <see langword="null" />.</returns>
  private static string? Read(string body, string name)
  {
    try
    {
      var json = JsonSerializer.Deserialize(body, UploaderJsonContext.Default.JsonElement);

      return json.ValueKind is JsonValueKind.Object && json.TryGetProperty(name, out var value) ? value.GetString() : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  #endregion
}
