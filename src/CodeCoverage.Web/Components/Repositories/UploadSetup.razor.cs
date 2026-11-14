// ------------------------------------------------------------------------------------------------
// <copyright file="UploadSetup.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   What to put in a workflow to send the coverage of a repository: a GitHub Actions job that grants
///   <c>id-token: write</c> and runs the composite action pinned on the deployed version, with the public address of the
///   Api, and links to the Azure Pipelines template and to the upload tokens of the repository. Without the public
///   address of the Api, no snippet can be written, and the panel says so.
/// </summary>
public partial class UploadSetup
{
  #region Fields

  /// <summary>The identifier of the panel, which the empty state of the overview links to.</summary>
  public const string AnchorId = "upload-setup";

  /// <summary>
  ///   The GitHub repository that publishes the composite action, the uploader and the Azure Pipelines template. No
  ///   setting carries it: it is the repository the deployed images are built from.
  /// </summary>
  public const string ActionRepository = "gaetanthouvenin/code-coverage";

  #endregion

  #region Parameters

  /// <summary>Gets or sets the forge segment of the address.</summary>
  [Parameter, EditorRequired]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the owner of the repository.</summary>
  [Parameter, EditorRequired]
  public string Owner { get; set; } = null!;

  /// <summary>Gets or sets the name of the repository.</summary>
  [Parameter, EditorRequired]
  public string Name { get; set; } = null!;

  /// <summary>Gets or sets the public address of the Api, or <see langword="null" /> before it is set.</summary>
  [Parameter]
  public string? ApiPublicUrl { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>
  ///   Gets the deployed version, without the <c>v</c> of its tag: the informational version of the Web without its
  ///   build metadata (<c>+…</c>), as the release of the action and of the uploader carry the same number.
  /// </summary>
  public static string DeployedVersion { get; } = VersionOf(typeof(UploadSetup).Assembly);

  // The line endings of the snippet do not depend on those of this source file.
  private string? Snippet =>
    string.IsNullOrWhiteSpace(ApiPublicUrl)
      ? null
      : $$$"""
          jobs:
            test:
              runs-on: ubuntu-latest
              permissions:
                contents: read
                id-token: write   # the upload authenticates with the OIDC token of GitHub Actions
              steps:
                - uses: actions/checkout@v6
                # ... build and test, producing a coverage report ...
                - uses: {{{ActionRepository}}}/action@v{{{DeployedVersion}}}
                  if: ${{ !cancelled() }}
                  with:
                    version: {{{DeployedVersion}}}
                    url: {{{ApiPublicUrl.TrimEnd('/')}}}
          """.ReplaceLineEndings("\n");

  private static string AzurePipelinesUrl => $"https://github.com/{ActionRepository}/blob/v{DeployedVersion}/pipelines/templates/coverage-upload.yml";

  private string UploadTokensHref => $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}/settings";

  #endregion

  #region Methods

  private static string VersionOf(Assembly assembly)
  {
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    var metadata = version.IndexOf('+', StringComparison.Ordinal);

    return metadata < 0 ? version : version[..metadata];
  }

  #endregion
}
