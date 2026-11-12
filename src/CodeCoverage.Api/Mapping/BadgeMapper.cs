// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Api.Contracts.Badges;
using CodeCoverage.Api.Contracts.Repositories;
using CodeCoverage.Application.Features.Repositories.Commands.ImportBadgeToken;
using CodeCoverage.Application.Features.Repositories.Commands.RotateBadgeToken;
using CodeCoverage.Application.Features.Repositories.Queries.GetBadge;
using CodeCoverage.Domain.Badges;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the badges between the Domain and the API.</summary>
internal static class BadgeMapper
{
  #region Fields

  /// <summary>The message of a badge whose coverage is unknown.</summary>
  public const string UnknownMessage = "unknown";

  /// <summary>The number of decimals shown when none, or a wrong one, is asked.</summary>
  public const int DefaultPrecision = 0;

  /// <summary>The largest number of decimals shown.</summary>
  public const int MaxPrecision = 2;

  #endregion

  #region Methods

  /// <summary>Reads the badge of a repository and converts it for the API; never refuses.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge or service segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="branch">The branch, or <see langword="null" /> for the default branch.</param>
  /// <param name="flag">The flag, or <see langword="null" /> for the whole project.</param>
  /// <param name="token">The badge token, or <see langword="null" />.</param>
  /// <param name="precision">The number of decimals, 0 to 2; anything else gives 0.</param>
  /// <param name="label">The label of the instance, used without a flag.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The badge; an unknown forge gives the grey « unknown » badge.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<BadgeResponse> GetBadgeResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    string? branch,
    string? flag,
    string? token,
    string? precision,
    string label,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var shownLabel = string.IsNullOrEmpty(flag) ? label : flag;
    var badge = ForgeSegments.TryParse(forge, out var parsed)
                  ? await sender.Send(new GetBadgeQuery(parsed, owner, repo, NullIfEmpty(branch), NullIfEmpty(flag), NullIfEmpty(token)), cancellationToken)
                                .ConfigureAwait(false)
                  : Badge.Unknown;

    var shown = Value(badge, ReadPrecision(precision));

    return new BadgeResponse(shownLabel, shown is null ? UnknownMessage : shown + "%", badge.Color, shown ?? UnknownMessage);
  }

  /// <summary>Gives a repository a new badge token.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The new token, given once.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<BadgeTokenResponse> RotateBadgeTokenResponseAsync(this ISender sender, string forge, string owner, string repo, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var value = await sender.Send(new RotateBadgeTokenCommand(AddressSegments.ToRepositoryForge(forge), owner, repo), cancellationToken).ConfigureAwait(false);

    return new BadgeTokenResponse(value);
  }

  /// <summary>Keeps a badge token given by the caller, e.g. the graphing token of Codecov.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="request">The token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the token is kept.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  public static async Task ImportBadgeTokenAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    ImportBadgeTokenRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(request);

    await sender.Send(new ImportBadgeTokenCommand(AddressSegments.ToRepositoryForge(forge), owner, repo, request.Value), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Writes the coverage of a badge, rounded down (decision 13; Codecov rounds to the nearest, a named difference).</summary>
  /// <param name="badge">The badge.</param>
  /// <param name="precision">The number of decimals.</param>
  /// <returns>The coverage without a percent sign, or <see langword="null" /> when it is unknown.</returns>
  internal static string? Value(Badge badge, int precision)
  {
    if (badge.Coverage is not { } coverage)
    {
      return null;
    }

    var scale = (decimal)Math.Pow(10, precision);
    var floored = Math.Floor(coverage * scale) / scale;

    return floored.ToString("F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
  }

  /// <summary>Reads the precision asked: 0, 1 or 2; anything else is the default, a named difference with Codecov.</summary>
  /// <param name="precision">The text of the parameter.</param>
  /// <returns>The number of decimals.</returns>
  internal static int ReadPrecision(string? precision) =>
    int.TryParse(precision, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed <= MaxPrecision ? parsed : DefaultPrecision;

  private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

  #endregion
}
