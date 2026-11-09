// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTreeCodec.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Text;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>Turns the files of a tree into the gzip blob of <c>RepositoryTrees</c>.</summary>
/// <remarks>The content is the number of files, then for each one its path and its blob SHA, length-prefixed in UTF-8.</remarks>
internal static class RepositoryTreeCodec
{
  #region Methods

  /// <summary>Encodes files.</summary>
  /// <param name="files">The files.</param>
  /// <returns>The blob.</returns>
  public static byte[] Encode(IReadOnlyCollection<ForgeTreeEntry> files)
  {
    using var output = new MemoryStream();

    using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
    using (var writer = new BinaryWriter(gzip, Encoding.UTF8))
    {
      writer.Write(files.Count);

      foreach (var file in files)
      {
        writer.Write(file.Path);
        writer.Write(file.Sha.Value);
      }
    }

    return output.ToArray();
  }

  /// <summary>Decodes a blob written by <see cref="Encode" />.</summary>
  /// <param name="blob">The blob.</param>
  /// <returns>The files.</returns>
  public static IReadOnlyList<ForgeTreeEntry> Decode(byte[] blob)
  {
    using var gzip = new GZipStream(new MemoryStream(blob), CompressionMode.Decompress);
    using var reader = new BinaryReader(gzip, Encoding.UTF8);

    var count = reader.ReadInt32();
    var files = new List<ForgeTreeEntry>(count);

    for (var index = 0; index < count; index++)
    {
      var path = reader.ReadString();
      files.Add(new ForgeTreeEntry(path, GitSha.Parse(reader.ReadString()), IsDirectory: false));
    }

    return files;
  }

  #endregion
}
