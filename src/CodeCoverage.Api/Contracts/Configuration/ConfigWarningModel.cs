// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigWarningModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Configuration;

/// <summary>Something in a <c>codecov.yml</c> file that was not applied as written.</summary>
/// <param name="Code">The reason, in the <c>Feature.Reason</c> form.</param>
/// <param name="Key">Where it is, such as <c>coverage.range</c> or <c>ignore[2]</c>.</param>
/// <param name="Detail">The offending text, if there is one.</param>
public sealed record ConfigWarningModel(string Code, string Key, string? Detail);
