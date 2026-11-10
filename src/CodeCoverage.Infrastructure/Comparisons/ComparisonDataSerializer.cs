// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonDataSerializer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Text.Json;

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Infrastructure.Comparisons;

/// <summary>Writes and reads the comparison stored in <c>Comparisons.Data</c>: a version byte, then gzipped JSON.</summary>
/// <remarks>
///   <para>
///     The format is written by hand, field by field, so that renaming a member of the Domain never changes
///     what is stored. Totals are arrays <c>[hits, misses, partials, branches, methods]</c>; a missing side
///     is <c>null</c>; a decimal is written as a number, with every digit it has.
///   </para>
///   <para>
///     A blob of a version this code does not know is refused with a <see cref="NotSupportedException" />:
///     a stored comparison is a cache, and whoever reads it computes it again.
///   </para>
/// </remarks>
internal static class ComparisonDataSerializer
{
  #region Fields

  /// <summary>The version of the format this code writes.</summary>
  public const byte CurrentVersion = 1;

  #endregion

  #region Methods

  /// <summary>Serializes a comparison.</summary>
  /// <param name="comparison">The comparison.</param>
  /// <returns>The blob.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="comparison" /> is <see langword="null" />.</exception>
  public static byte[] Serialize(Comparison comparison)
  {
    ArgumentNullException.ThrowIfNull(comparison);

    using var output = new MemoryStream();

    output.WriteByte(CurrentVersion);

    using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
    using (var writer = new Utf8JsonWriter(gzip))
    {
      writer.WriteStartObject();
      WriteTotals(writer, "head", comparison.Head);
      WriteTotals(writer, "base", comparison.Base);
      writer.WriteBoolean("diffIncomplete", comparison.DiffIncomplete);
      WritePatch(writer, "patch", comparison.Patch);

      writer.WriteStartArray("files");

      foreach (var file in comparison.Files)
      {
        writer.WriteStartObject();
        writer.WriteString("path", file.Path);
        writer.WriteString("previousPath", file.PreviousPath);
        writer.WriteString("status", file.Status.ToString());
        WriteTotals(writer, "base", file.Base);
        WriteTotals(writer, "head", file.Head);
        WriteTotals(writer, "patch", file.Patch);
        writer.WriteEndObject();
      }

      writer.WriteEndArray();
      writer.WriteStartArray("flags");

      foreach (var flag in comparison.Flags)
      {
        writer.WriteStartObject();
        writer.WriteString("flag", flag.Flag);
        WriteTotals(writer, "base", flag.Base);
        WriteTotals(writer, "head", flag.Head);
        WritePatch(writer, "patch", flag.Patch);
        writer.WriteBoolean("carriedForward", flag.CarriedForward);
        writer.WriteEndObject();
      }

      writer.WriteEndArray();
      writer.WriteStartArray("indirectChanges");

      foreach (var change in comparison.IndirectChanges)
      {
        writer.WriteStartObject();
        writer.WriteString("path", change.Path);
        writer.WriteString("kind", change.Kind.ToString());
        writer.WriteNumber("hits", change.DeltaHits);
        writer.WriteNumber("misses", change.DeltaMisses);
        writer.WriteNumber("partials", change.DeltaPartials);

        if (change.DeltaCoverage is { } delta)
        {
          writer.WriteNumber("coverage", delta);
        }
        else
        {
          writer.WriteNull("coverage");
        }

        writer.WriteEndObject();
      }

      writer.WriteEndArray();
      writer.WriteEndObject();
    }

    return output.ToArray();
  }

