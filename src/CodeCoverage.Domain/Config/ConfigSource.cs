// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigSource.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>Where the configuration of a commit came from.</summary>
public enum ConfigSource
{
  /// <summary>A file of the commit.</summary>
  Commit,

  /// <summary>The configuration read from the default branch of the repository.</summary>
  RepositoryDefault,

  /// <summary>The configuration shared by the organization.</summary>
  Organization,

  /// <summary>The built-in defaults only.</summary>
  Builtin,
}
