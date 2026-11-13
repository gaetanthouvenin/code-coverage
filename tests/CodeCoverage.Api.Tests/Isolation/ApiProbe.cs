// ------------------------------------------------------------------------------------------------
// <copyright file="ApiProbe.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Tests.Isolation;

// What an action is probed with: the request to send against the organization the caller knows nothing about, or the
// reason why the action addresses no organization. An action with neither fails EveryAction_ShouldDeclareProbe.
public sealed record ApiProbe
{
  #region Ctors

  private ApiProbe(string action, HttpMethod? method, string? path, string? body, string? reason)
  {
    Action = action;
    Method = method;
    Path = path;
    Body = body;
    Reason = reason;
  }

  #endregion

  #region Properties

  // "Controller.Method", as the suite names an action.
  public string Action { get; }

  public HttpMethod? Method { get; }

  // The address of the organization of the probe; {owner} stands for the owner the suite substitutes.
  public string? Path { get; }

  public string? Body { get; }

  // Why the action addresses no organization, for the ones that are not probed across organizations.
  public string? Reason { get; }

  public bool IsCrossOrganization => Path is not null;

  #endregion

  #region Methods

  // An action addressed to an organization: the suite sends it once at the other organization, once at an owner that
  // never existed, and the two answers must be the same.
  public static ApiProbe Addressed(string action, HttpMethod method, string path, string? body = null) =>
    new(action, method, path, body, null);

  // An action that addresses no organization: the reason is read by the review, and the action is still declared.
  public static ApiProbe NotAddressed(string action, string reason) => new(action, null, null, null, reason);

  public override string ToString() => Action;

  #endregion
}
