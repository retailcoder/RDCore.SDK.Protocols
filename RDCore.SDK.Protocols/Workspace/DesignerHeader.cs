using RDCore.SDK.Workspace.Forms;

namespace RDCore.SDK.Workspace;

/// <summary>
/// The designer a module's header names (<see cref="ModuleHeader.DesignerOf"/>): the <c>Begin &lt;class&gt; &lt;name&gt;</c> line that opens its
/// designer block, which says what the module's designer is (<c>VB.Form</c>, <c>VB.UserControl</c>, a VBA UserForm) and what it names the form.
/// </summary>
/// <param name="Class">
/// The designer's class: <c>VB.Form</c>, <c>VB.MDIForm</c>, <c>VB.UserControl</c> or <c>VB.PropertyPage</c> for a Visual Basic 6.0 designer;
/// <c>MSForms.UserForm</c>, with <see cref="FormControlClass.MSFormsDesignerClassId"/>, for a VBA UserForm; the class identifier alone for another ActiveX
/// designer.
/// </param>
/// <param name="Name">The name the line gives the form (<c>Form1</c>).</param>
/// <param name="Line">The zero-based line of the <c>Begin</c> line in the module's text, its lines split at each line feed.</param>
/// <param name="NameColumn">The zero-based column of <see cref="Line"/> where <paramref name="Name"/> starts.</param>
/// <param name="EndLine">
/// The zero-based line of the <c>End</c> that closes the designer block, or <see langword="null"/> when the text never closes it. Each control's
/// <c>Begin</c> line opens a block that its own <c>End</c> closes. The lines of a property group (<c>BeginProperty</c> to <c>EndProperty</c>) are the
/// property's: a <c>Begin</c> or an <c>End</c> among them opens or closes no block. The platform reads no designer from a block that is never closed.
/// </param>
public sealed record class DesignerHeader(FormControlClass Class, string Name, int Line, int NameColumn, int? EndLine);
