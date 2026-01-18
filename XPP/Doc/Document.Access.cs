
using System;
using System.Text;

namespace XPP.Doc;

public partial class XPDocument
{
  private XPNodeRef MakeRef(XPNodeId from, int index) =>
    MakeRefAt(from.DocVersion, index);

  private XPNodeRef MakeRefExact(int index)
  {
    if (index == -1)
      return XPNodeRef.Invalid;
    return MakeRefAt(nodes[index].DVersion, index);
  }

  private XPNodeRef MakeRefAt(int version, int index)
  {
    if (index == -1)
      return XPNodeRef.Invalid;
    ref var node = ref Latest(index, version);
    if (node.Removed)
      return XPNodeRef.Invalid;
    return new(this, new(version, node.Index));
  }

  private static readonly Node InvalidNode = new()
  {
    Index = -1,
    Value = "",
    Depth = -1,
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
    Removed = true,
  };

  private ref readonly Node Lookup(XPNodeId id)
  {
    if (!id.Valid)
      return ref InvalidNode;
    return ref Latest(id);
  }

  private ref readonly Node Lookup(XPNodeId Id, int version, bool allowLate = false)
  {
    if (!Id.Valid)
      return ref InvalidNode;
    ref var lnode = ref Latest(Id.Index, version);
    if (!allowLate && lnode.DVersion > version)
      return ref InvalidNode;
    return ref lnode;
  }

  public XPNodeRef Root(int version)
  {
    version = Math.Clamp(version, 0, docVersion);
    return new(this, new(version, roots[version]));
  }

  public XPNodeRef LatestRoot => Root(int.MaxValue);

  public XPType Type(XPNodeId of) => Lookup(of).Type;
  public XPName Name(XPNodeId of) => Lookup(of).Name;
  public string Value(XPNodeId of) => Lookup(of).Value;
  public int EditVersion(XPNodeId of) => Lookup(of).DVersion;
  public int Depth(XPNodeId of) => Lookup(of).Depth;

  public XPNodeRef Canon(XPNodeId of) => MakeRef(of, Lookup(of).Index);
  public XPNodeRef Parent(XPNodeId of) => MakeRef(of, Lookup(of).Parent);
  public XPNodeRef FirstContent(XPNodeId of) => MakeRef(of, Lookup(of).FirstContent);
  public XPNodeRef LastContent(XPNodeId of) => MakeRef(of, Lookup(of).LastContent);
  public XPNodeRef FirstAttr(XPNodeId of) => MakeRef(of, Lookup(of).FirstAttr);
  public XPNodeRef LastAttr(XPNodeId of) => MakeRef(of, Lookup(of).LastAttr);
  public XPNodeRef PrevSibling(XPNodeId of) => MakeRef(of, Lookup(of).PrevSibling);
  public XPNodeRef NextSibling(XPNodeId of) => MakeRef(of, Lookup(of).NextSibling);
  public XPNodeRef PrevVersion(XPNodeId of) => MakeRefExact(Lookup(of).VPrev);
  public XPNodeRef NextVersion(XPNodeId of) => MakeRefExact(Lookup(of).VPrev);
  public XPNodeRef FirstVersion(XPNodeId of) => MakeRefExact(Lookup(of, -1, true).Index);
  public XPNodeRef LatestVersion(XPNodeId of) => MakeRefAt(docVersion, of.Index);
  public XPNodeRef AtVersion(XPNodeId of, int version) => MakeRefAt(version, of.Index);

  public void SetValue(XPNodeId of, string value)
  {
    if (of.DocVersion < docVersion)
      throw new InvalidOperationException("cannot set value of previous version");
    ref var node = ref Latest(of.Index, of.DocVersion);
    if (!node.Type.HasValue)
      throw new InvalidOperationException($"cannot set value of {node.Type} node");
    if (node.Removed)
      throw new InvalidOperationException($"node has been removed");
    node = ref Current(node.Index);
    node.Value = value;
  }

  public string ToString(XPNodeId of, string indent = "")
  {
    ref readonly var node = ref Lookup(of);
    var sb = new StringBuilder();
    if (node.Type.IsAttribute)
      AddNodeInline(node.Index, sb, of.DocVersion, siblings: false);
    else
      AddNode(node.Index, sb, indent, of.DocVersion, siblings: false);
    return sb.ToString();
  }

