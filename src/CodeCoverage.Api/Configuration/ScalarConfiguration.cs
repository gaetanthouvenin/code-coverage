// ------------------------------------------------------------------------------------------------
// <copyright file="ScalarConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Scalar.AspNetCore;

namespace CodeCoverage.Api.Configuration;

/// <summary>The Scalar interactive documentation of the REST API.</summary>
public static class ScalarConfiguration
{
  #region Fields

  /// <summary>The path of the Scalar UI, which redirects to <see cref="DocumentPath" />.</summary>
  public const string Path = "/scalar";

  /// <summary>The path of the Scalar UI of the <c>v1</c> document.</summary>
  public const string DocumentPath = Path + "/" + OpenApiConfiguration.DocumentName;

  #endregion

  #region Methods

  /// <summary>Maps the Scalar UI on <c>/scalar/v1</c>; <c>/scalar</c> redirects to it.</summary>
  /// <param name="app">The endpoint route builder.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="app" /> is <see langword="null" />.
  /// </exception>
  public static IEndpointRouteBuilder MapScalarDocumentation(this IEndpointRouteBuilder app)
  {
    ArgumentNullException.ThrowIfNull(app);

    app.MapScalarApiReference(options =>
    {
      options.Title = "CodeCoverage API";
      options.Theme = ScalarTheme.Purple;
      options.DefaultHttpClient =
        new KeyValuePair<ScalarTarget, ScalarClient>(ScalarTarget.CSharp, ScalarClient.HttpClient);
      options.Authentication = new ScalarAuthenticationOptions
      {
        PreferredSecuritySchemes = [OpenApiConfiguration.BearerScheme],
      };
      options.WithOpenApiRoutePattern(OpenApiConfiguration.DocumentPath);
    });

    app.MapGet(
         Path,
         context =>
         {
           context.Response.Redirect($"{context.Request.PathBase}{DocumentPath}");
           return Task.CompletedTask;
         }
       )
       .ExcludeFromDescription();

    return app;
  }

  #endregion
}
