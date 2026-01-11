
using System;
using System.Text;

namespace XPP.Doc;

public partial class XPDocument
{
  private XPNodeRef MakeRef(XPNodeRef from, int index)
  {
    if (!from.Valid || index == -1)
      return XPNodeRef.Invalid;
    return new(this, from.DocVersion, index);
  }

  private XPNodeRef MakeRefExact(int index)
  {
    if (index == -1)
      return XPNodeRef.Invalid;
    return new(this, nodes[index].DVersion, index);
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

  private ref readonly Node Lookup(XPNodeRef node, int version, bool allowLate = false)
  {
    if (!node.Valid)
      return ref InvalidNode;
    ref var lnode = ref Latest(node.Index, version);
    if (!allowLate && lnode.DVersion > version)
      return ref InvalidNode;
    return ref lnode;
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
  public int EditVersion(XPNodeRef of) => Lookup(of).DVersion;

  public XPNodeRef Canon(XPNodeRef of) => MakeRef(of, Lookup(of).Index);
  public XPNodeRef Parent(XPNodeRef of) => MakeRef(of, Lookup(of).Parent);
  public XPNodeRef FirstContent(XPNodeRef of) => MakeRef(of, Lookup(of).FirstContent);
  public XPNodeRef LastContent(XPNodeRef of) => MakeRef(of, Lookup(of).LastContent);
  public XPNodeRef FirstAttr(XPNodeRef of) => MakeRef(of, Lookup(of).FirstAttr);
  public XPNodeRef LastAttr(XPNodeRef of) => MakeRef(of, Lookup(of).LastAttr);
  public XPNodeRef PrevSibling(XPNodeRef of) => MakeRef(of, Lookup(of).PrevSibling);
  public XPNodeRef NextSibling(XPNodeRef of) => MakeRef(of, Lookup(of).NextSibling);
  public XPNodeRef PrevVersion(XPNodeRef of) => MakeRefExact(Lookup(of).VPrev);
  public XPNodeRef NextVersion(XPNodeRef of) => MakeRefExact(Lookup(of).VPrev);
  public XPNodeRef FirstVersion(XPNodeRef of) => MakeRefExact(Lookup(of, -1, true).Index);
  public XPNodeRef LatestVersion(XPNodeRef of) => MakeRefExact(Lookup(of, int.MaxValue).Index);
  public XPNodeRef AtVersion(XPNodeRef of, int version) => MakeRef(new(this, version, of.Index), Lookup(of, version).Index);

  public string ToString(XPNodeRef of, string indent = "")
  {
    ref readonly var node = ref Lookup(of);
    var sb = new StringBuilder();
    if (node.Type.IsAttribute)
      AddNodeInline(node.Index, sb, of.DocVersion, siblings: false);
    else
      AddNode(node.Index, sb, indent, of.DocVersion, siblings: false);
    return sb.ToString();
  }
}

public partial struct XPNodeRef
{
  public XPType Type => Doc?.Type(this) ?? default;
  public XPName Name => Doc?.Name(this) ?? default;
  public string Value => Doc?.Value(this) ?? "";
  public int EditVersion => Doc?.EditVersion(this) ?? -1;

  public XPNodeRef Canon => Doc?.Canon(this) ?? Invalid;
  public XPNodeRef Parent => Doc?.Parent(this) ?? Invalid;
  public XPNodeRef FirstContent => Doc?.FirstContent(this) ?? Invalid;
  public XPNodeRef LastContent => Doc?.LastContent(this) ?? Invalid;
  public XPNodeRef FirstAttr => Doc?.FirstAttr(this) ?? Invalid;
  public XPNodeRef LastAttr => Doc?.LastAttr(this) ?? Invalid;
  public XPNodeRef PrevSibling => Doc?.PrevSibling(this) ?? Invalid;
  public XPNodeRef NextSibling => Doc?.NextSibling(this) ?? Invalid;
  public XPNodeRef PrevVersion => Doc?.PrevVersion(this) ?? Invalid;
  public XPNodeRef NextVersion => Doc?.NextVersion(this) ?? Invalid;
  public XPNodeRef FirstVersion => Doc?.FirstVersion(this) ?? Invalid;
  public XPNodeRef LatestVersion => Doc?.LatestVersion(this) ?? Invalid;
  public XPNodeRef AtVersion(int version) => Doc?.AtVersion(this, version) ?? Invalid;

  public bool SameAs(XPNodeRef other) => Valid && Canon.Index == other.Canon.Index;

  public XPNodeRef Attribute(string rawName)
  {
    if (!Valid)
      return Invalid;
    if (!Doc.ResolveName(Canon.Index, DocVersion, rawName, out var name))
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

  public bool ResolveName(string raw, out XPName name) =>
    Doc.ResolveName(Canon.Index, DocVersion, raw, out name);

  public bool ResolveName(string prefix, string local, out XPName name) =>
    Doc.ResolveName(Canon.Index, DocVersion, prefix, local, out name);
}