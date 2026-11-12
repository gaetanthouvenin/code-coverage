// ------------------------------------------------------------------------------------------------
// <copyright file="GeneratedReadStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// A long stream of incompressible bytes that is never held in memory, with a callback at regular intervals that receives
// the number of bytes read so far.
public sealed class GeneratedReadStream(long length, long sampleEvery, Action<long> onSample) : Stream
{
  #region Fields

  private readonly Random _random = new(1234);

  private long _position;

  private long _samples;

  #endregion

  #region Properties

  public override bool CanRead => true;

  public override bool CanSeek => false;

  public override bool CanWrite => false;

  public override long Length => throw new NotSupportedException();

  public override long Position
  {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  #endregion

  #region Methods

  public override void Flush()
  {
  }

  public override int Read(byte[] buffer, int offset, int count) => Fill(buffer.AsSpan(offset, count));

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
    ValueTask.FromResult(Fill(buffer.Span));

  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

  public override void SetLength(long value) => throw new NotSupportedException();

  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  private int Fill(Span<byte> buffer)
  {
    var count = (int)Math.Min(length - _position, buffer.Length);

    _random.NextBytes(buffer[..count]);
    _position += count;

    // Each multiple of the interval fires once, whatever the size of the reads: a stream of n intervals samples n times.
    while (_position >= (_samples + 1) * sampleEvery)
    {
      _samples++;
      onSample(_position);
    }

    return count;
  }

  #endregion
}
