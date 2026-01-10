
using System;

namespace XPP.Doc;

public partial class XPDocument
{
  private XPNodeRef MakeRef(XPNodeRef from, int index)
  {
    if (!from.Valid || index == -1)
      return XPNodeRef.Invalid;
    return new(this, from.DocVersion, index);
  }

  private static readonly Node InvalidNode = new()
  {
    Index = -1,
    Value = "",
    Parent = -1,
    FirstContent = -1,
    LastContent = -1,
    FirstAttr = -1,
    LastAttr = -1,
    PrevSibling = -1,
    NextSibling = -1,
    DVersion = -1,
    VPrev = -1,
    VNext = -1,
  };

  private ref readonly Node Lookup(XPNodeRef node)
  {
    if (!node.Valid)
      return ref InvalidNode;
    return ref Latest(node.Index, node.DocVersion);
  }

  public XPNodeRef Root(int version)
  {
    version = Math.Clamp(version, 0, docVersion);
    return new(this, version, roots[version]);
  }

  public XPNodeRef LatestRoot => Root(int.MaxValue);

  public XPType Type(XPNodeRef of) => Lookup(of).Type;
  public XPName Name(XPNodeRef of) => Lookup(of).Name;
  public string Value(XPNodeRef of) => Lookup(of).Value;

  public XPNodeRef Canon(XPNodeRef of) => MakeRef(of, Lookup(of).Index);
  public XPNodeRef Parent(XPNodeRef of) => MakeRef(of, Lookup(of).Parent);
  public XPNodeRef FirstContent(XPNodeRef of) => MakeRef(of, Lookup(of).FirstContent);
  public XPNodeRef LastContent(XPNodeRef of) => MakeRef(of, Lookup(of).LastContent);
  public XPNodeRef FirstAttr(XPNodeRef of) => MakeRef(of, Lookup(of).FirstAttr);
  public XPNodeRef LastAttr(XPNodeRef of) => MakeRef(of, Lookup(of).LastAttr);
  public XPNodeRef PrevSibling(XPNodeRef of) => MakeRef(of, Lookup(of).PrevSibling);
  public XPNodeRef NextSibling(XPNodeRef of) => MakeRef(of, Lookup(of).NextSibling);
}

public partial struct XPNodeRef
{
  public XPType Type => Doc?.Type(this) ?? default;
  public XPName Name => Doc?.Name(this) ?? default;
  public string Value => Doc?.Value(this) ?? "";

  public XPNodeRef Canon => Doc?.Canon(this) ?? Invalid;
  public XPNodeRef Parent => Doc?.Parent(this) ?? Invalid;
  public XPNodeRef FirstContent => Doc?.FirstContent(this) ?? Invalid;
  public XPNodeRef LastContent => Doc?.LastContent(this) ?? Invalid;
  public XPNodeRef FirstAttr => Doc?.FirstAttr(this) ?? Invalid;
  public XPNodeRef LastAttr => Doc?.LastAttr(this) ?? Invalid;
  public XPNodeRef PrevSibling => Doc?.PrevSibling(this) ?? Invalid;
  public XPNodeRef NextSibling => Doc?.NextSibling(this) ?? Invalid;

  public bool SameAs(XPNodeRef other) => Valid && Canon.Index == other.Canon.Index;

  public XPNodeRef Attribute(string rawName)
  {
    if (!Valid)
      return Invalid;
    if (!Doc.ResolveName(Canon.Index, rawName, out var name))
      return Invalid;
    return Attribute(name);
  }

  public XPNodeRef Attribute(XPName name)
  {
    var attr = FirstAttr;
    while (attr.Valid)
    {
      if (attr.Type == XPType.Attribute && attr.Name == name)
        return attr;
      attr = attr.NextSibling;
    }
    return Invalid;
  }
}