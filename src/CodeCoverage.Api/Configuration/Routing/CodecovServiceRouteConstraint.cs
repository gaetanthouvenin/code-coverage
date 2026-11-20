// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovServiceRouteConstraint.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Api.Configuration.Routing;

/// <summary>Admits the service segment of an address of Codecov: a segment of <see cref="CodecovServiceSegments" />, whatever its case.</summary>
/// <remarks>
///   The routes compatible with Codecov follow the table: a forge it gains a row for gets its badge addresses, and a forge
///   without a row (Azure DevOps) only has the neutral route.
/// </remarks>
internal sealed class CodecovServiceRouteConstraint : IRouteConstraint
{
  #region Fields

  /// <summary>The name of the constraint in a route template.</summary>
  public const string Name = "codecovService";

  #endregion

  #region Methods

  /// <summary>Tells whether the value of the parameter is a service segment of Codecov.</summary>
  /// <param name="httpContext">The request, unused.</param>
  /// <param name="route">The router, unused.</param>
  /// <param name="routeKey">The name of the parameter.</param>
  /// <param name="values">The values of the route.</param>
  /// <param name="routeDirection">Whether a request is matched or an address generated, unused.</param>
  /// <returns><see langword="true" /> when the value is a service segment of Codecov.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="routeKey" /> or <paramref name="values" /> is <see langword="null" />.</exception>
  public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
  {
    ArgumentNullException.ThrowIfNull(routeKey);
    ArgumentNullException.ThrowIfNull(values);

    return values.TryGetValue(routeKey, out var value) && CodecovServiceSegments.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out _);
  }

  #endregion
}
