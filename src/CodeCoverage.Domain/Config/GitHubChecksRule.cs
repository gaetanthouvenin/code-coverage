// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubChecksRule.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>Whether the statuses are published as GitHub Check Runs, and with annotations (<c>github_checks</c>).</summary>
/// <param name="Enabled">Whether Check Runs may replace the statuses: <c>github_checks: false</c> turns them off.</param>
/// <param name="Annotations">Whether the patch check run annotates the added lines the tests do not cover.</param>
/// <remarks>
///   As Codecov, Check Runs are used unless the file says <c>false</c>, and annotations unless
///   <c>github_checks.annotations</c> is <c>false</c>. Here, they also need the switch of the organization
///   <c>Notifications:ChecksEnabled</c>, off by default (ADR-007).
/// </remarks>
public sealed record GitHubChecksRule(bool Enabled, bool Annotations)
{
  #region Properties

  /// <summary>Gets the rule of a file that says nothing: Check Runs with annotations.</summary>
  public static GitHubChecksRule Default { get; } = new(Enabled: true, Annotations: true);

  #endregion
}
