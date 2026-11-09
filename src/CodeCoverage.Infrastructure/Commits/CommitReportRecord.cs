// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>The merged report of a commit: one row of the <c>CommitReports</c> table.</summary>
/// <remarks>A technical record of the Infrastructure: the Application only sees the blobs.</remarks>
internal sealed class CommitReportRecord
{
  #region Properties

  /// <summary>Gets or sets the identifier of the commit.</summary>
  public long CommitId { get; set; }

  /// <summary>Gets or sets the revision of the report.</summary>
  public int Revision { get; set; }

  /// <summary>Gets or sets the format of <see cref="Data" />.</summary>
  public byte Codec { get; set; } = UploadCoverageRecord.CurrentCodec;

  /// <summary>Gets or sets the blob of the report, with every session merged.</summary>
  public byte[] Data { get; set; } = [];

  /// <summary>Gets or sets the blob of the counters of its files, read without decoding the lines.</summary>
  public byte[] FileIndex { get; set; } = [];

  #endregion
}
