// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeAvailabilitySnapshot.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>The forges of the instance available for admission and for sign-in, at the moment of the reading.</summary>
/// <param name="AdmissionForges">The forges an organization can be admitted on.</param>
/// <param name="SignInForges">The forges a user can sign in with.</param>
/// <remarks>A read model of the configuration of the instance: it names forges, nothing of an organization.</remarks>
public sealed record ForgeAvailabilitySnapshot(IReadOnlyList<Forge> AdmissionForges, IReadOnlyList<Forge> SignInForges);
