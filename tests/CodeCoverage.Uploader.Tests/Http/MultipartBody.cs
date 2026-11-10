// ------------------------------------------------------------------------------------------------
// <copyright file="MultipartBody.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Text;

namespace CodeCoverage.Uploader.Tests.Http;

// A minimal reader of the multipart bodies the uploader writes.
internal static class MultipartBody
{
  #region Methods

  public static IReadOnlyList<(string Headers, byte[] Content)> Parse(byte[] body, string boundary)
  {
    var text = Encoding.Latin1.GetString(body);
    var delimiter = $"--{boundary}";
    var parts = new List<(string Headers, byte[] Content)>();
    var position = text.IndexOf(delimiter, StringComparison.Ordinal);

    while (position >= 0)
    {
      var start = position + delimiter.Length;

      if (text.AsSpan(start).StartsWith("--", StringComparison.Ordinal))
      {
        break;
      }

      start += 2;
      var next = text.IndexOf("\r\n" + delimiter, start, StringComparison.Ordinal);
      var separator = text.IndexOf("\r\n\r\n", start, StringComparison.Ordinal);

      parts.Add((text[start..separator], Encoding.Latin1.GetBytes(text[(separator + 4)..next])));
      position = next + 2;
    }

    return parts;
  }

  public static string Text(byte[] content) => Encoding.UTF8.GetString(content);

  public static string Gunzip(byte[] content)
  {
    using var input = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
    using var output = new MemoryStream();

    input.CopyTo(output);

    return Encoding.UTF8.GetString(output.ToArray());
  }

  #endregion
}
