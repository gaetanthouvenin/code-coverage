// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryDepartureTokens.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Repositories;

/// <summary>What the departure of a repository did to the upload tokens of its organization.</summary>
/// <param name="Revoked">How many tokens of the repository were revoked.</param>
/// <param name="RotationAdvised">How many tokens of the organization were marked as to be replaced.</param>
public sealed record RepositoryDepartureTokens(int Revoked, int RotationAdvised);
