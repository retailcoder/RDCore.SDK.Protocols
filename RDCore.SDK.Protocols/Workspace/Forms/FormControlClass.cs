namespace RDCore.SDK.Workspace.Forms;

/// <summary>
/// The class of a form or of one of its controls: the library that defines it and the class's name in that library,
/// or the class identifier of an ActiveX control the designer data names only by its CLSID.
/// </summary>
/// <param name="Library">The name of the library that defines the class (<c>MSForms</c>, <c>VB</c>, <c>MSComctlLib</c>), or
/// <c>null</c> when the designer data names the class by its CLSID only.</param>
/// <param name="Name">The name of the class in its library (<c>TextBox</c>), or <c>null</c> when the designer data names the
/// class by its CLSID only.</param>
/// <param name="ClassId">The class identifier, when the designer data gives one.</param>
public sealed record class FormControlClass(string? Library, string? Name, Guid? ClassId = null)
{
    /// <summary>
    /// The class identifier of the Microsoft Forms 2.0 designer, which names a VBA UserForm's designer block
    /// (<c>Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} UserForm1</c>).
    /// </summary>
    public static readonly Guid MSFormsDesignerClassId = new("C62A69F0-16DC-11CE-9E98-00AA00574A4F");

    /// <summary>
    /// The name of the Microsoft Forms type library, which qualifies the classes of a UserForm and of its controls (<c>MSForms.TextBox</c>).
    /// </summary>
    public const string MSFormsLibrary = "MSForms";

    /// <summary>
    /// The class a designer's <c>Begin &lt;class&gt; &lt;name&gt;</c> line names in its <c>&lt;class&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <c>VB.CommandButton</c> and <c>MSComctlLib.ListView</c> name a library and a class; a bare name (<c>Label</c>) names a class of no library; a class
    /// identifier in braces (<c>{8BD21D10-EC42-11CE-9E0D-00AA006002F3}</c>) names an ActiveX class by its CLSID only.
    /// </remarks>
    /// <param name="token">The <c>&lt;class&gt;</c> of the <c>Begin</c> line, as it is written.</param>
    /// <returns>The class, with its library and name, or with its class identifier only.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public static FormControlClass Parse(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (Guid.TryParse(token, out var classId))
        {
            return new FormControlClass(null, null, classId);
        }

        var dot = token.IndexOf('.');
        return dot < 0 ? new FormControlClass(null, token) : new FormControlClass(token[..dot], token[(dot + 1)..]);
    }

    /// <summary>
    /// The library-qualified name of the class (<c>MSForms.TextBox</c>), or its class identifier in braces when the
    /// designer data names it by its CLSID only.
    /// </summary>
    public override string ToString()
        => Name is null ? $"{{{ClassId?.ToString().ToUpperInvariant()}}}" : Library is null ? Name : $"{Library}.{Name}";
}
