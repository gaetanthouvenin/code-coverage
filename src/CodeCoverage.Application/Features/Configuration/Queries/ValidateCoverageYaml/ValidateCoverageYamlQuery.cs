// ------------------------------------------------------------------------------------------------
// <copyright file="ValidateCoverageYamlQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Config;

namespace CodeCoverage.Application.Features.Configuration.Queries.ValidateCoverageYaml;

/// <summary>Validates a <c>codecov.yml</c> file on its own, for anyone.</summary>
/// <param name="Yaml">The text of the file.</param>
public sealed record ValidateCoverageYamlQuery(string Yaml) : IQuery<ConfigValidationResult>, IAnonymousEntry;
