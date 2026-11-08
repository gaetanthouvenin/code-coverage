// ------------------------------------------------------------------------------------------------
// <copyright file="Principals.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// The principals a message can be sent for, faked over ICurrentUser.
internal static class Principals
{
  #region Methods

  public static ICurrentUser Anonymous()
  {
    return A.Fake<ICurrentUser>();
  }

  public static ICurrentUser User(bool isAdmin = false)
  {
    var user = A.Fake<ICurrentUser>();
    A.CallTo(() => user.UserId).Returns(42L);
    A.CallTo(() => user.Login).Returns("octocat");
    A.CallTo(() => user.IsAuthenticated).Returns(true);
    A.CallTo(() => user.IsAdmin).Returns(isAdmin);

    return user;
  }

  public static ICurrentUser System()
  {
    var system = A.Fake<ICurrentUser>();
    A.CallTo(() => system.IsSystem).Returns(true);

    return system;
  }

  #endregion
}
