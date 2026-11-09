// ------------------------------------------------------------------------------------------------
// <copyright file="ValidationResultResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

namespace CodeCoverage.Api.Contracts.Configuration;

/// <summary>The result of the validation of a <c>codecov.yml</c> file.</summary>
/// <param name="Valid">Whether the file can be used.</param>
/// <param name="Normalized">The content of the file once read, or <see langword="null" /> if it cannot be read.</param>
/// <param name="Warnings">What could not be applied as written.</param>
/// <param name="Errors">Why the file cannot be read, if so.</param>
public sealed record ValidationResultResponse(
  bool Valid,
  JsonElement? Normalized,
  IReadOnlyList<ConfigWarningModel> Warnings,
  IReadOnlyList<string> Errors);
