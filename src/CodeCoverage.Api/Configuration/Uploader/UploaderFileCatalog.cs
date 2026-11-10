// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderFileCatalog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace CodeCoverage.Api.Configuration.Uploader;

/// <summary>The closed list of the runtimes the uploader is published for, and the file of each.</summary>
/// <remarks>
///   The file name served for a runtime is a constant of this list: nothing a caller sends ever becomes
///   a part of a path. <c>Dockerfile.api</c> publishes exactly these files, under these names.
/// </remarks>
public static class UploaderFileCatalog
{
  #region Fields

  /// <summary>The suffix of the file that holds the SHA-256 of a binary, and of the address that serves it.</summary>
  public const string ChecksumSuffix = ".sha256";

  private const string FilePrefix = "coverage-upload-";

  private const string WindowsExtension = ".exe";

  private static readonly FrozenDictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal)
  {
    ["linux-x64"] = FilePrefix + "linux-x64",
    ["linux-musl-x64"] = FilePrefix + "linux-musl-x64",
    ["win-x64"] = FilePrefix + "win-x64" + WindowsExtension,
    ["osx-arm64"] = FilePrefix + "osx-arm64",
  }.ToFrozenDictionary(StringComparer.Ordinal);

  #endregion

  #region Properties

  /// <summary>Gets the runtime identifiers the uploader is published for.</summary>
  public static IReadOnlyCollection<string> RuntimeIdentifiers => Files.Keys;

  #endregion

  #region Methods

  /// <summary>Finds the file of a runtime.</summary>
  /// <param name="runtimeIdentifier">The runtime identifier, compared exactly: no case folding, no trimming.</param>
  /// <param name="fileName">The name of its binary in the directory of the uploader, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> when the uploader is published for this runtime.</returns>
  public static bool TryGetFileName([NotNullWhen(true)] string? runtimeIdentifier, [NotNullWhen(true)] out string? fileName)
  {
    fileName = null;

    return runtimeIdentifier is not null && Files.TryGetValue(runtimeIdentifier, out fileName);
  }

  #endregion
}
