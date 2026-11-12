// ------------------------------------------------------------------------------------------------
// <copyright file="BadgesController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using CodeCoverage.Api.Contracts.Badges;
using CodeCoverage.Api.Mapping;
using CodeCoverage.Api.Rendering;
using CodeCoverage.Application.Configuration;

namespace CodeCoverage.Api.Controllers;

/// <summary>The coverage badges of the repositories, at the routes of the API and at the ones of Codecov.</summary>
/// <param name="sender">The request sender.</param>
/// <param name="options">The settings of the badges, read at each request.</param>
/// <remarks>
///   A badge never answers 404 nor 403: an unknown forge, repository, branch or flag, or a wrong token, gives the grey
///   « unknown » badge with 200, which reveals nothing. Only an address that is not a badge (another extension) is a
///   normal 404.
/// </remarks>
[ApiController]
[AllowAnonymous]
[Tags(CodeCoverageApi.Tag)]
public sealed partial class BadgesController(ISender sender, IOptionsMonitor<BadgeOptions> options) : ControllerBase
{
  #region Fields

  private const string NoCache = "no-cache, max-age=0, must-revalidate";

  private const string SvgType = "image/svg+xml";

  private const string TextType = "text/plain";

  private const string Svg = "svg";

  #endregion

  #region Methods

  /// <summary>Read the badge of a repository</summary>
  /// <remarks>
  ///   The coverage of the covered head of a branch (the default one unless <c>branch</c> names another) as an SVG badge
  ///   or as text, for the whole project or one <c>flag</c>. A private repository needs its badge <c>token</c>.
  ///   <c>precision</c> is 0 (the default), 1 or 2 decimals, rounded to the nearest as Codecov; another value gives the
  ///   default. Never 404 nor 403: anything unknown gives the grey « unknown » badge. Answers 304 to a matching
  ///   <c>If-None-Match</c>.
  /// </remarks>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="ext">The format: svg or txt.</param>
  /// <param name="branch">The branch, or the default branch.</param>
  /// <param name="flag">A flag, case-sensitive, or the whole project.</param>
  /// <param name="token">The badge token of a private repository.</param>
  /// <param name="precision">The number of decimals: 0, 1 or 2.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The badge.</returns>
  [HttpGet(CodeCoverageApi.Route + "/badges/{forge}/{owner}/{repo}.{ext:regex(^(svg|txt)$)}", Name = "GetBadge")]
  [ProducesResponseType<string>(StatusCodes.Status200OK, SvgType, TextType)]
  [ProducesResponseType(StatusCodes.Status304NotModified)]
  public Task<IActionResult> GetAsync(
    string forge,
    string owner,
    string repo,
    string ext,
    [FromQuery] string? branch,
    [FromQuery] string? flag,
    [FromQuery] string? token,
    [FromQuery] string? precision,
    CancellationToken cancellationToken) =>
    BadgeAsync(forge, owner, repo, ext, branch, flag, token, precision, cancellationToken);

  /// <summary>Read the badge of a repository at the address of Codecov.</summary>
  /// <param name="service">gh or github.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="ext">The format: svg or txt.</param>
  /// <param name="branch">The branch, or the default branch.</param>
  /// <param name="flag">A flag, or the whole project.</param>
  /// <param name="token">The badge token of a private repository.</param>
  /// <param name="precision">The number of decimals: 0, 1 or 2.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The badge.</returns>
  [HttpGet("/{service:regex(^(gh|github)$)}/{owner}/{repo}/{graph:regex(^graphs?$)}/badge.{ext:regex(^(svg|txt)$)}")]
  [ApiExplorerSettings(IgnoreApi = true)]
  public Task<IActionResult> GetCodecovAsync(
    string service,
    string owner,
    string repo,
    string ext,
    [FromQuery] string? branch,
    [FromQuery] string? flag,
    [FromQuery] string? token,
    [FromQuery] string? precision,
    CancellationToken cancellationToken) =>
    BadgeAsync(service, owner, repo, ext, branch, flag, token, precision, cancellationToken);

  /// <summary>Read the badge of a branch at the address of Codecov.</summary>
  /// <param name="service">gh or github.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="branchAndSuffix">The branch, then <c>/graph/badge.svg</c> or one of its variants.</param>
  /// <param name="flag">A flag, or the whole project.</param>
  /// <param name="token">The badge token of a private repository.</param>
  /// <param name="precision">The number of decimals: 0, 1 or 2.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The badge, or 404 when the address is not a badge.</returns>
  [HttpGet("/{service:regex(^(gh|github)$)}/{owner}/{repo}/branch/{**branchAndSuffix}")]
  [ApiExplorerSettings(IgnoreApi = true)]
  public async Task<IActionResult> GetCodecovBranchAsync(
    string service,
    string owner,
    string repo,
    string branchAndSuffix,
    [FromQuery] string? flag,
    [FromQuery] string? token,
    [FromQuery] string? precision,
    CancellationToken cancellationToken)
  {
    var match = BranchSuffix().Match(branchAndSuffix ?? string.Empty);

    if (!match.Success)
    {
      return NotFound();
    }

    // Kestrel already decoded the path, all but an encoded slash: only %2F is left to decode (fix%2541 is fix%41).
    var branch = EncodedSlash().Replace(match.Groups["branch"].Value, "/");

    return await BadgeAsync(service, owner, repo, match.Groups["ext"].Value, branch, flag, token, precision, cancellationToken).ConfigureAwait(false);
  }

  [GeneratedRegex("^(?<branch>.+)/graphs?/badge\\.(?<ext>svg|txt)$", RegexOptions.CultureInvariant)]
  private static partial Regex BranchSuffix();

  [GeneratedRegex("%2F", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
  private static partial Regex EncodedSlash();

  private async Task<IActionResult> BadgeAsync(
    string forge,
    string owner,
    string repo,
    string ext,
    string? branch,
    string? flag,
    string? token,
    string? precision,
    CancellationToken cancellationToken)
  {
    var badge = await sender.GetBadgeResponseAsync(forge, owner, repo, branch, flag, token, precision, options.CurrentValue.Label, cancellationToken)
                            .ConfigureAwait(false);
    var entityTag = BadgeEntityTag.Of(badge, ext);

    Response.Headers[HeaderNames.CacheControl] = NoCache;
    Response.Headers[HeaderNames.ETag] = entityTag;

    if (BadgeEntityTag.Matches(Request.Headers.IfNoneMatch, entityTag))
    {
      return StatusCode(StatusCodes.Status304NotModified);
    }

    return string.Equals(ext, Svg, StringComparison.Ordinal)
             ? Content(BadgeSvgRenderer.Render(badge), SvgType)
             : Content(badge.Text, TextType);
  }

  #endregion
}
