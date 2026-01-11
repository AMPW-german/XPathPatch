
using System;

namespace XPP.Doc;

public partial class XPDocument
{
  public const string XMLNS_PREFIX = "xmlns";
  public const string XMLNS_URI = "http://www.w3.org/2000/xmlns/";

  private readonly AppendList<Node> nodes = [];
  private readonly AppendList<int> roots = [];
  private int docVersion = 0;

  public int Version => docVersion;
  public int TotalNodes => nodes.Length;

  public static XPDocument New() => new();

  private XPDocument()
  {
    NewNode(XPType.Document, default, "");
    roots.Add(0);
  }

  public void NewVersion()
  {
    docVersion++;
    roots.Add(roots[^1]);
  }

  public bool ResolveName(int index, int version, string name, out XPName resolved)
  {
    XPName.Parts(name, out var prefix, out var local);
    if (prefix.Length == 0)
    {
      resolved = new("", "", name);
      return true;
    }
    if (prefix.SequenceEqual(XMLNS_PREFIX))
    {
      resolved = new(XMLNS_URI, XMLNS_PREFIX, new(local));
      return true;
    }

    return ResolveName(index, version, new(prefix), new(local), out resolved);
  }

  public bool ResolveName(int index, int version, string prefix, string local, out XPName resolved)
  {
    if (prefix == "")
    {
      resolved = new("", "", local);
      return true;
    }
    if (prefix == XMLNS_PREFIX)
    {
      resolved = new(XMLNS_URI, XMLNS_PREFIX, local);
      return true;
    }
    while (index != -1)
    {
      ref var node = ref nodes[index];
      var nsIdx = node.FirstAttr;
      while (nsIdx != -1)
      {
        ref var nsNode = ref Latest(nsIdx, version);
        if (nsNode.Type is XPType.Namespace && prefix == nsNode.Name.Local)
        {
          resolved = new(nsNode.Value, new(prefix), new(local));
          return true;
        }
        nsIdx = nsNode.NextSibling;
      }
      index = node.Parent;
    }

    resolved = new("", prefix, local);
    return false;
  }

  private void ValidateParentBeforeAfter(int parent, XPType childType, int before, int after)
  {
    if (before != -1 && after != -1)
      throw new InvalidOperationException($"cannot specify both before and after nodes");

    ref var pnode = ref Latest(parent);
    if (pnode.Removed)
      throw new InvalidOperationException($"parent has been removed");
    if (pnode.Index != parent)
      throw new InvalidOperationException($"parent is not latest version");

    if (!pnode.Type.CanHaveChild(childType))
      throw new InvalidOperationException($"{childType} cannot be child of {pnode.Type}");

    if (pnode.Type.HasSingleChild && FirstChild(ref pnode, childType) != -1)
      throw new InvalidOperationException($"parent {pnode.Type} can only have one child {childType}");

    if (before != -1)
    {
      ref var bnode = ref Latest(before);
      if (bnode.Removed)
        throw new InvalidOperationException($"before has been removed");
      if (bnode.Index != before)
        throw new InvalidOperationException($"before is not latest version");
      if (Latest(bnode.Parent).Index != parent)
        throw new InvalidOperationException($"before is not child of parent");
      if (!bnode.Type.SameChildTypeAs(childType))
        throw new InvalidOperationException(
          $"before {bnode.Type} is not same child type as {childType}");
    }
    if (after != -1)
    {
      ref var anode = ref Latest(after);
      if (anode.Removed)
        throw new InvalidOperationException($"after has been removed");
      if (anode.Index != after)
        throw new InvalidOperationException($"after is not latest version");
      if (Latest(anode.Parent).Index != parent)
        throw new InvalidOperationException($"after is not child of parent");
      if (!anode.Type.SameChildTypeAs(childType))
        throw new InvalidOperationException(
          $"after {anode.Type} is not same child type as {childType}");
    }
  }