  public int Compare(XPNodeId left, XPNodeId right)
  {
    var v = left.DocVersion;
    if (v != right.DocVersion)
      throw new InvalidOperationException($"doc versions mismatch");

    ref var lnode = ref Latest(left);
    ref var rnode = ref Latest(right);

    var old = lnode.Depth;
    var rld = rnode.Depth;

    while (lnode.Depth > rnode.Depth)
      lnode = ref Latest(lnode.Parent, v);
    while (rnode.Depth > lnode.Depth)
      rnode = ref Latest(rnode.Parent, v);

    if (lnode.Index == rnode.Index)
      return old.CompareTo(rld);

    do
    {
      ref var pleft = ref Latest(lnode.Parent, v);
      ref var pright = ref Latest(rnode.Parent, v);
      if (pleft.Index == pright.Index)
        break;
      lnode = ref pleft;
      rnode = ref pright;
    } while (true);

    if (lnode.Type.IsAttribute && !rnode.Type.IsAttribute)
      return -1;
    if (rnode.Type.IsAttribute && !lnode.Type.IsAttribute)
      return 1;

    var cmp = otree.Compare(
      Latest(lnode.Index, v).Order,
      Latest(rnode.Index, v).Order);

    if (cmp == 0)
      return old.CompareTo(rld);
    return cmp;
  }
}

public partial struct XPNodeRef
{
  public XPType Type => Doc?.Type(Id) ?? default;
  public XPName Name => Doc?.Name(Id) ?? default;
  public string Value => Doc?.Value(Id) ?? "";
  public int EditVersion => Doc?.EditVersion(Id) ?? -1;
  public int Depth => Doc?.Depth(Id) ?? -1;

  public XPNodeRef Canon => Doc?.Canon(Id) ?? Invalid;
  public XPNodeRef Parent => Doc?.Parent(Id) ?? Invalid;
  public XPNodeRef FirstContent => Doc?.FirstContent(Id) ?? Invalid;
  public XPNodeRef LastContent => Doc?.LastContent(Id) ?? Invalid;
  public XPNodeRef FirstAttr => Doc?.FirstAttr(Id) ?? Invalid;
  public XPNodeRef LastAttr => Doc?.LastAttr(Id) ?? Invalid;
  public XPNodeRef PrevSibling => Doc?.PrevSibling(Id) ?? Invalid;
  public XPNodeRef NextSibling => Doc?.NextSibling(Id) ?? Invalid;
  public XPNodeRef PrevVersion => Doc?.PrevVersion(Id) ?? Invalid;
  public XPNodeRef NextVersion => Doc?.NextVersion(Id) ?? Invalid;
  public XPNodeRef FirstVersion => Doc?.FirstVersion(Id) ?? Invalid;
  public XPNodeRef LatestVersion => Doc?.LatestVersion(Id) ?? Invalid;
  public XPNodeRef AtVersion(int version) => Doc?.AtVersion(Id, version) ?? Invalid;
  public XPNodeRef AtVersion(Index version) =>
    Doc?.AtVersion(Id, version.GetOffset(Doc.Version)) ?? Invalid;

  public XPNodeRef AddChild(
    XPType type, string name, string value = null,
    XPNodeRef? before = null, XPNodeRef? after = null
  ) => Doc?.AddChild(
      Canon.Id.Index, type, rawName: name, value: value,
      before: before?.Canon.Id.Index ?? -1, after: after?.Canon.Id.Index ?? -1
    ) ?? Invalid;

  public XPNodeRef AddChild(
    XPType type, XPName name, string value = null,
    XPNodeRef? before = null, XPNodeRef? after = null
  ) => Doc?.AddChild(
      Canon.Id.Index, type, prefixedName: name, value: value,
      before: before?.Canon.Id.Index ?? -1, after: after?.Canon.Id.Index ?? -1
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

  public void SetValue(string value) => Doc.SetValue(Id, value);

  public void Remove() => Doc.RemoveNode(Canon.Id.Index);

  public bool SameAs(XPNodeRef other) => Valid && Canon.Id.Index == other.Canon.Id.Index;

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
    if (!Doc.ResolveName(Id, rawName, out var name))
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
    Doc.ResolveName(Id, raw, out name);

  public bool ResolveName(string prefix, string local, out XPName name) =>
    Doc.ResolveName(Id, prefix, local, out name);

  public string DebugName
  {
    get
    {
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