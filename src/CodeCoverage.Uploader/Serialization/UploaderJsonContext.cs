// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderJsonContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeCoverage.Uploader.Serialization;

/// <summary>The source-generated JSON metadata, for trimmed binaries without reflection.</summary>
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class UploaderJsonContext : JsonSerializerContext;
