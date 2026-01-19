
using System;
using XPP.Utils;

namespace XPP.Doc;

public partial class XPDocument
{
  public const string XMLNS_PREFIX = "xmlns";
  public const string XMLNS_URI = "http://www.w3.org/2000/xmlns/";

  private readonly AppendList<Node> roots = [];
  public readonly OrderTree otree = new();
  private int docVersion = 0;

  public int Version => docVersion;

  public static XPDocument New() => new();

  private XPDocument()
  {
    var root = NewNode(XPType.Document, default, "", 0);
    root.Order = otree.Root;
    roots.Add(root);
  }

  public void NewVersion()
  {
    docVersion++;
    roots.Add(roots[^1]);
  }

  internal static bool ResolveName(VNode vnode, string name, out XPName resolved)
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

    return ResolveName(vnode, new(prefix), new(local), out resolved);
  }

  internal static bool ResolveName(VNode vnode, string prefix, string local, out XPName resolved)
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
    var node = vnode.Node;
    var version = vnode.Version;
    while (node != null)
    {
      node = Latest(node, version);
      var ns = node.FirstAttr;
      while (ns != null)
      {
        ns = Latest(ns, version);
        if (ns.Type is XPType.Namespace && prefix == ns.Name.Local)
        {
          resolved = new(ns.Value, new(prefix), new(local));
          return true;
        }
        ns = ns.NextSibling;
      }
      node = node.Parent;
    }

    resolved = new("", prefix, local);
    return false;
  }

  private void ValidateParentBeforeAfter(Node parent, XPType childType, Node before, Node after)
  {
    if (before != null && after != null)
      throw new InvalidOperationException($"cannot specify both before and after nodes");

    if (parent.VNext != null)
      throw new InvalidOperationException($"parent is not latest version");
    if (parent.Removed)
      throw new InvalidOperationException($"parent has been removed");

    if (!parent.Type.CanHaveChild(childType))
      throw new InvalidOperationException($"{childType} cannot be child of {parent.Type}");

    if (parent.Type.HasSingleChild && FirstChild(parent, childType) != null)
      throw new InvalidOperationException($"parent {parent.Type} can only have one child {childType}");

    if (before != null)
    {
      if (before.VNext != null)
        throw new InvalidOperationException($"before is not latest version");
      if (before.Removed)
        throw new InvalidOperationException($"before has been removed");
      if (before.Parent != parent)
        throw new InvalidOperationException($"before is not child of parent");
      if (!before.Type.SameChildTypeAs(childType))
        throw new InvalidOperationException(
          $"before {before.Type} is not same child type as {childType}");
    }
    if (after != null)
    {
      if (after.VNext != null)
        throw new InvalidOperationException($"after is not latest version");
      if (after.Removed)
        throw new InvalidOperationException($"after has been removed");
      if (after.Parent != parent)
        throw new InvalidOperationException($"after is not child of parent");
      if (!after.Type.SameChildTypeAs(childType))
        throw new InvalidOperationException(
          $"after {after.Type} is not same child type as {childType}");
    }
  }

  internal XPNodeRef AddChild(
    Node parent, XPType type,
    string rawName = null, XPName? prefixedName = null, string value = "",
    Node before = null, Node after = null)
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

    if (before == null && after == null)
      after = LastChild(parent, type);

    Node prev = after, next = before;

    if (prev != null)
      next = Latest(prev.NextSibling);
    else if (next != null)
      prev = Latest(next.PrevSibling);

    XPName name;
    if (type.HasName)
    {
      bool validName;
      if (prefixedName is XPName parsed)
        validName = ResolveName(new(parent, docVersion), parsed.Prefix, parsed.Local, out name);
      else
        validName = ResolveName(new(parent, docVersion), rawName, out name);
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
      var sibling = FirstChild(parent, type);
      while (sibling != null)
      {
        if (sibling.Name == name)
          throw new InvalidOperationException($"{type} node must have distinct name in parent");
        sibling = Latest(sibling.NextSibling);
      }
    }
    if (type.SiblingsMerge)
    {
      if (prev != null && prev.Type == type)
      {
        prev = Current(prev);
        prev.Value += value;
        return new(this, prev, docVersion);
      }
      if (next != null && next.Type == type)
      {
        next = Current(next);
        next.Value = value + next.Value;
        return new(this, next, docVersion);
      }
    }

    // if we are first or last, we need a current parent
    if (prev == null || next == null)
      parent = Current(parent);

    var node = NewNode(type, name, value, parent.Depth + 1);
    node.Parent = parent;

    if (prev == null)
      FirstChild(parent, type) = node;
    else
    {
      prev = Current(prev);
      prev.NextSibling = node;
      node.PrevSibling = prev;
      (prev.Order, node.Order) = otree.Split(prev.Order);
    }

    if (next == null)
      LastChild(parent, type) = node;
    else
    {
      next = Current(next);
      next.PrevSibling = node;
      node.NextSibling = next;
      if (prev == null)
        (node.Order, next.Order) = otree.Split(next.Order);
    }

    if (prev == null && next == null)
      node.Order = otree.Root;

    return new(this, node, docVersion);
  }

  internal void RemoveNode(Node node)
  {
    if (node.VNext != null)
      throw new InvalidOperationException($"node is not latest version");
    if (node.Removed)
      throw new InvalidOperationException($"node has already been removed");
    if (node.Parent == null)
      throw new InvalidOperationException($"cannot remove root document node");
    TombstoneTree(node);

    if (node.Parent != null && (node.PrevSibling == null || node.NextSibling == null))
    {
      var parent = Current(node.Parent);
      if (node.Type.IsAttribute)
      {
        if (node.PrevSibling == null)
          parent.FirstAttr = node.NextSibling;
        if (node.NextSibling == null)
          parent.LastAttr = node.PrevSibling;
      }
      else if (node.Type.IsContent)
      {
        if (node.PrevSibling == null)
          parent.FirstContent = node.NextSibling;
        if (node.NextSibling == null)
          parent.LastContent = node.PrevSibling;
      }
    }
    if (node.PrevSibling != null)
      Current(node.PrevSibling).NextSibling = node.NextSibling;
    if (node.NextSibling != null)
      Current(node.NextSibling).PrevSibling = node.PrevSibling;
  }

  private void TombstoneTree(Node node, bool siblings = false)
  {
    if (node == null)
      return;
    do
    {
      node = Current(node);
      node.Removed = true;
      TombstoneTree(node.FirstAttr, true);
      TombstoneTree(node.FirstContent, true);
      node = node.NextSibling;
    } while (siblings && node != null);
  }

  internal static Node Latest(VNode nref) => Latest(nref.Node, nref.Version);
  internal static Node Latest(Node node, int maxVersion = int.MaxValue)
  {
    if (node == null)
      return null;
    if (node.DVersion == maxVersion)
      return node;
    while (node.DVersion < maxVersion && node.VNext is Node next)
    {
      if (next.DVersion > maxVersion)
        break;
      node = next;
    }
    while (node.DVersion > maxVersion && node.VPrev is Node prev)
      node = prev;
    return node;
  }

  private Node Current(Node node)
  {
    node = Latest(node);
    if (node.DVersion == docVersion)
      return node;

    // if the latest isn't on the current version, make a new node
    var newNode = new Node(node);
    newNode.VPrev = node;
    newNode.VNext = null;
    newNode.DVersion = docVersion;
    node.VNext = newNode;

    if (newNode.Depth == 0)
      roots[docVersion] = newNode;

    return newNode;
  }

  private Node NewNode(XPType type, XPName name, string value, int depth)
  {
    return new()
    {
      Type = type,
      Name = name,
      Value = value,
      Depth = depth,
      Order = OrderTree.Key.Invalid,

      Parent = null,
      FirstContent = null,
      LastContent = null,
      FirstAttr = null,
      LastAttr = null,
      PrevSibling = null,
      NextSibling = null,

      DVersion = docVersion,

      VPrev = null,
      VNext = null,

      Removed = false,
    };
  }

  private static ref Node FirstChild(Node node, XPType child)
  {
    if (child.IsAttribute)
      return ref node.FirstAttr;
    if (child.IsContent)
      return ref node.FirstContent;
    throw new InvalidOperationException($"{child}");
  }

  private static ref Node LastChild(Node node, XPType child)
  {
    if (child.IsAttribute)
      return ref node.LastAttr;
    if (child.IsContent)
      return ref node.LastContent;
    throw new InvalidOperationException($"{child}");
  }

  internal class Node
  {
    // basic info
    public XPType Type;
    public XPName Name;
    public string Value;
    public int Depth;

    // ordering ranges
    public OrderTree.Key Order;

    // link info
    public Node Parent;
    public Node FirstContent;
    public Node LastContent;
    public Node FirstAttr;
    public Node LastAttr;
    public Node PrevSibling;
    public Node NextSibling;

    // version info
    public int DVersion; // document version
    // node list indices of versions of this node
    public Node VPrev;
    public Node VNext;

    // remove flag
    public bool Removed;

    public Node() { }

    public Node(Node other)
    {
      Type = other.Type;
      Name = other.Name;
      Value = other.Value;
      Depth = other.Depth;
      Order = other.Order;
      Parent = other.Parent;
      FirstContent = other.FirstContent;
      LastContent = other.LastContent;
      FirstAttr = other.FirstAttr;
      LastAttr = other.LastAttr;
      PrevSibling = other.PrevSibling;
      NextSibling = other.NextSibling;
      DVersion = other.DVersion;
      VPrev = other.VPrev;
      VNext = other.VNext;
      Removed = other.Removed;
    }
  }

  internal readonly struct VNode(Node Node, int Version)
  {
    public readonly Node Node = Node;
    public readonly int Version = Version;
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

public readonly partial struct XPNodeRef
{
  public static readonly XPNodeRef Invalid = new(null, null, -1);

  internal XPNodeRef(XPDocument Doc, XPDocument.Node Node, int Version)
  {
    this.Doc = Doc;
    this.Node = Node;
    this.Version = Version;
  }

  public readonly XPDocument Doc;
  internal readonly XPDocument.Node Node;
  public readonly int Version;
  public bool Valid => Doc != null && Node != null;
}