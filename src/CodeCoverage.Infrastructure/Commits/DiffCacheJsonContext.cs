// ------------------------------------------------------------------------------------------------
// <copyright file="DiffCacheJsonContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>The serialization of the files of a cached diff.</summary>
[JsonSerializable(typeof(List<DiffCacheFile>))]
internal sealed partial class DiffCacheJsonContext : JsonSerializerContext;
