// <copyright file="RegexCleanTextCustomAPI.cs" company="Stewart">
// Copyright 2025 Stewart
// </copyright>

namespace StewartTitle.Argano.APIs
{
    using System;
    using System.Text.RegularExpressions;
    using Microsoft.Xrm.Sdk;

    /// <summary>
    /// Receives an input text and a regex pattern. It returns a cleaned-up text based on both parameters.
    /// </summary>
    /// <remarks>
    /// Input parameters:
    ///     Input: Input text.
    ///     Pattern: Regex pattern.
    /// Output parameters:
    ///     CleanedText: Resulting cleaned-up text.
    /// </remarks>
    public class RegexCleanTextCustomAPI : IPlugin
    {
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            tracingService.Trace("RegexCleanTextCustomAPI started executing.");

            try
            {
                if (context.InputParameters.Contains("Input") && context.InputParameters["Input"] is string input &&
                    context.InputParameters.Contains("Pattern") && context.InputParameters["Pattern"] is string pattern)
                {
                    string cleanedText = Regex.Replace(input, pattern, string.Empty);

                    tracingService.Trace($"Cleaned text: {cleanedText}");
                    context.OutputParameters["CleanedText"] = cleanedText;
                }
            }

            catch (Exception ex)
            {
                tracingService.Trace($"Exception: {ex.Message}");
                context.OutputParameters["ResultCount"] = "An error occurred during processing.";
                context.OutputParameters["response"] = $"Error: {ex.Message}";
            }
        }
    }
}
