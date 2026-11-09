// ------------------------------------------------------------------------------------------------
// <copyright file="DiffCacheCodec.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Text.Json;

using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>Turns the files of a diff into the gzip JSON blob of <c>DiffCache</c>.</summary>
internal static class DiffCacheCodec
{
  #region Methods

  /// <summary>Encodes files.</summary>
  /// <param name="files">The files.</param>
  /// <returns>The blob.</returns>
  public static byte[] Encode(IEnumerable<FileDiff> files)
  {
    var model = files.Select(static f => new DiffCacheFile(f.Path, f.PreviousPath, f.Status.ToString(), f.Patch)).ToList();

    using var output = new MemoryStream();

    using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
    {
      JsonSerializer.Serialize(gzip, model, DiffCacheJsonContext.Default.ListDiffCacheFile);
    }

    return output.ToArray();
  }

  /// <summary>Decodes a blob written by <see cref="Encode" />.</summary>
  /// <param name="blob">The blob.</param>
  /// <returns>The files.</returns>
  /// <exception cref="JsonException">The blob holds no list of files.</exception>
  public static IReadOnlyList<FileDiff> Decode(byte[] blob)
  {
    using var gzip = new GZipStream(new MemoryStream(blob), CompressionMode.Decompress);

    var model = JsonSerializer.Deserialize(gzip, DiffCacheJsonContext.Default.ListDiffCacheFile)
                ?? throw new JsonException("A cached diff holds a list of files.");

    return [.. model.Select(static f => FileDiff.Create(f.Path, f.PreviousPath, Enum.Parse<FileDiffStatus>(f.Status), f.Patch))];
  }

  #endregion
}
