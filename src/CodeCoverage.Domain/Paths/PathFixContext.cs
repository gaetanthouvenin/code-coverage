// ------------------------------------------------------------------------------------------------
// <copyright file="PathFixContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Paths;

/// <summary>What the <see cref="PathFixer" /> knows about the report whose paths it fixes.</summary>
public sealed record PathFixContext
{
  #region Properties

  /// <summary>Gets the compiled <c>fixes</c> of the configuration.</summary>
  public FixRule Fixes { get; init; } = FixRule.None;

  /// <summary>Gets the root directory sent by the uploader, if any.</summary>
  public string? Root { get; init; }

  /// <summary>Gets the source directories declared by a Cobertura report.</summary>
  public IReadOnlyList<string> SourceRoots { get; init; } = [];

  /// <summary>Gets the path of the report file in the upload, if known.</summary>
  public string? ReportPath { get; init; }

  /// <summary>Gets the files of the repository, or <see langword="null" /> if unknown.</summary>
  public NetworkIndex? Network { get; init; }

  /// <summary>Gets a value indicating whether <c>codecov.disable_default_path_fixes</c> is set.</summary>
  public bool DisableDefaultPathFixes { get; init; }

  /// <summary>Gets the <c>paths</c> of the flags of the upload, which break a tie between files.</summary>
  public PathPatternSet FlagPaths { get; init; } = new([]);

  /// <summary>Gets a value indicating whether <c>Coverage:AllowFilenameOnlyMatch</c> is set.</summary>
  public bool AllowFilenameOnlyMatch { get; init; }

  #endregion
}
