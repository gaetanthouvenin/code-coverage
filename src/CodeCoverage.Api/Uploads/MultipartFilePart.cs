// ------------------------------------------------------------------------------------------------
// <copyright file="MultipartFilePart.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Uploads;

/// <summary>A file of an upload request, read as it arrives.</summary>
/// <param name="IsNetwork">A value indicating whether it holds the list of the files of the repository, and not a report.</param>
/// <param name="FileName">The name it was sent under.</param>
/// <param name="Content">Its content, valid until the next part is read.</param>
internal sealed record MultipartFilePart(bool IsNetwork, string FileName, Stream Content);
