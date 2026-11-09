// ------------------------------------------------------------------------------------------------
// <copyright file="VarIntReader.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;
using System.Text.Unicode;

namespace CodeCoverage.Infrastructure.Reports.Codecs;

/// <summary>Reads what <see cref="VarIntWriter" /> wrote, refusing whatever does not fit.</summary>
/// <remarks>
///   Every failure is an <see cref="InvalidDataException" />. A count read from the data is checked
///   against the bytes left before anything is allocated for it, so a corrupted count can never
///   ask for more memory than the data itself justifies.
/// </remarks>
internal ref struct VarIntReader
{
  #region Fields

  private const byte ContinuationBit = 0x80;
  private const byte PayloadMask = 0x7F;
  private const int PayloadBits = 7;
  private const int LastByteShift = 28;
  private const byte LastByteMaximum = 0x0F;

  private readonly ReadOnlySpan<byte> _data;
  private int _position;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="VarIntReader" /> struct.</summary>
  /// <param name="data">The data to read.</param>
  public VarIntReader(ReadOnlySpan<byte> data)
  {
    _data = data;
  }

  #endregion

  #region Properties

  /// <summary>Gets the number of bytes left.</summary>
  public readonly int Remaining => _data.Length - _position;

  #endregion

  #region Methods

  /// <summary>Reads one byte.</summary>
  /// <returns>The value.</returns>
  /// <exception cref="InvalidDataException">The data ends here.</exception>
  public byte ReadByte()
  {
    if (_position >= _data.Length)
    {
      throw Corrupted("it ends too early");
    }

    return _data[_position++];
  }

  /// <summary>Reads an integer written on 1 to 5 bytes.</summary>
  /// <returns>The value.</returns>
  /// <exception cref="InvalidDataException">The data ends, or the value exceeds 32 bits.</exception>
  public uint ReadUInt32()
  {
    var result = 0u;
    var shift = 0;

    while (true)
    {
      var current = ReadByte();

      if (shift == LastByteShift && current > LastByteMaximum)
      {
        throw Corrupted("a variable-length integer exceeds 32 bits");
      }

      result |= (uint)(current & PayloadMask) << shift;

      if (current < ContinuationBit)
      {
        return result;
      }

      shift += PayloadBits;
    }
  }

  /// <summary>Reads an integer that cannot be negative.</summary>
  /// <returns>The value.</returns>
  /// <exception cref="InvalidDataException">The data ends, or the value exceeds an integer.</exception>
  public int ReadInt32()
  {
    var value = ReadUInt32();

    return value <= int.MaxValue ? (int)value : throw Corrupted("an integer is out of range");
  }

  /// <summary>Reads the number of items that follow.</summary>
  /// <param name="minimumItemBytes">The fewest bytes an item can take.</param>
  /// <returns>The number of items, safe to allocate.</returns>
  /// <exception cref="InvalidDataException">The bytes left cannot hold that many items.</exception>
  public int ReadCount(int minimumItemBytes)
  {
    var count = ReadInt32();

    return count <= Remaining / minimumItemBytes
      ? count
      : throw Corrupted("a count exceeds what the remaining bytes can hold");
  }

  /// <summary>Reads a string written as its UTF-8 length followed by its UTF-8 bytes.</summary>
  /// <returns>The string.</returns>
  /// <exception cref="InvalidDataException">The data ends, or the bytes are not valid UTF-8.</exception>
  public string ReadString()
  {
    var length = ReadCount(1);
    var bytes = _data.Slice(_position, length);

    if (!Utf8.IsValid(bytes))
    {
      throw Corrupted("a string is not valid UTF-8");
    }

    _position += length;

    return Encoding.UTF8.GetString(bytes);
  }

  /// <summary>Checks that everything was read.</summary>
  /// <exception cref="InvalidDataException">Bytes are left.</exception>
  public readonly void ThrowIfNotAtEnd()
  {
    if (Remaining != 0)
    {
      throw Corrupted("unexpected bytes follow its content");
    }
  }

  /// <summary>Creates the error of a failed read.</summary>
  /// <param name="reason">What is wrong, as the end of a sentence.</param>
  /// <returns>The exception to throw, which tells where the read stopped.</returns>
  public readonly InvalidDataException Corrupted(string reason)
  {
    return new InvalidDataException($"The coverage blob is corrupted at offset {_position} of its body: {reason}.");
  }

  #endregion
}
