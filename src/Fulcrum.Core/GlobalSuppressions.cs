// This file is used by the .NET analyzers to suppress specific warnings at the project level.
// Fulcrum.Core only — other projects inherit suppressions from .editorconfig.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "The Result<T> pattern uses static factory methods (Success, Failure) on a generic type. This is the standard Result pattern.")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "'Error' is the standard name in Result patterns and does not cause confusion in this context.")]
