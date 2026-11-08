// ------------------------------------------------------------------------------------------------
// <copyright file="SafeXmlReaderFactory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Xml;

namespace CodeCoverage.Infrastructure.Reports;

/// <summary>Creates the XML readers of the coverage reports, which come from outside.</summary>
/// <remarks>
///   A DOCTYPE is skipped and never fetched, so the DOCTYPE of a JaCoCo report is accepted, and
///   reading touches neither the network nor the file system. No entity being declared, a reference
///   to one makes the reader throw an <see cref="XmlException" />.
/// </remarks>
internal static class SafeXmlReaderFactory
{
  #region Fields

  private static readonly XmlReaderSettings Settings = new()
  {
    DtdProcessing = DtdProcessing.Ignore,
    XmlResolver = null,
    MaxCharactersFromEntities = 0,
    IgnoreComments = true,
    Async = true,
  };

  #endregion

  #region Methods

  /// <summary>Creates a reader over a stream, whose encoding it detects.</summary>
  /// <param name="stream">The stream; it stays open when the reader is disposed.</param>
  /// <returns>The reader.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="stream" /> is <see langword="null" />.
  /// </exception>
  public static XmlReader Create(Stream stream)
  {
    ArgumentNullException.ThrowIfNull(stream);

    return XmlReader.Create(stream, Settings);
  }

  /// <summary>Creates a reader over some text.</summary>
  /// <param name="text">The text; it stays open when the reader is disposed.</param>
  /// <returns>The reader.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="text" /> is <see langword="null" />.
  /// </exception>
  public static XmlReader Create(TextReader text)
  {
    ArgumentNullException.ThrowIfNull(text);

    return XmlReader.Create(text, Settings);
  }

  #endregion
}
