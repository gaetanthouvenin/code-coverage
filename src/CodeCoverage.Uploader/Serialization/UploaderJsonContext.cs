// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderJsonContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

using CodeCoverage.Uploader.Http;

namespace CodeCoverage.Uploader.Serialization;

/// <summary>The source-generated JSON metadata, for trimmed binaries without reflection.</summary>
/// <remarks>The server writes camel case: the names are matched whatever their case.</remarks>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(UploadStatus))]
internal sealed partial class UploaderJsonContext : JsonSerializerContext;
