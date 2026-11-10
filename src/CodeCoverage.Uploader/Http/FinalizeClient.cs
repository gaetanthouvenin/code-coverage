// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizeClient.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using CodeCoverage.Uploader.Output;
using CodeCoverage.Uploader.Serialization;

namespace CodeCoverage.Uploader.Http;

/// <summary>Announces the end of the uploads of a commit (<c>--finalize</c>): the commit is then notified without waiting for more.</summary>
/// <param name="client">The HTTP client.</param>
/// <param name="retry">The retry policy.</param>
/// <param name="reporter">The reporter.</param>
internal sealed class FinalizeClient(HttpClient client, RetryPolicy retry, ConsoleReporter reporter)
{
  #region Fields

  // The uploads of this tool are those of a GitHub repository.
  private const string ForgeSegment = "github";

  #endregion

  #region Methods

  /// <summary>Builds the address of the announcement.</summary>
  /// <param name="server">The address of the server.</param>
  /// <param name="slug">The slug of the repository, <c>owner/name</c>.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <returns>The address, or <see langword="null" /> when the slug has no owner or no name.</returns>
  public static Uri? BuildAddress(Uri server, string slug, string sha)
  {
    ArgumentNullException.ThrowIfNull(server);
    ArgumentNullException.ThrowIfNull(slug);
    ArgumentNullException.ThrowIfNull(sha);

    var separator = slug.IndexOf('/', StringComparison.Ordinal);

    if (separator <= 0 || separator == slug.Length - 1 || sha.Length == 0)
    {
      return null;
    }

    var owner = Uri.EscapeDataString(slug[..separator]);
    var name = Uri.EscapeDataString(slug[(separator + 1)..]);

    return new Uri(
      new Uri(server.ToString().TrimEnd('/') + "/"),
      $"api/v1/repos/{ForgeSegment}/{owner}/{name}/commits/{Uri.EscapeDataString(sha)}/uploads-complete"
    );
  }

  /// <summary>Announces the end of the uploads of a commit.</summary>
  /// <param name="server">The address of the server.</param>
  /// <param name="slug">The slug of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="credential">The credential that sent the upload.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns><see langword="true" /> when the server took the announcement.</returns>
  /// <remarks>A failure is reported and never thrown: the coverage tooling does not break a build.</remarks>
  public async Task<bool> CompleteAsync(Uri server, string slug, string sha, string credential, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(credential);

    if (BuildAddress(server, slug, sha) is not { } address)
    {
      reporter.Error($"The slug '{slug}' is not owner/name: the uploads cannot be declared complete.");

      return false;
    }

    reporter.Detail($"POST {address}");

    try
    {
      using var response = await retry.SendAsync(
                             async token =>
                             {
                               using var message = new HttpRequestMessage(HttpMethod.Post, address);

                               message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);

                               return await client.SendAsync(message, token).ConfigureAwait(false);
                             },
                             cancellationToken
                           ).ConfigureAwait(false);

      var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

      if (response.StatusCode is HttpStatusCode.OK)
      {
        reporter.Info(Describe(body));

        return true;
      }

      var code = Read(body, "errorCode", static value => value.GetString());

      reporter.Error($"The server refused to declare the uploads complete: {(int)response.StatusCode}{(code is null ? string.Empty : $" ({code})")}.");

      return false;
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
    {
      reporter.Error($"The uploads could not be declared complete: {exception.Message}");

      return false;
    }
  }

  /// <summary>Writes what the server answered.</summary>
  /// <param name="body">The body of the answer.</param>
  /// <returns>The sentence shown.</returns>
  private static string Describe(string body)
  {
    var total = Read(body, "total", static value => value.TryGetInt32(out var number) ? number : (int?)null);
    var processing = Read(body, "processing", static value => value.TryGetInt32(out var number) ? number : (int?)null);
    var failed = Read(body, "failed", static value => value.TryGetInt32(out var number) ? number : (int?)null);

    if (total is null)
    {
      return "The uploads of the commit are declared complete.";
    }

    var waiting = processing is > 0 ? $", {processing} still being processed" : string.Empty;
    var failures = failed is > 0 ? $", {failed} failed" : string.Empty;

    return $"The uploads of the commit are declared complete: {total} received{waiting}{failures}.";
  }

  /// <summary>Reads a property of a JSON body.</summary>
  /// <typeparam name="T">The type of the value.</typeparam>
  /// <param name="body">The body.</param>
  /// <param name="name">The name of the property.</param>
  /// <param name="read">What reads the value.</param>
  /// <returns>The value, or the default when the body is not the expected JSON.</returns>
  private static T? Read<T>(string body, string name, Func<JsonElement, T?> read)
  {
    try
    {
      var json = JsonSerializer.Deserialize(body, UploaderJsonContext.Default.JsonElement);

      return json.ValueKind is JsonValueKind.Object && json.TryGetProperty(name, out var value) ? read(value) : default;
    }
    catch (Exception exception) when (exception is JsonException or InvalidOperationException)
    {
      return default;
    }
  }

  #endregion
}