
using System;
using XPP.Path;

namespace XPP.Doc;

public struct XPNavigator(XPNodeRef Node) : IComparable<XPNavigator>
{
  public static readonly XPNavigator Invalid = new(XPNodeRef.Invalid);

  public readonly XPNodeRef Node = Node;

  public XPNavigator Clone() => this;
  public XPNavigator Root() => new(Node.Doc.Root(Node.Id.DocVersion));
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
  public bool PreviousSibling(out XPNavigator nav) =>
    Make(Node.Type.IsContent ? Node.PrevSibling : XPNodeRef.Invalid, out nav);
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

  public int StringValue(Span<char> buffer) => BuildStringValue(Node, buffer);

  private static int BuildStringValue(XPNodeRef node, Span<char> buffer)
  {
    if (!node.Valid)
      return 0;
    var type = node.Type;
    var length = 0;
    if (type is XPType.Comment)
    {
      // noop
    }
    else if (type.HasValue)
    {
      var val = node.Value;
      val.CopyTo(buffer);
      length += val.Length;
    }
    else if (type.CanHaveContent)
      length = BuildStringValue(node.FirstContent, buffer);

    if (!type.IsContent)
      return length;

    return length + BuildStringValue(node.FirstContent, buffer[length..]);
  }

  public bool SameAs(XPNavigator other) => Node.SameAs(other.Node);

  public static bool USE_OTREE = true;

  public int CompareTo(XPNavigator other)
  {
    if (USE_OTREE)
      return Node.Doc.Compare(Node.Id, other.Node.Id);
    if (Node.SameAs(other.Node))
      return 0;
    var n1 = Node;
    var n2 = other.Node;

    var d1 = Node.Depth; ;
    var d2 = other.Node.Depth;
    var initd1 = d1;
    var initd2 = d2;

    while (d1 > d2)
    {
      n1 = n1.Parent;
      d1--;
    }
    while (d2 > d1)
    {
      n2 = n2.Parent;
      d2--;
    }

    var a1 = Node.Type.IsAttribute;
    var a2 = other.Node.Type.IsAttribute;

    if (n1.SameAs(n2))
      return initd1 > initd2 ? 1 : -1;

    if (a1 && !a2)
      return -1;
    if (a2 && !a1)
      return 1;

    while (!n1.Parent.SameAs(n2.Parent))
    {
      n1 = n1.Parent;
      n2 = n2.Parent;
    }

    n1 = n1.Canon;
    var res = 0;
    var n2l = n2.PrevSibling;
    var n2r = n2.NextSibling;
    while (n2l.Valid || n2r.Valid)
    {
      if (n2l.Valid)
      {
        if (n2l.SameAs(n1))
        {
          res = -1;
          break;
        }
        n2l = n2l.PrevSibling;
      }
      if (n2r.Valid)
      {
        if (n2r.SameAs(n1))
        {
          res = 1;
          break;
        }
        n2r = n2r.NextSibling;
      }
    }
    if (res == 0)
      throw new InvalidOperationException();
    return res;
  }

  public string OuterXml() => Node.Doc.ToString(Node.Id);
}

public partial struct XPNodeRef
{
  public XPNavigator Nav => new(this);
}