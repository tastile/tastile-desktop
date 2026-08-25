// Compile-only type marker for ThemeManagerOverrideTests.
// The actual TastileDesktop.Services.ThemeManager lives in the WinUI app project
// (src/TastileDesktop/Services/ThemeManager.cs) which depends on Microsoft.UI.* /
// Windows.UI.* assemblies not available in this test project. The contract test
// reads the source file as text, so a runtime-inert type marker is sufficient
// to make the verbatim `typeof(TastileDesktop.Services.ThemeManager)` reference
// resolve. Do not add any runtime members — keep this purely a type anchor.
namespace TastileDesktop.Services;

public static class ThemeManager
{
}