// ------------------------------------------------------------------------------------------------
// <copyright file="RevokedToken.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>What the confirmation of a revocation needs: which token, and the name the user recognizes.</summary>
/// <param name="Id">The identifier of the token.</param>
/// <param name="Label">The name it was given.</param>
public sealed record RevokedToken(long Id, string Label);
