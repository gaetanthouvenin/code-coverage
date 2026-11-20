// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovServiceRouteConstraintTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Routing;

using CodeCoverage.Api.Configuration.Routing;

namespace CodeCoverage.Api.Tests.Configuration;

// The constraint of the routes of Codecov (M7.30), read from the table of its segments.
public sealed class CodecovServiceRouteConstraintTests
{
  #region Fields

  private const string Key = "service";

  private readonly CodecovServiceRouteConstraint _constraint = new();

  #endregion

  #region Methods

  [Theory]
  [InlineData("gh")]
  [InlineData("github")]
  [InlineData("GitHub")]
  public void Match_CodecovService_ShouldAdmit(string service)
  {
    Match(new RouteValueDictionary { [Key] = service }).ShouldBeTrue();
  }

  [Theory]
  [InlineData("azure-devops")]
  [InlineData("svn")]
  [InlineData(null)]
  public void Match_OtherValue_ShouldRefuse(string? service)
  {
    Match(new RouteValueDictionary { [Key] = service }).ShouldBeFalse();
  }

  [Fact]
  public void Match_MissingValue_ShouldRefuse()
  {
    Match([]).ShouldBeFalse();
  }

  [Fact]
  public void Match_NullArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => _constraint.Match(null, null, null!, [], RouteDirection.IncomingRequest));
    Should.Throw<ArgumentNullException>(() => _constraint.Match(null, null, Key, null!, RouteDirection.IncomingRequest));
  }

  private bool Match(RouteValueDictionary values) => _constraint.Match(null, null, Key, values, RouteDirection.IncomingRequest);

  #endregion
}
