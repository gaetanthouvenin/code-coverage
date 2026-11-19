// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsJsonContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

using CodeCoverage.Infrastructure.AzureDevOps.Models;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>The source-generated serialization of the payloads of Azure DevOps and of its identity provider, in <c>camelCase</c>.</summary>
/// <remarks>
///   A missing or <see langword="null" /> field that a model declares as required makes the reading fail, so an
///   unexpected payload surfaces as an unreadable answer and never as a half-filled record. A model of Microsoft Entra
///   ID names its <c>snake_case</c> fields itself.
/// </remarks>
[JsonSourceGenerationOptions(
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
  RespectNullableAnnotations = true,
  RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(EntraTokenResponse))]
[JsonSerializable(typeof(EntraUserTokenResponse))]
[JsonSerializable(typeof(AzureDevOpsCommit))]
[JsonSerializable(typeof(AzureDevOpsList<AzureDevOpsAccount>))]
[JsonSerializable(typeof(AzureDevOpsList<AzureDevOpsRef>))]
[JsonSerializable(typeof(AzureDevOpsProfile))]
[JsonSerializable(typeof(AzureDevOpsPullRequest))]
[JsonSerializable(typeof(AzureDevOpsRepository))]
internal sealed partial class AzureDevOpsJsonContext : JsonSerializerContext;
