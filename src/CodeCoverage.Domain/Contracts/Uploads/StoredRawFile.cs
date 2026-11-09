// ------------------------------------------------------------------------------------------------
// <copyright file="StoredRawFile.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>What was stored for a part of an upload.</summary>
/// <param name="Id">The identifier of the stored file.</param>
/// <param name="Sha256">The SHA-256 of its decompressed content.</param>
/// <param name="CompressedSize">The number of bytes stored.</param>
/// <param name="UncompressedSize">The number of bytes of its decompressed content.</param>
public sealed record StoredRawFile(long Id, byte[] Sha256, long CompressedSize, long UncompressedSize);
