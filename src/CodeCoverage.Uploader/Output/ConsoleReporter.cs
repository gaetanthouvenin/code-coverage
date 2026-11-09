// ------------------------------------------------------------------------------------------------
// <copyright file="ConsoleReporter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Output;

/// <summary>Prints the messages of the uploader.</summary>
/// <remarks>
///   Everything goes to one writer, the standard output in production, warnings and errors
///   included: a CI step set to fail on anything written to the standard error would otherwise
///   break the build, which the uploader must never do.
/// </remarks>
/// <param name="output">The writer that receives the messages.</param>
internal sealed class ConsoleReporter(TextWriter output)
{
  #region Properties

  /// <summary>Gets the writer that receives the messages.</summary>
  public TextWriter Output { get; } = output;

  /// <summary>Gets or sets a value indicating whether the details are printed.</summary>
  public bool IsVerbose { get; set; }

  #endregion

  #region Methods

  /// <summary>Prints a message.</summary>
  /// <param name="message">The message.</param>
  public void Info(string message)
  {
    Output.WriteLine(message);
  }

  /// <summary>Prints a warning.</summary>
  /// <param name="message">The message.</param>
  public void Warning(string message)
  {
    Output.WriteLine($"warning: {message}");
  }

  /// <summary>Prints an error.</summary>
  /// <param name="message">The message.</param>
  public void Error(string message)
  {
    Output.WriteLine($"error: {message}");
  }

  /// <summary>Prints a detail, only when <see cref="IsVerbose" /> is set.</summary>
  /// <param name="message">The message.</param>
  public void Detail(string message)
  {
    if (IsVerbose)
    {
      Output.WriteLine(message);
    }
  }

  #endregion
}
