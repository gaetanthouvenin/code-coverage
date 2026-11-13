// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationTokensViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Tokens;

/// <summary>The upload tokens of an organization, with the warning that a rotation is advised.</summary>
/// <param name="Tokens">The tokens, the newest first, the revoked ones included.</param>
/// <param name="RotationAdvised">
///   Whether a repository left the organization while one of these tokens was live: such a token stays valid in the
///   secrets of that repository until it is revoked.
/// </param>
public sealed record OrganizationTokensViewModel(IReadOnlyList<UploadTokenViewModel> Tokens, bool RotationAdvised);
