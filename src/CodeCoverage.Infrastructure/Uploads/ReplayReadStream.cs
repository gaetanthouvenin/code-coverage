// ------------------------------------------------------------------------------------------------
// <copyright file="ReplayReadStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>Reads a source after the bytes already taken from it, and optionally hands over everything it reads.</summary>
/// <param name="source">The rest of the source, left open.</param>
/// <param name="head">The bytes already taken from the source.</param>
/// <param name="capture">Receives each block as it is read, or <see langword="null" />.</param>
internal sealed class ReplayReadStream(
  Stream source,
  ReadOnlyMemory<byte> head,
  Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? capture) : ReadOnlyStream
{
  #region Fields

  /// <summary>How many bytes of the head were read.</summary>
  private int _headRead;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The stream captures what it reads, which is asynchronous.</exception>
  public override int Read(byte[] buffer, int offset, int count)
  {
    if (capture is not null)
    {
      throw new NotSupportedException("A capturing stream is read asynchronously.");
    }

    var replayed = Replay(buffer.AsMemory(offset, count));

    return replayed > 0 ? replayed : source.Read(buffer, offset, count);
  }

  /// <inheritdoc />
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    var read = Replay(buffer);

    if (read == 0)
    {
      read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    if (read > 0 && capture is not null)
    {
      await capture(buffer[..read], cancellationToken).ConfigureAwait(false);
    }

    return read;
  }

  /// <summary>Copies the bytes of the head that remain.</summary>
  /// <param name="buffer">The destination.</param>
  /// <returns>The number of bytes copied.</returns>
  private int Replay(Memory<byte> buffer)
  {
    var count = Math.Min(head.Length - _headRead, buffer.Length);

    head.Slice(_headRead, count).CopyTo(buffer);
    _headRead += count;

    return count;
  }

  #endregion
}
