// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentationLinks.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Documentation;

/// <summary>The targets of the contextual help of the console: a page of the documentation and, often, a heading of it.</summary>
/// <remarks>
///   Every target names a real heading of the published document, and a test checks each one against the rendered
///   documentation. <c>/admin/settings</c> has no help until a document describes the settings of the instance.
/// </remarks>
public static class DocumentationLinks
{
  #region Fields

  /// <summary>The help of <c>/setup</c>: the first deployment of an instance.</summary>
  public const string Setup = "docs/operations#first-deployment";

  /// <summary>The help of <c>/admin/organizations</c>: the admission of an account.</summary>
  public const string AdmitAccount = "docs/organization-onboarding#2-admit-it";

  /// <summary>The help of <c>/orgs/{forge}/{org}/settings</c>: the five settings of an organization.</summary>
  public const string OrganizationSettings = "docs/organization-onboarding#3-configure-it";

  /// <summary>The help of <c>/orgs/{forge}/{org}/upload-tokens</c>: which token a pipeline uses.</summary>
  public const string UploadTokens = "docs/migration-from-codecov#2-tokens";

  /// <summary>The help of a repository without a report: how to send the first upload.</summary>
  public const string FirstUpload = "docs/github-actions";

  /// <summary>The help of <c>/{forge}/{owner}/{repo}/commit/{sha}/report</c>: the figures of the detailed report.</summary>
  public const string DetailedReport = "docs/migration-from-codecov#the-detailed-report";

  #endregion
}
