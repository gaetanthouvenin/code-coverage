// ------------------------------------------------------------------------------------------------
// <copyright file="LatestRequestTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Messaging;

namespace CodeCoverage.Web.Tests.Messaging;

public sealed class LatestRequestTests
{
  #region Methods

  [Fact]
  public void IsLatest_AfterAnotherBegin_ShouldBeFalse()
  {
    var latest = new LatestRequest();

    var first = latest.Begin();
    latest.IsLatest(first).ShouldBeTrue();
    var second = latest.Begin();

    latest.IsLatest(first).ShouldBeFalse();
    latest.IsLatest(second).ShouldBeTrue();
  }

  #endregion
}
