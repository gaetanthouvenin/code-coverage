// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubManifestConversionResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The answer of <c>POST /app-manifests/{code}/conversions</c>.</summary>
/// <param name="Id">The identifier of the App.</param>
/// <param name="Slug">The slug of the App.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="ClientSecret">The client secret.</param>
/// <param name="WebhookSecret">The secret that signs the webhooks.</param>
/// <param name="Pem">The private key.</param>
internal sealed record GitHubManifestConversionResponse(long Id, string Slug, string ClientId, string ClientSecret, string WebhookSecret, string Pem);
