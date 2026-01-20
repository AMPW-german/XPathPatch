
using System;
using System.Text;

namespace XPP.Doc;

public partial class XPDocument
{
  private Node Lookup(VNode vnode, int version, bool allowLate = false)
  {
    if (vnode.Node == null)
      return null;
    var node = Latest(vnode.Node, version);
    if (!allowLate && node.DVersion > version)
      return null;
    return node;
  }

  public XPNodeRef Root(int version)
  {
    version = Math.Clamp(version, 0, docVersion);
    return new(this, roots[version], version);
  }

  public XPNodeRef LatestRoot => Root(int.MaxValue);

  internal void SetValue(VNode of, string value)
  {
    if (of.Version < docVersion)
      throw new InvalidOperationException("cannot set value of previous version");
    var node = Latest(of);
    if (!node.Type.HasValue)
      throw new InvalidOperationException($"cannot set value of {node.Type} node");
    if (node.Removed)
      throw new InvalidOperationException($"node has been removed");
    node = Current(node);
    node.Value = value;
  }

  internal string ToString(VNode of, string indent = "")
  {
    var node = Latest(of);
    var sb = new StringBuilder();
    if (node.Type.IsAttribute)
      AddNodeInline(node, sb, of.Version, siblings: false);
    else
      AddNode(node, sb, indent, of.Version, siblings: false);
    return sb.ToString();
  }

  internal int Compare(VNode left, VNode right)
  {
    var v = left.Version;
    if (v != right.Version)
      throw new InvalidOperationException($"doc versions mismatch");

    var lnode = Latest(left);
    var rnode = Latest(right);

    var old = lnode.Depth;
    var rld = rnode.Depth;

    while (lnode.Depth > rnode.Depth)
      lnode = Latest(lnode.Parent, v);
    while (rnode.Depth > lnode.Depth)
      rnode = Latest(rnode.Parent, v);

    if (lnode == rnode)
      return old.CompareTo(rld);

    do
    {
      var pleft = Latest(lnode.Parent, v);
      var pright = Latest(rnode.Parent, v);
      if (pleft == pright)
        break;
      lnode = pleft;
      rnode = pright;
    } while (true);

    if (lnode.Type.IsAttribute && !rnode.Type.IsAttribute)
      return -1;
    if (rnode.Type.IsAttribute && !lnode.Type.IsAttribute)
      return 1;

    var cmp = otree.Compare(
      Latest(lnode, v).Order,
      Latest(rnode, v).Order);

    if (cmp == 0)
      return old.CompareTo(rld);
    return cmp;
  }
}

public partial struct XPNodeRef
{
  internal XPDocument.Node Latest => XPDocument.Latest(Node, Version);
  internal XPDocument.VNode VNode => new(Latest, Version);

  private XPNodeRef Make(XPDocument.Node node) =>
    node != null ? new(Doc, node, Version) : Invalid;
  private XPNodeRef MakeExact(XPDocument.Node node) =>
    node != null ? new(Doc, node, node.DVersion) : Invalid;
  private XPNodeRef MakeAt(XPDocument.Node node, int version) =>
    node != null ? new(Doc, node, version) : Invalid;

  public XPType Type => Latest?.Type ?? default;
  public XPName Name => Latest?.Name ?? default;
  public string Value => Latest?.Value ?? "";
  public int EditVersion => Latest?.DVersion ?? -1;
  public int Depth => Latest?.Depth ?? -1;

  public XPNodeRef Parent => Make(Latest?.Parent);
  public XPNodeRef FirstContent => Make(Latest?.FirstContent);
  public XPNodeRef LastContent => Make(Latest?.LastContent);
  public XPNodeRef FirstAttr => Make(Latest?.FirstAttr);
  public XPNodeRef LastAttr => Make(Latest?.LastAttr);
  public XPNodeRef PrevSibling => Make(Latest?.PrevSibling);
  public XPNodeRef NextSibling => Make(Latest?.NextSibling);
  public XPNodeRef PrevVersion => MakeExact(Latest?.VPrev);
  public XPNodeRef NextVersion => MakeExact(Latest?.VNext);
  public XPNodeRef FirstVersion
  {
    get
    {
      var node = Node;
      while (node?.VPrev != null) node = node.VPrev;
      return MakeExact(node);
    }
  }
  public XPNodeRef LatestVersion
  {
    get
    {
      var node = Node;
      while (node?.VNext != null) node = node.VNext;
      return MakeAt(node, Doc?.Version ?? -1);
    }
  }
  public XPNodeRef AtVersion(int version) =>
    MakeAt(XPDocument.Latest(Node, version), version);
  public XPNodeRef AtVersion(Index version) =>
    AtVersion(version.GetOffset(Doc?.Version ?? int.MaxValue));

  public XPNodeRef AddChild(
    XPType type, string name, string value = null,
    XPNodeRef? before = null, XPNodeRef? after = null
  ) => Doc?.AddChild(
      Node, type, rawName: name, value: value,
      before: before?.Node, after: after?.Node
    ) ?? Invalid;

  public XPNodeRef AddChild(
    XPType type, XPName name, string value = null,
    XPNodeRef? before = null, XPNodeRef? after = null
  ) => Doc?.AddChild(
      Node, type, prefixedName: name, value: value,
      before: before?.Node, after: after?.Node
    ) ?? Invalid;

  public XPNodeRef AddElement(
    string name, XPNodeRef? before = null, XPNodeRef? after = null
  ) => AddChild(XPType.Element, name, before: before, after: after);
  public XPNodeRef AddElement(
    XPName name, XPNodeRef? before = null, XPNodeRef? after = null
  ) => AddChild(XPType.Element, name, before: before, after: after);

  public XPNodeRef AddAttribute(
    string name, string value, XPNodeRef? before = null, XPNodeRef? after = null
  ) => AddChild(XPType.Attribute, name, value: value, before: before, after: after);
  public XPNodeRef AddAttribute(
    XPName name, string value, XPNodeRef? before = null, XPNodeRef? after = null
  ) => AddChild(XPType.Attribute, name, value: value, before: before, after: after);

  public void SetValue(string value) => Doc.SetValue(VNode, value);

  public void Remove() => Doc.RemoveNode(Node);

  public bool SameAs(XPNodeRef other) => Latest == other.Latest;

  public XPNodeRef SetAttribute(string name, string value)
  {
    var attr = Attribute(name);
    if (attr.Valid)
    {
      attr.SetValue(value);
      return attr;
    }
    return AddAttribute(name, value);
  }

  public XPNodeRef SetAttribute(XPName name, string value)
  {
    var attr = Attribute(name);
    if (attr.Valid)
    {
      attr.SetValue(value);
      return attr;
    }
    return AddAttribute(name, value);
  }

  public XPNodeRef Attribute(string rawName)
  {
    if (!Valid)
      return Invalid;
    if (!XPDocument.ResolveName(VNode, rawName, out var name))
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
    XPDocument.ResolveName(VNode, raw, out name);

  public bool ResolveName(string prefix, string local, out XPName name) =>
    XPDocument.ResolveName(VNode, prefix, local, out name);

  public string DebugName
  {
    get
    {
      if (!Valid)
        return "Invalid";
      var parent = Parent;
      var pstring = "";
      if (parent.Valid && parent.Type != XPType.Document)
        pstring = parent.DebugName + "/";

      var index = 0;
      var prev = PrevSibling;
      while (prev.Valid)
      {
        if (prev.Type == Type && prev.Name == Name)
          index++;
        prev = prev.PrevSibling;
      }
      return $"{pstring}{Name.Local}#{index}";
    }
  }
}