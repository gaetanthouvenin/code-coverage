// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovApiClient.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Codecov;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Codecov.Models;

namespace CodeCoverage.Infrastructure.Codecov;

/// <summary>Reads the commits of a repository from the API v2 of Codecov.</summary>
/// <param name="client">The typed client, whose base address is the API of Codecov.</param>
/// <remarks>
///   <para>
///     The shape of the answers comes from the public documentation of Codecov; it was never compared with a real
///     answer (M5.06: the fixtures of the tests are written from the documentation).
///   </para>
///   <para>
///     The token goes in the <c>Authorization</c> header of each request only: the factory of the clients redacts the
///     values of the headers it logs, and no message of this class holds it.
///   </para>
/// </remarks>
internal sealed class CodecovApiClient(HttpClient client) : ICodecovApiClient
{
  #region Fields

  /// <summary>The commits of one page, the most Codecov serves.</summary>
  internal const int PageSize = 100;

  /// <summary>The answers that mean a refusal for good: the token, its rights, or the repository on Codecov.</summary>
  private static readonly HttpStatusCode[] Refusals = [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound];

  #endregion

  #region Methods

  /// <summary>Gives the name of a forge in the addresses of Codecov.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The service of Codecov.</returns>
  /// <exception cref="ArgumentException">The forge has no counterpart on Codecov.</exception>
  private static string Service(Forge forge) =>
    forge switch
    {
      Forge.GitHub => "github",
      Forge.GitLab => "gitlab",
      Forge.Bitbucket => "bitbucket",
      var _ => throw new ArgumentException($"Codecov has no service for the forge {forge}.", nameof(forge)),
    };

  /// <summary>Gives the owner of a repository as Codecov names it: a GitLab subgroup joins its levels with colons.</summary>
  /// <param name="owner">The owner of the repository on its forge.</param>
  /// <param name="namespace">The levels between the owner and the repository, <c>sub/sub2</c>, or empty.</param>
  /// <returns><c>owner</c>, or <c>owner:sub:sub2</c> for a repository of a subgroup.</returns>
  private static string CodecovOwner(string owner, string @namespace) =>
    @namespace.Length == 0 ? owner : string.Join(':', [owner, .. @namespace.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

  /// <summary>Turns a commit of Codecov into an imported commit, when it can be one.</summary>
  /// <param name="model">The commit, as Codecov reports it.</param>
  /// <returns>The commit, or <see langword="null" /> when it has no complete report, no valid SHA or no usable branch.</returns>
  private static CodecovCommit? ToCommit(CodecovCommitModel model)
  {
    if (model is not { State: "complete", Totals: { } totals, Branch: { Length: > 0 and <= Commit.BranchMaxLength } branch }
        || !GitSha.TryParse(model.Commitid, out var sha)
        || totals.Hits < 0
        || totals.Misses < 0
        || totals.Partials < 0
        || totals.Files < 0)
    {
      return null;
    }

    var counters = new CoverageTotals(totals.Hits, totals.Misses, totals.Partials, Math.Max(0, totals.Branches), Math.Max(0, totals.Methods));

    return new CodecovCommit(sha, branch, model.Timestamp, totals.Files, counters);
  }

  #endregion

  #region Implementation of ICodecovApiClient

  /// <inheritdoc />
  public async Task<CodecovCommitPage?> ListCommitsAsync(
    Forge forge,
    string owner,
    string @namespace,
    string name,
    string token,
    int page,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(@namespace);
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(token);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);

    var address = string.Create(
      CultureInfo.InvariantCulture,
      $"api/v2/{Service(forge)}/{Uri.EscapeDataString(CodecovOwner(owner, @namespace))}/repos/{Uri.EscapeDataString(name)}/commits/?page={page}&page_size={PageSize}"
    );

    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, UriKind.Relative));

    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    if (Array.IndexOf(Refusals, response.StatusCode) >= 0)
    {
      return null;
    }

    // A failure of Codecov: the message names the status only, and the job runs again.
    response.EnsureSuccessStatusCode();

    var body = await response.Content.ReadFromJsonAsync(CodecovJsonContext.Default.CodecovCommitListResponse, cancellationToken).ConfigureAwait(false);

    if (body is null)
    {
      return new CodecovCommitPage([], HasNext: false);
    }

    return new CodecovCommitPage([.. body.Results.Select(ToCommit).OfType<CodecovCommit>()], body.Next is not null);
  }

  #endregion
}