  /// <summary>Reads a comparison back.</summary>
  /// <param name="blob">The blob.</param>
  /// <returns>The comparison.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="blob" /> is <see langword="null" />.</exception>
  /// <exception cref="NotSupportedException">The blob is empty or of a version this code does not know.</exception>
  /// <exception cref="InvalidDataException">The blob is not what <see cref="Serialize" /> writes.</exception>
  public static Comparison Deserialize(byte[] blob)
  {
    ArgumentNullException.ThrowIfNull(blob);

    if (blob.Length == 0 || blob[0] != CurrentVersion)
    {
      throw new NotSupportedException(
        blob.Length == 0 ? "A stored comparison cannot be empty." : $"The version {blob[0]} of a stored comparison is not supported."
      );
    }

    try
    {
      using var input = new MemoryStream(blob, 1, blob.Length - 1);
      using var gzip = new GZipStream(input, CompressionMode.Decompress);
      using var document = JsonDocument.Parse(gzip);

      var root = document.RootElement;

      return new Comparison(
        ReadTotals(root.GetProperty("head")) ?? default,
        ReadTotals(root.GetProperty("base")),
        ReadPatch(root.GetProperty("patch")),
        [
          .. root.GetProperty("files")
                 .EnumerateArray()
                 .Select(static file => new ImpactedFile(
                           file.GetProperty("path").GetString()!,
                           file.GetProperty("previousPath").GetString(),
                           Enum.Parse<FileDiffStatus>(file.GetProperty("status").GetString()!),
                           ReadTotals(file.GetProperty("base")),
                           ReadTotals(file.GetProperty("head")),
                           ReadTotals(file.GetProperty("patch")) ?? default
                         )
                 ),
        ],
        [
          .. root.GetProperty("flags")
                 .EnumerateArray()
                 .Select(static flag => new FlagComparison(
                           flag.GetProperty("flag").GetString()!,
                           ReadTotals(flag.GetProperty("base")),
                           ReadTotals(flag.GetProperty("head")),
                           ReadPatch(flag.GetProperty("patch")),
                           flag.GetProperty("carriedForward").GetBoolean()
                         )
                 ),
        ],
        [
          .. root.GetProperty("indirectChanges")
                 .EnumerateArray()
                 .Select(static change => new IndirectChange(
                           change.GetProperty("path").GetString()!,
                           Enum.Parse<IndirectChangeKind>(change.GetProperty("kind").GetString()!),
                           change.GetProperty("hits").GetInt32(),
                           change.GetProperty("misses").GetInt32(),
                           change.GetProperty("partials").GetInt32(),
                           change.GetProperty("coverage") is { ValueKind: JsonValueKind.Number } coverage ? coverage.GetDecimal() : null
                         )
                 ),
        ],
        root.GetProperty("diffIncomplete").GetBoolean()
      );
    }
    catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException or NullReferenceException)
    {
      throw new InvalidDataException("The stored comparison cannot be read.", exception);
    }
  }

  /// <summary>Writes totals, or null for a missing side.</summary>
  /// <param name="writer">The writer.</param>
  /// <param name="name">The name of the property.</param>
  /// <param name="totals">The totals, or <see langword="null" />.</param>
  private static void WriteTotals(Utf8JsonWriter writer, string name, CoverageTotals? totals)
  {
    if (totals is not { } value)
    {
      writer.WriteNull(name);

      return;
    }

    writer.WriteStartArray(name);
    writer.WriteNumberValue(value.Hits);
    writer.WriteNumberValue(value.Misses);
    writer.WriteNumberValue(value.Partials);
    writer.WriteNumberValue(value.Branches);
    writer.WriteNumberValue(value.Methods);
    writer.WriteEndArray();
  }

  /// <summary>Writes a patch coverage.</summary>
  /// <param name="writer">The writer.</param>
  /// <param name="name">The name of the property.</param>
  /// <param name="patch">The patch coverage.</param>
  private static void WritePatch(Utf8JsonWriter writer, string name, PatchCoverage patch)
  {
    writer.WriteStartObject(name);
    WriteTotals(writer, "totals", patch.Totals);
    writer.WriteStartArray("files");

    foreach (var file in patch.Files)
    {
      writer.WriteStartObject();
      writer.WriteString("path", file.Path);
      WriteTotals(writer, "totals", file.Totals);
      writer.WriteBoolean("diffUnavailable", file.DiffUnavailable);
      writer.WriteEndObject();
    }

    writer.WriteEndArray();
    writer.WriteEndObject();
  }

  /// <summary>Reads totals.</summary>
  /// <param name="element">The array of the totals, or null.</param>
  /// <returns>The totals, or <see langword="null" /> for a missing side.</returns>
  private static CoverageTotals? ReadTotals(JsonElement element)
  {
    if (element.ValueKind is JsonValueKind.Null)
    {
      return null;
    }

    return new CoverageTotals(element[0].GetInt32(), element[1].GetInt32(), element[2].GetInt32(), element[3].GetInt32(), element[4].GetInt32());
  }

  /// <summary>Reads a patch coverage.</summary>
  /// <param name="element">The object of the patch.</param>
  /// <returns>The patch coverage.</returns>
  private static PatchCoverage ReadPatch(JsonElement element) =>
    new(
      ReadTotals(element.GetProperty("totals")) ?? default,
      [
        .. element.GetProperty("files")
                  .EnumerateArray()
                  .Select(static file => new PatchFileCoverage(
                            file.GetProperty("path").GetString()!,
                            ReadTotals(file.GetProperty("totals")) ?? default,
                            file.GetProperty("diffUnavailable").GetBoolean()
                          )
                  ),
      ]
    );

  #endregion
}