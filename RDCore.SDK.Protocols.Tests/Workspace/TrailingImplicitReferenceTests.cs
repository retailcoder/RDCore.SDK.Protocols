using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The libraries a project references after the ones its project file lists (RD-VBAL §2.3.1.3): Windows Script Host's, when a module of the project is a
/// script and its language does not have them among its own.
/// </summary>
[TestClass]
public sealed class TrailingImplicitReferenceTests
{
    private static readonly IReadOnlyList<string> ScriptHost = SupportedLanguages.VBScript.ImplicitReferences;

    private static IEnumerable<SupportedLanguage> NotVBScript => SupportedLanguages.All.Where(language => language != SupportedLanguages.VBScript);

    // every language but VBScript has neither of Windows Script Host's libraries among its own: a project of it references them only for its scripts.
    [TestMethod]
    [DataRow("Lib.vbs", DisplayName = "a script")]
    [DataRow("Scripts/Lib.VBS", DisplayName = "a script in a folder, in capitals")]
    public void AProjectWithAScript_ReferencesWindowsScriptHost_AfterTheListedReferences(string script)
    {
        CollectionAssert.Contains(ScriptHost.ToArray(), "IHost", "WScript is IHost's: the test needs rewriting if VBScript's implicit references change");
        foreach (var language in NotVBScript)
        {
            string[] modules = ["Program.bas", script];
            Assert.IsFalse(language.ImplicitReferences.Intersect(ScriptHost, StringComparer.OrdinalIgnoreCase).Any(), $"{language.Id} has none of its own");
            CollectionAssert.AreEqual(ScriptHost.ToArray(), language.TrailingImplicitReferencesOf(modules).ToArray(), language.Id);
            Assert.IsFalse(language.ImplicitReferencesOf(modules).Intersect(ScriptHost, StringComparer.OrdinalIgnoreCase).Any(),
                $"{language.Id}: not before the listed references");
        }
    }

    [TestMethod]
    public void AProjectWithNoScript_ReferencesNothingAfterTheListedReferences()
    {
        foreach (var language in SupportedLanguages.All)
        {
            Assert.IsEmpty(language.TrailingImplicitReferencesOf(["Program.bas", "Form1.rdform", null]), language.Id);
        }
    }

    // a project of scripts has the libraries as its language's own, before the listed references, and not a second time after them.
    [TestMethod]
    public void AProjectOfScripts_HasWindowsScriptHostAsItsLanguagesOwn_AndNotAgainAfterTheListedReferences()
    {
        string[] modules = ["Backup.vbs", "Restore.vbs"];

        CollectionAssert.AreEqual(ScriptHost.ToArray(), SupportedLanguages.VBScript.ImplicitReferencesOf(modules).ToArray());
        Assert.IsEmpty(SupportedLanguages.VBScript.TrailingImplicitReferencesOf(modules));
    }
}
