// ------------------------------------------------------------------------------------------------
// <copyright file="VarIntWriter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Buffers;
using System.Text;

namespace CodeCoverage.Infrastructure.Reports.Codecs;

/// <summary>Writes bytes, variable-length integers and strings into a growing buffer.</summary>
internal sealed class VarIntWriter
{
  #region Fields

  private const int MaxVarIntBytes = 5;
  private const uint ContinuationBit = 0x80;
  private const int PayloadBits = 7;

  private static readonly UTF8Encoding StrictUtf8 = new(false, true);

  private readonly ArrayBufferWriter<byte> _buffer = new();

  #endregion

  #region Properties

  /// <summary>Gets the bytes written so far.</summary>
  public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

  #endregion

  #region Methods

  /// <summary>Writes one byte.</summary>
  /// <param name="value">The value.</param>
  public void WriteByte(byte value)
  {
    _buffer.GetSpan(1)[0] = value;
    _buffer.Advance(1);
  }

  /// <summary>Writes an integer on 1 to 5 bytes, 7 bits at a time, the lowest bits first.</summary>
  /// <param name="value">The value.</param>
  public void WriteUInt32(uint value)
  {
    var span = _buffer.GetSpan(MaxVarIntBytes);
    var length = 0;

    while (value >= ContinuationBit)
    {
      span[length++] = (byte)(value | ContinuationBit);
      value >>= PayloadBits;
    }

    span[length++] = (byte)value;
    _buffer.Advance(length);
  }

  /// <summary>Writes an integer that cannot be negative.</summary>
  /// <param name="value">The value.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="value" /> is negative.</exception>
  public void WriteInt32(int value)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(value);

    WriteUInt32((uint)value);
  }

  /// <summary>Writes a string as its UTF-8 length followed by its UTF-8 bytes.</summary>
  /// <param name="value">The string.</param>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="value" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="EncoderFallbackException">
  ///   <paramref name="value" /> is not valid UTF-16, so it could not be read back unchanged.
  /// </exception>
  public void WriteString(string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    var length = StrictUtf8.GetByteCount(value);

    WriteInt32(length);
    StrictUtf8.GetBytes(value, _buffer.GetSpan(length));
    _buffer.Advance(length);
  }

  #endregion
}
