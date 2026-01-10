
using System;
using XPP.Path;

namespace XPP.Doc;

public struct NavAdapter(XPNodeRef node) : IXPathNav<NavAdapter>
{
  private readonly XPNodeRef node = node;

  public NavAdapter Clone() => this;
  public NavAdapter Root() => new(node.Doc.Root(node.DocVersion));
  public NodeType Type() => node.Type switch
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
  public bool IsAttribute() => node.Type is XPType.Attribute;
  public bool IsNs() => node.Type is XPType.Namespace;

  public bool HasNs(ReadOnlySpan<char> ns)
  {
    var name = node.Name;
    if (ns.Length == 0)
      return name.NsUri == "";

    if (ns.SequenceEqual(name.Prefix))
      return true;

    if (!node.Doc.ResolveName(node.Canon.Index, new(ns), "x", out var resolved))
      return false;

    return resolved.NsUri == name.NsUri;
  }
  public bool HasName(ReadOnlySpan<char> name) => name.SequenceEqual(node.Name.Local);

  public bool Parent(out NavAdapter nav) => Make(node.Parent, out nav);
  public bool FirstChild(out NavAdapter nav) => Make(node.FirstContent, out nav);
  public bool LastChild(out NavAdapter nav) => Make(node.LastContent, out nav);
  public bool NextSibling(out NavAdapter nav) =>
    Make(node.Type.IsContent ? node.NextSibling : XPNodeRef.Invalid, out nav);
  public bool PreviousSibling(out NavAdapter nav) =>
    Make(node.Type.IsContent ? node.PrevSibling : XPNodeRef.Invalid, out nav);
  public bool FirstAttribute(out NavAdapter nav) =>
    FirstOfType(node.FirstAttr, XPType.Attribute, out nav);
  public bool NextAttribute(out NavAdapter nav) =>
    FirstOfType(node.NextSibling, XPType.Attribute, out nav);
  public bool FirstNamespace(out NavAdapter nav) =>
    FirstOfType(node.FirstAttr, XPType.Namespace, out nav);
  public bool NextNamespace(out NavAdapter nav) =>
    FirstOfType(node.NextSibling, XPType.Namespace, out nav);

  private static bool FirstOfType(XPNodeRef node, XPType type, out NavAdapter nav)
  {
    if (!node.Type.SameChildTypeAs(type))
      return Make(XPNodeRef.Invalid, out nav);
    while (node.Valid && node.Type != type)
      node = node.NextSibling;
    return Make(node, out nav);
  }

  private static bool Make(XPNodeRef node, out NavAdapter nav) =>
    (nav = new(node)).node.Valid;

  public int StringValue(Span<char> buffer) => BuildStringValue(node, buffer);

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

  public int CompareTo(NavAdapter other)
  {
    if (node.SameAs(other.node))
      return 0;
    var n1 = node;
    var n2 = other.node;

    var d1 = Depth();
    var d2 = other.Depth();
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

    var a1 = node.Type.IsAttribute;
    var a2 = other.node.Type.IsAttribute;

    if (n1.SameAs(n2))
      return initd1 > initd2 ? 1 : -1;

    if (a1 && !a2)
      return -1;
    if (a2 && !a1)
      return 1;

    n1 = n1.Canon;
    while (n2.Valid)
    {
      if (n1.SameAs(n2))
        return 1;
      n2 = n2.PrevSibling;
    }
    return -1;
  }

  private int Depth()
  {
    var depth = 0;
    var node = this.node.Parent;
    while (node.Valid)
    {
      depth++;
      node = node.Parent;
    }
    return depth;
  }

  public string OuterXml() => node.Doc.ToString(node);
}

public partial struct XPNodeRef
{
  public NavAdapter Nav => new(this);
}