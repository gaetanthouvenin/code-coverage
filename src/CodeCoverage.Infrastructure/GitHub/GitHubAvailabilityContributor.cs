// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAvailabilityContributor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Tells whether GitHub can admit organizations and sign users in, from the settings of the GitHub App.</summary>
/// <param name="options">The <c>GitHub</c> settings group, read at each call.</param>
/// <remarks>
///   Registered as the <see cref="IForgeAvailabilityContributor" /> keyed by <see cref="Domain.Organizations.Forge.GitHub" />:
///   <c>GitHub:AppId</c> rules the admission, <c>GitHub:ClientId</c> the sign-in.
/// </remarks>
internal sealed class GitHubAvailabilityContributor(IOptionsMonitor<GitHubOptions> options) : IForgeAvailabilityContributor
{
  #region Implementation of IForgeAvailabilityContributor

  /// <inheritdoc />
  public bool CanAdmit => !string.IsNullOrWhiteSpace(options.CurrentValue.AppId);

  /// <inheritdoc />
  public bool CanSignIn => !string.IsNullOrWhiteSpace(options.CurrentValue.ClientId);

  #endregion
}
