// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeFileContent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>The text of a file read from a forge within a size bound.</summary>
/// <param name="Text">The text of the file, or <see langword="null" /> when it is larger than the bound.</param>
public sealed record ForgeFileContent(string? Text)
{
  #region Properties

  /// <summary>Gets the answer for a file larger than the bound: nothing of it was kept.</summary>
  public static ForgeFileContent TooLarge { get; } = new((string?)null);

  /// <summary>Gets a value indicating whether the file is larger than the bound.</summary>
  public bool IsTooLarge => Text is null;

  #endregion
}
