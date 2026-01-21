
using System;
using System.Text;
using XPP.Path;

namespace XPP.Doc;

public readonly struct XPNavigator(XPNodeRef Node) : IComparable<XPNavigator>
{
  public static readonly XPNavigator Invalid = new(XPNodeRef.Invalid);

  public readonly XPNodeRef Node = Node;

  public XPNavigator Clone() => this;
  public XPNavigator Root() => new(Node.Doc.Root(Node.Version));
  public NodeType Type() => Node.Type switch
  {
    XPType.Invalid => NodeType.Invalid,
    XPType.Document => NodeType.Node,
    XPType.Element => NodeType.Node,
    XPType.Text => NodeType.Text,
    XPType.CData => NodeType.Text,
    XPType.ProcInst => NodeType.ProcessingInstruction,
    XPType.Comment => NodeType.Comment,
    XPType.Attribute => NodeType.Invalid,
    XPType.Namespace => NodeType.Invalid,
    _ => NodeType.Invalid,
  };
  public bool IsAttribute() => Node.Type is XPType.Attribute;
  public bool IsNs() => Node.Type is XPType.Namespace;

  public bool HasNs(ReadOnlySpan<char> ns)
  {
    var name = Node.Name;
    if (ns.Length == 0)
      return name.NsUri == "";

    if (ns.SequenceEqual(name.Prefix))
      return true;

    if (!Node.ResolveName(new(ns), "x", out var resolved))
      return false;

    return resolved.NsUri == name.NsUri;
  }
  public bool HasName(ReadOnlySpan<char> name) => name.SequenceEqual(Node.Name.Local);

  public bool Parent(out XPNavigator nav) => Make(Node.Parent, out nav);
  public bool FirstChild(out XPNavigator nav) => Make(Node.FirstContent, out nav);
  public bool LastChild(out XPNavigator nav) => Make(Node.LastContent, out nav);
  public bool NextSibling(out XPNavigator nav) =>
    Make(Node.Type.IsContent ? Node.NextSibling : XPNodeRef.Invalid, out nav);
  public bool PreviousSibling(out XPNavigator nav) {
    if (!Node.Valid) throw new InvalidOperationException();
    return Make(Node.Type.IsContent ? Node.PrevSibling : XPNodeRef.Invalid, out nav);
  }
  public bool FirstAttribute(out XPNavigator nav) =>
    FirstOfType(Node.FirstAttr, XPType.Attribute, out nav);
  public bool NextAttribute(out XPNavigator nav) =>
    FirstOfType(Node.NextSibling, XPType.Attribute, out nav);
  public bool FirstNamespace(out XPNavigator nav) =>
    FirstOfType(Node.FirstAttr, XPType.Namespace, out nav);
  public bool NextNamespace(out XPNavigator nav) =>
    FirstOfType(Node.NextSibling, XPType.Namespace, out nav);

  private static bool FirstOfType(XPNodeRef node, XPType type, out XPNavigator nav)
  {
    if (!node.Type.SameChildTypeAs(type))
      return Make(XPNodeRef.Invalid, out nav);
    while (node.Valid && node.Type != type)
      node = node.NextSibling;
    return Make(node, out nav);
  }

  private static bool Make(XPNodeRef node, out XPNavigator nav) =>
    (nav = new(node)).Node.Valid;

  public void StringValue(StringBuilder sb) => BuildStringValue(Node, sb);

  private static void BuildStringValue(XPNodeRef node, StringBuilder sb)
  {
    if (!node.Valid)
      return;
    var type = node.Type;
    if (type is XPType.Comment)
    {
      // noop
    }
    else if (type.HasValue)
      sb.Append(node.Value);
    else if (type.CanHaveContent)
      BuildStringValue(node.FirstContent, sb);

    if (!type.IsContent)
      return;

    BuildStringValue(node.FirstContent, sb);
  }

  public bool SameAs(XPNavigator other) => Node.SameAs(other.Node);

  public int CompareTo(XPNavigator other) => Node.Doc.Compare(Node.VNode, other.Node.VNode);

  public string OuterXml() => Node.Doc.ToString(Node.VNode);
}

public partial struct XPNodeRef
{
  public XPNavigator Nav => new(this);
}