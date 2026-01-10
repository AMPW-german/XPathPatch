
namespace XPP.Doc;

public enum XPType
{
  Invalid,
  Document,
  Element,
  Text,
  CData,
  ProcInst,
  Comment,
  Attribute,
  Namespace,
}

public static partial class Extensions
{
  extension(XPType type)
  {
    public bool HasName => type switch
    {
      XPType.Element or XPType.ProcInst or XPType.Attribute or XPType.Namespace => true,
      _ => false,
    };
    public bool HasValue => type switch
    {
      XPType.Text or XPType.CData or XPType.ProcInst or XPType.Comment => true,
      XPType.Attribute or XPType.Namespace => true,
      _ => false,
    };
    public bool CanHaveContent => type switch
    {
      XPType.Document or XPType.Element => true,
      _ => false,
    };
    public bool IsAttribute => type is XPType.Attribute or XPType.Namespace;
    public bool IsContent => type switch
    {
      XPType.Element or XPType.Text or XPType.CData => true,
      XPType.ProcInst or XPType.Comment => true,
      _ => false,
    };
    public bool CanHaveAttributes => type is XPType.Element;

    public bool CanHaveChild(XPType child) => child switch
    {
      { IsAttribute: true } => type.CanHaveAttributes,
      _ when type is XPType.Document => child is XPType.Element,
      { IsContent: true } => type.CanHaveContent,
      _ => false,
    };

    public bool SameChildTypeAs(XPType other) => type switch
    {
      { IsAttribute: true } => other.IsAttribute,
      { IsContent: true } => other.IsContent,
      _ => false,
    };

    public bool HasSingleChild => type is XPType.Document;

    public bool SiblingsMerge => type is XPType.Text;

    public bool DistinctName => type switch
    {
      XPType.Attribute or XPType.Namespace => true,
      _ => false,
    };
  }
}
