// ------------------------------------------------------------------------------------------------
// <copyright file="GitShaListConverter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Infrastructure.Persistence.Conventions;

/// <summary>Stores a list of <see cref="GitSha" /> as a JSON array of texts.</summary>
/// <remarks>
///   Every item is read back through <see cref="GitSha.Parse" />, as <see cref="GitShaConverter" /> does for a
///   single value. Pair it with <see cref="JsonListConverter{T}.Comparer" />.
/// </remarks>
internal sealed class GitShaListConverter : ValueConverter<IReadOnlyList<GitSha>, string>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GitShaListConverter" /> class.</summary>
  public GitShaListConverter()
    : base(static shas => Write(shas), static json => Read(json))
  {
  }

  #endregion

  #region Methods

  /// <summary>Writes the SHAs as a JSON array.</summary>
  /// <param name="shas">The SHAs.</param>
  /// <returns>The JSON array.</returns>
  private static string Write(IReadOnlyList<GitSha> shas) => JsonSerializer.Serialize(shas.Select(static sha => sha.Value));

  /// <summary>Reads a JSON array of SHAs.</summary>
  /// <param name="json">The JSON array.</param>
  /// <returns>The SHAs, in order.</returns>
  /// <exception cref="ArgumentException">An item is not a SHA.</exception>
  private static IReadOnlyList<GitSha> Read(string json) =>
    (JsonSerializer.Deserialize<string[]>(json) ?? []).Select(GitSha.Parse).ToArray();

  #endregion
}
