// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingLog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Commits;

/// <summary>What the processing of an upload reported: its messages and the paths it could not resolve.</summary>
/// <remarks>
///   <para>
///     A value never changes: <see cref="Add" /> and <see cref="AddPath" /> return a new one. A report can list
///     hundreds of thousands of paths, so the log stays small: at most <see cref="MaxPaths" /> paths and
///     <see cref="MaxBytes" /> bytes once stored, while the totals keep counting what was left out.
///   </para>
///   <para>
///     The size counts each text at the worst the JSON storage can make of it, an escape sequence for any
///     character outside the plain ones, so that the cap holds on the column and not only on the characters.
///   </para>
/// </remarks>
public sealed class ProcessingLog
{
  #region Fields

  /// <summary>The largest size of the stored log, in bytes.</summary>
  public const int MaxBytes = 64 * 1024;

  /// <summary>The largest number of paths kept.</summary>
  public const int MaxPaths = 200;

  /// <summary>The bytes reserved for what surrounds the items in the stored log: names, totals and brackets.</summary>
  private const int EnvelopeBytes = 128;

  /// <summary>The bytes an item takes besides its text: two quotes and a comma.</summary>
  private const int ItemOverheadBytes = 3;

  /// <summary>The bytes a character takes at worst, written as an escape sequence such as <c>é</c>.</summary>
  private const int EscapedCharacterBytes = 6;

  /// <summary>The size of the items and of the envelope, in bytes.</summary>
  private readonly int _size;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ProcessingLog" /> class.</summary>
  /// <param name="messages">The messages kept.</param>
  /// <param name="messageTotal">The number of messages reported.</param>
  /// <param name="paths">The paths kept.</param>
  /// <param name="pathTotal">The number of paths reported.</param>
  /// <param name="size">The size of the log, in bytes.</param>
  private ProcessingLog(
    IReadOnlyList<string> messages,
    int messageTotal,
    IReadOnlyList<string> paths,
    int pathTotal,
    int size)
  {
    Messages = messages;
    MessageTotal = messageTotal;
    Paths = paths;
    PathTotal = pathTotal;
    _size = size;
  }

  #endregion

  #region Properties

  /// <summary>Gets the empty log.</summary>
  public static ProcessingLog Empty { get; } = new([], 0, [], 0, EnvelopeBytes);

  /// <summary>Gets the messages kept, in the order they were reported.</summary>
  public IReadOnlyList<string> Messages { get; }

  /// <summary>Gets the number of messages reported, those left out included.</summary>
  public int MessageTotal { get; }

  /// <summary>Gets the paths kept, the first ones reported.</summary>
  public IReadOnlyList<string> Paths { get; }

  /// <summary>Gets the number of paths reported, those left out included.</summary>
  public int PathTotal { get; }

  /// <summary>Gets a value indicating whether a message or a path was left out.</summary>
  public bool IsTruncated => Messages.Count < MessageTotal || Paths.Count < PathTotal;

  #endregion

  #region Methods

  /// <summary>Rebuilds a log from what was stored.</summary>
  /// <param name="messages">The messages kept.</param>
  /// <param name="messageTotal">The number of messages reported.</param>
  /// <param name="paths">The paths kept.</param>
  /// <param name="pathTotal">The number of paths reported.</param>
  /// <returns>The log.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="messages" /> or <paramref name="paths" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">A total is lower than the number of items kept.</exception>
  public static ProcessingLog Restore(
    IReadOnlyList<string> messages,
    int messageTotal,
    IReadOnlyList<string> paths,
    int pathTotal)
  {
    ArgumentNullException.ThrowIfNull(messages);
    ArgumentNullException.ThrowIfNull(paths);

    ArgumentException.ThrowIf(messageTotal < messages.Count, "The total cannot be lower than the number of messages kept.", nameof(messageTotal));
    ArgumentException.ThrowIf(pathTotal < paths.Count, "The total cannot be lower than the number of paths kept.", nameof(pathTotal));

    var size = EnvelopeBytes + messages.Concat(paths).Sum(Cost);

    return new ProcessingLog(messages, messageTotal, paths, pathTotal, size);
  }

  /// <summary>Reports a message.</summary>
  /// <param name="message">The message.</param>
  /// <returns>The log with the message, or with its total only when the message does not fit any more.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
  public ProcessingLog Add(string message)
  {
    ArgumentNullException.ThrowIfNull(message);

    var cost = Cost(message);
    var total = checked(MessageTotal + 1);

    return _size + cost > MaxBytes
             ? new ProcessingLog(Messages, total, Paths, PathTotal, _size)
             : new ProcessingLog([.. Messages, message], total, Paths, PathTotal, _size + cost);
  }

  /// <summary>Reports a path that could not be resolved.</summary>
  /// <param name="path">The path.</param>
  /// <returns>The log with the path, or with its total only when <see cref="MaxPaths" /> or the size is reached.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is <see langword="null" />.</exception>
  public ProcessingLog AddPath(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    var cost = Cost(path);
    var total = checked(PathTotal + 1);

    return Paths.Count >= MaxPaths || _size + cost > MaxBytes
             ? new ProcessingLog(Messages, MessageTotal, Paths, total, _size)
             : new ProcessingLog(Messages, MessageTotal, [.. Paths, path], total, _size + cost);
  }

  /// <summary>Gets the bytes an item takes in the stored log, at worst.</summary>
  /// <param name="text">The text of the item.</param>
  /// <returns>The size, in bytes.</returns>
  private static int Cost(string text) =>
    ItemOverheadBytes + text.Sum(static c => IsPlain(c) ? 1 : EscapedCharacterBytes);

  /// <summary>Tells whether a character is stored as itself, whatever the JSON encoder.</summary>
  /// <param name="character">The character.</param>
  /// <returns><see langword="true" /> for a character that takes one byte; otherwise, <see langword="false" />.</returns>
  private static bool IsPlain(char character) =>
    char.IsAsciiLetterOrDigit(character) || character is ' ' or '.' or '-' or '_' or '/' or ':' or ',';

  #endregion
}
