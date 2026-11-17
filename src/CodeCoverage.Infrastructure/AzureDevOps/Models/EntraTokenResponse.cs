// ------------------------------------------------------------------------------------------------
// <copyright file="EntraTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The answer of the Microsoft Entra ID token endpoint to a client credentials request.</summary>
/// <param name="AccessToken">The access token of the application for Azure DevOps.</param>
/// <param name="ExpiresIn">The number of seconds the token stays valid.</param>
/// <remarks>The endpoint names its fields in <c>snake_case</c>, unlike the Azure DevOps API.</remarks>
internal sealed record EntraTokenResponse(
  [property: JsonPropertyName("access_token")] string AccessToken,
  [property: JsonPropertyName("expires_in")] long ExpiresIn);
