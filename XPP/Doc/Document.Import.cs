
using System;
using System.Xml;

namespace XPP.Doc;

public partial class XPDocument
{
  internal XPNodeRef Import(XmlNode node, Node parent)
  {
    switch (node.NodeType)
    {
      case XmlNodeType.Element when node is XmlElement el:
        return ImportElement(el, parent);
      case XmlNodeType.Attribute when node is XmlAttribute attr:
        return AddChild(parent,
          PrefixedName(node).IsNamespace ? XPType.Namespace : XPType.Attribute,
          prefixedName: PrefixedName(node), value: attr.Value);
      case XmlNodeType.Text:
        return AddChild(parent, XPType.Text, value: node.Value);
      case XmlNodeType.CDATA:
        return AddChild(parent, XPType.CData, value: node.Value);
      case XmlNodeType.ProcessingInstruction:
        return AddChild(
          parent, XPType.ProcInst, prefixedName: PrefixedName(node), value: node.Value);
      case XmlNodeType.Comment:
        return AddChild(parent, XPType.Comment, value: node.Value);
      case XmlNodeType.Document when node is XmlDocument doc:
        // just import the root element
        return Import(doc.DocumentElement, parent);
      default:
        throw new NotSupportedException($"{node.NodeType}");
    }
  }

  private XPNodeRef ImportElement(XmlElement el, Node parent)
  {
    var elRef = AddChild(parent, XPType.Element, prefixedName: PrefixedName(el));
    var attrs = el.Attributes;
    for (var i = 0; i < attrs.Count; i++)
      Import(attrs[i], elRef.Node);
    var child = el.FirstChild;
    while (child != null)
    {
      Import(child, elRef.Node);
      child = child.NextSibling;
    }
    return elRef;
  }

  private static XPName PrefixedName(XmlNode node) =>
    new("", node.Prefix, node.LocalName);

  internal XPNodeRef Import(XmlReader reader, Node parent, bool interior = false)
  {
    if (reader.Settings?.IgnoreWhitespace != true)
    {
      var settings = reader.Settings?.Clone() ?? new();
      settings.IgnoreWhitespace = true;
      reader = XmlReader.Create(reader, settings);
    }
    while (reader.Read())
    {
      if (interior || reader.NodeType == XmlNodeType.Element)
        return ImportInternal(reader, parent);
    }
    throw new InvalidOperationException($"Unexpected EOF");
  }

  private XPNodeRef ImportInternal(XmlReader reader, Node parent)
  {
    switch (reader.NodeType)
    {
      case XmlNodeType.Element:
        return ImportElement(reader, parent);
      case XmlNodeType.Attribute:
        return AddChild(parent,
          PrefixedName(reader).IsNamespace ? XPType.Namespace : XPType.Attribute,
          prefixedName: PrefixedName(reader), value: reader.Value);
      case XmlNodeType.Text:
        return AddChild(parent, XPType.Text, value: reader.Value);
      case XmlNodeType.CDATA:
        return AddChild(parent, XPType.CData, value: reader.Value);
      case XmlNodeType.ProcessingInstruction:
        return AddChild(
          parent, XPType.CData, prefixedName: PrefixedName(reader), value: reader.Value);
      case XmlNodeType.Comment:
        return AddChild(parent, XPType.Comment, value: reader.Value);
      case XmlNodeType.EndElement:
        // shouldn't get End element here
        throw new InvalidOperationException();
      case XmlNodeType.Whitespace:
        return XPNodeRef.Invalid;
      case XmlNodeType.XmlDeclaration:
      default:
        throw new NotImplementedException($"{reader.NodeType}");
    }
  }

  private XPNodeRef ImportElement(XmlReader reader, Node parent)
  {
    var el = AddChild(parent, XPType.Element, prefixedName: PrefixedName(reader));
    if (reader.MoveToFirstAttribute())
    {
      do
      {
        ImportInternal(reader, el.Node);
      } while (reader.MoveToNextAttribute());
      reader.MoveToElement();
    }
    if (reader.IsEmptyElement)
      return el;
    while (reader.Read())
    {
      if (reader.NodeType == XmlNodeType.EndElement)
        return el;
      ImportInternal(reader, el.Node);
    }
    throw new InvalidOperationException($"unexpected EOF");
  }

  private static XPName PrefixedName(XmlReader reader) =>
    new("", reader.Prefix, reader.LocalName);

  internal XPNodeRef Import(
    XPNodeRef nref, Node parent, Node before = null, Node after = null)
  {
    if (!nref.Valid)
      throw new InvalidOperationException($"invalid node");
    if (nref.Doc == this && nref.Version == docVersion && nref.Type.CanHaveContent)
    {
      // if copying a node that can have children from the live version of this doc,
      // check that we aren't copying into a child node
      var node = nref.Latest;
      parent = Latest(parent);
      while (true)
      {
        if (parent == node)
          throw new InvalidOperationException("cannot copy a parent into its child");
        if (parent.Parent == null)
          break;
        parent = Latest(parent.Parent);
      }
    }
    return ImportInternal(nref, parent, false, before, after);
  }

  private XPNodeRef ImportInternal(
    XPNodeRef node, Node parent, bool siblings, Node before = null, Node after = null)
  {
    var inode = XPNodeRef.Invalid;
    while (node.Valid)
    {
      inode = AddChild(
        parent, node.Type,
        prefixedName: node.Name, value: node.Value,
        before: before, after: after
      );
      ImportInternal(node.FirstAttr, inode.Node, true);
      ImportInternal(node.FirstContent, inode.Node, true);
      if (!siblings)
        return inode;
      node = node.NextSibling;
    }
    return inode;
  }
}

public partial struct XPNodeRef
{
  public XPNodeRef Import(XmlNode node)
  {
    if (Version != Doc.Version)
      throw new InvalidOperationException($"Cannot import to previous version");
    return Doc.Import(node, Latest);
  }

  public XPNodeRef Import(XmlReader reader, bool interior = false)
  {
    if (Version != Doc.Version)
      throw new InvalidOperationException($"Cannot import to previous version");
    return Doc.Import(reader, Latest, interior);
  }

  public XPNodeRef Import(
    XPNodeRef node, XPNodeRef? before = null, XPNodeRef? after = null)
  {
    if (Version != Doc.Version)
      throw new InvalidOperationException($"Cannot import to previous version");
    if (before is XPNodeRef beforeNode && beforeNode.Version != Doc.Version)
      throw new InvalidOperationException($"Cannot import before previous version");
    if (after is XPNodeRef afterNode && afterNode.Version != Doc.Version)
      throw new InvalidOperationException($"Cannot import after previous version");
    return Doc.Import(node, Latest, before?.Latest, after?.Latest);
  }
}