  public XPNodeRef AddChild(
    int parent, XPType type,
    string rawName = null, XPName? prefixedName = null, string value = "",
    int before = -1, int after = -1)
  {
    value ??= "";

    ValidateParentBeforeAfter(parent, type, before, after);

    if (type.HasName && string.IsNullOrEmpty(rawName) && string.IsNullOrEmpty(prefixedName?.Local))
      throw new InvalidOperationException($"{type} node must have name");
    else if (!type.HasName && !string.IsNullOrEmpty(rawName))
      throw new InvalidOperationException($"{type} node must not have name");
    else if (!type.HasName && prefixedName is XPName pname && pname.Local != "")
      throw new InvalidOperationException($"{type} node must not have name");
    if (!type.HasValue && value != "")
      throw new InvalidOperationException($"{type} node must not have value");

    ref var pnode = ref nodes[parent];

    if (before == -1 && after == -1)
      after = LastChild(ref pnode, type);

    var (prev, next) = (-1, -1);
    if (before != -1)
      (prev, next) = (Latest(before).PrevSibling, before);
    if (after != -1)
      (prev, next) = (after, Latest(after).NextSibling);

    XPName name;
    if (type.HasName)
    {
      bool validName;
      if (prefixedName is XPName parsed)
        validName = ResolveName(parent, docVersion, parsed.Prefix, parsed.Local, out name);
      else
        validName = ResolveName(parent, docVersion, rawName, out name);
      if (!validName)
        throw new InvalidOperationException($"unknown prefix '{name.Prefix}'");

      if (type is XPType.Namespace && name.Prefix != XMLNS_PREFIX)
        throw new InvalidOperationException($"Namespace must have {XMLNS_PREFIX} prefix");
      if (type is XPType.Attribute && name.Prefix == XMLNS_PREFIX)
        throw new InvalidOperationException($"Attribute must not have prefix {XMLNS_PREFIX}");
    }
    else
      name = new("", "", "");

    if (type.DistinctName)
    {
      var sibIdx = FirstChild(ref pnode, type);
      while (sibIdx != -1)
      {
        ref var sibling = ref nodes[sibIdx];
        if (sibling.Name == name)
          throw new InvalidOperationException($"{type} node must have distinct name in parent");
        sibIdx = sibling.NextSibling;
      }
    }
    if (type.SiblingsMerge)
    {
      if (prev != -1)
      {
        ref var prevNode = ref Latest(prev);
        if (prevNode.Type == type)
        {
          prevNode = ref Current(prev);
          prevNode.Value += value;
          return new(this, docVersion, prevNode.Index);
        }
      }
      if (next != -1)
      {
        ref var nextNode = ref Latest(next);
        if (nextNode.Type == type)
        {
          nextNode = ref Current(next);
          nextNode.Value = value + nextNode.Value;
          return new(this, docVersion, nextNode.Index);
        }
      }
    }

    // if we are first or last, we need a current parent
    if (prev == -1 || next == -1)
    {
      pnode = ref Current(parent);
      parent = pnode.Index;
    }

    ref var node = ref NewNode(type, name, value);
    node.Parent = parent;

    if (prev == -1)
      FirstChild(ref pnode, type) = node.Index;
    else
    {
      ref var prevNode = ref Current(prev);
      prevNode.NextSibling = node.Index;
      node.PrevSibling = prevNode.Index;
    }

    if (next == -1)
      LastChild(ref pnode, type) = node.Index;
    else
    {
      ref var nextNode = ref Current(next);
      nextNode.PrevSibling = node.Index;
      node.NextSibling = nextNode.Index;
    }

    return new(this, docVersion, node.Index);
  }

  public void RemoveNode(int index)
  {
    ref var node = ref nodes[index];
    if (node.VNext != -1)
      throw new InvalidOperationException($"node is not latest version");
    if (node.Removed)
      throw new InvalidOperationException($"node has already been removed");
    if (node.Parent == -1)
      throw new InvalidOperationException($"cannot remove root document node");
    TombstoneTree(index);

    node = ref Latest(index);
    if (node.Parent != -1 && (node.PrevSibling == -1 || node.NextSibling == -1))
    {
      ref var pnode = ref Current(node.Parent);
      if (node.Type.IsAttribute)
      {
        if (node.PrevSibling == -1)
          pnode.FirstAttr = node.NextSibling;
        if (node.NextSibling == -1)
          pnode.LastAttr = node.PrevSibling;
      }
      else if (node.Type.IsContent)
      {
        if (node.PrevSibling == -1)
          pnode.FirstContent = node.NextSibling;
        if (node.NextSibling == -1)
          pnode.LastContent = node.PrevSibling;
      }
    }
    if (node.PrevSibling != -1)
    {
      ref var prev = ref Current(node.PrevSibling);
      prev.NextSibling = node.NextSibling;
    }
    if (node.NextSibling != -1)
    {
      ref var next = ref Current(node.NextSibling);
      next.PrevSibling = node.PrevSibling;
    }
  }

