using System.ComponentModel;

namespace Template.Domain.Models;

/// <summary>
/// SQL Server sequence identifiers. Annotate each member with <see cref="DescriptionAttribute"/>
/// using the exact sequence name in the database (for use with NEXT VALUE FOR).
/// </summary>
public enum DatabaseSequence
{
    // [Description("SEQ_Example")]
    // Example = 0,
}
