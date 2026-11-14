// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovJsonContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

using CodeCoverage.Infrastructure.Codecov.Models;

namespace CodeCoverage.Infrastructure.Codecov;

/// <summary>The source-generated serialization of the payloads of the API v2 of Codecov, in <c>snake_case</c>.</summary>
/// <remarks>A required field that is missing makes the reading fail: an unexpected payload surfaces, never a half-filled record.</remarks>
[JsonSourceGenerationOptions(
  PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
  RespectNullableAnnotations = true,
  RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(CodecovCommitListResponse))]
internal sealed partial class CodecovJsonContext : JsonSerializerContext;
