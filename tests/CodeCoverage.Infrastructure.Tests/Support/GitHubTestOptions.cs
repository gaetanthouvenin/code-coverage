// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubTestOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// Stands for an options class of a host, bound to the GitHub section and read through IOptionsMonitor.
public sealed class GitHubTestOptions
{
  #region Properties

  public string? AppSlug { get; set; }

  #endregion
}