  private void TombstoneTree(int index, bool siblings = false)
  {
    if (index == -1)
      return;
    do
    {
      ref var node = ref Current(index);
      node.Removed = true;
      TombstoneTree(node.FirstAttr, true);
      TombstoneTree(node.FirstContent, true);
      index = node.NextSibling;
    } while (siblings && index != -1);
  }

  private ref Node Latest(int index, int maxVersion = int.MaxValue)
  {
    ref var node = ref nodes[index];
    while (node.DVersion < maxVersion && node.VNext != -1)
    {
      ref var next = ref nodes[node.VNext];
      if (next.DVersion > maxVersion)
        break;
      node = ref next;
    }
    while (node.DVersion > maxVersion && node.VPrev != -1)
      node = ref nodes[node.VPrev];
    return ref node;
  }

  private ref Node Current(int index)
  {
    ref var node = ref Latest(index);
    if (node.DVersion == docVersion)
      return ref node;

    // if the latest isn't on the current version, make a new node
    index = nodes.Add(ref node);
    ref var newNode = ref nodes[index];
    newNode.Index = node.VNext = index;
    newNode.VPrev = node.Index;
    newNode.VNext = -1;
    newNode.DVersion = docVersion;

    if (node.Index == roots[docVersion])
      roots[docVersion] = index;

    return ref newNode;
  }

  private ref Node NewNode(XPType type, XPName name, string value)
  {
    var idx = nodes.Add(new()
    {
      Type = type,
      Name = name,
      Value = value,

      Parent = -1,
      FirstContent = -1,
      LastContent = -1,
      FirstAttr = -1,
      LastAttr = -1,
      PrevSibling = -1,
      NextSibling = -1,

      DVersion = docVersion,

      VPrev = -1,
      VNext = -1,

      Removed = false,
    });
    ref var node = ref nodes[idx];
    node.Index = idx;
    return ref node;
  }

  private static ref int FirstChild(ref Node node, XPType child)
  {
    if (child.IsAttribute)
      return ref node.FirstAttr;
    if (child.IsContent)
      return ref node.FirstContent;
    throw new InvalidOperationException($"{child}");
  }

  private static ref int LastChild(ref Node node, XPType child)
  {
    if (child.IsAttribute)
      return ref node.LastAttr;
    if (child.IsContent)
      return ref node.LastContent;
    throw new InvalidOperationException($"{child}");
  }

  // TODO: document order
  private struct Node
  {
    // basic info
    public int Index; // self index in node list
    public XPType Type;
    public XPName Name; // index in name store
    public string Value;

    // link info
    public int Parent;
    public int FirstContent;
    public int LastContent;
    public int FirstAttr;
    public int LastAttr;
    public int PrevSibling;
    public int NextSibling;

    // version info
    public int DVersion; // document version
    // node list indices of versions of this node
    public int VPrev;
    public int VNext;

    // remove flag
    public bool Removed;
  }
}

public readonly struct XPName(string NsUri, string Prefix, string Local) : IEquatable<XPName>
{
  public readonly string NsUri = NsUri;
  public readonly string Prefix = Prefix;
  public readonly string Local = Local;

  public bool Equals(XPName other) => NsUri == other.NsUri && Local == other.Local;
  public override bool Equals(object obj) => obj is XPName other && this == other;
  public override int GetHashCode() => HashCode.Combine(NsUri, Local);
  public static bool operator ==(XPName left, XPName right) => left.Equals(right);
  public static bool operator !=(XPName left, XPName right) => !(left == right);

  public static void Parts(
    string raw, out ReadOnlySpan<char> prefix, out ReadOnlySpan<char> local)
  {
    var split = raw.IndexOf(':');
    if (split == -1)
    {
      prefix = [];
      local = raw;
    }
    else
    {
      prefix = raw.AsSpan(0, split);
      local = raw.AsSpan(split + 1);
    }
  }
}

public readonly partial struct XPNodeRef(XPDocument Doc, int DocVersion, int Index)
{
  public static readonly XPNodeRef Invalid = new(null, 0, -1);

  public readonly XPDocument Doc = Doc;
  public readonly int DocVersion = DocVersion;
  public readonly int Index = Index;
  public bool Valid => Index >= 0 && Doc != null;
}