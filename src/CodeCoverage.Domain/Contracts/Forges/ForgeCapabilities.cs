// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeCapabilities.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge accepts in what this application writes on it (ADR-014).</summary>
/// <param name="CommentMaxLength">
///   The longest body of a pull request comment that is published on the forge, in characters, kept under the limit
///   the forge enforces.
/// </param>
/// <remarks>
///   Each adapter gives the capabilities of its forge, which <see cref="Ports.IForgeGatewayProvider.GetCapabilities" />
///   resolves, so that no limit of one forge is written in the use cases.
/// </remarks>
public sealed record ForgeCapabilities(int CommentMaxLength);
