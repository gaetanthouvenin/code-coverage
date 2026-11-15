// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentationSection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Documentation;

/// <summary>A section of the navigation of the in-app documentation, in the order of the mockup of M4.02n.</summary>
/// <remarks>Its label is the resource <c>Docs_Section_{name}</c> of <c>Common.resx</c>.</remarks>
public enum DocumentationSection
{
  /// <summary>Install an instance and run it for the first time.</summary>
  GettingStarted,

  /// <summary>Install the GitHub App and admit an account.</summary>
  GitHubApp,

  /// <summary>Send the coverage of a repository.</summary>
  ConfigureRepository,

  /// <summary>Run an instance in production.</summary>
  Operations,

  /// <summary>Move a repository from Codecov.</summary>
  Migration,
}
