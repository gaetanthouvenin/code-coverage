// ------------------------------------------------------------------------------------------------
// <copyright file="GitShaConverter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Infrastructure.Persistence.Conventions;

/// <summary>Stores a <see cref="GitSha" /> as its 40 hexadecimal characters, in a text column.</summary>
/// <remarks>
///   The value is read back through <see cref="GitSha.Parse" />, so an instance always holds the invariant of
///   its type. A column that holds anything else was not written by this model: reading the row throws rather
///   than returning a value that would later be placed in the address of a forge.
/// </remarks>
internal sealed class GitShaConverter : ValueConverter<GitSha, string>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GitShaConverter" /> class.</summary>
  public GitShaConverter()
    : base(static sha => sha.Value, static text => GitSha.Parse(text))
  {
  }

  #endregion
}
