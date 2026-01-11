
using System;
using System.Xml;

namespace XPP.Doc;

public partial class XPDocument
{
  public XPNodeRef Import(XmlNode node, int parent = 0)
  {
    switch (node.NodeType)
    {
      case XmlNodeType.Element when node is XmlElement el:
        return ImportElement(el, parent);
      case XmlNodeType.Attribute when node is XmlAttribute attr:
        return AddChild(parent, attr.Prefix == XMLNS_PREFIX ? XPType.Namespace : XPType.Attribute,
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

  private XPNodeRef ImportElement(XmlElement el, int parent)
  {
    var elRef = AddChild(parent, XPType.Element, prefixedName: PrefixedName(el));
    var attrs = el.Attributes;
    for (var i = 0; i < attrs.Count; i++)
      Import(attrs[i], elRef.Index);
    var child = el.FirstChild;
    while (child != null)
    {
      Import(child, elRef.Index);
      child = child.NextSibling;
    }
    return elRef;
  }

  private static XPName PrefixedName(XmlNode node) =>
    new("", node.Prefix, node.LocalName);

  public XPNodeRef Import(XmlReader reader, int parent = 0, bool interior = false)
  {
    if (reader.Settings?.IgnoreWhitespace != true)
      reader = XmlReader.Create(reader, new() { IgnoreWhitespace = true });
    while (reader.Read())
    {
      if (interior || reader.NodeType == XmlNodeType.Element)
        return ImportInternal(reader, parent);
    }
    throw new InvalidOperationException($"Unexpected EOF");
  }

  private XPNodeRef ImportInternal(XmlReader reader, int parent)
  {
    switch (reader.NodeType)
    {
      case XmlNodeType.Element:
        return ImportElement(reader, parent);
      case XmlNodeType.Attribute:
        return AddChild(parent,
          reader.Prefix == XMLNS_PREFIX ? XPType.Namespace : XPType.Attribute,
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

  private XPNodeRef ImportElement(XmlReader reader, int parent)
  {
    var el = AddChild(parent, XPType.Element, prefixedName: PrefixedName(reader));
    if (reader.MoveToFirstAttribute())
    {
      do
      {
        ImportInternal(reader, el.Index);
      } while (reader.MoveToNextAttribute());
      reader.MoveToElement();
    }
    if (reader.IsEmptyElement)
      return el;
    while (reader.Read())
    {
      if (reader.NodeType == XmlNodeType.EndElement)
        return el;
      ImportInternal(reader, el.Index);
    }
    throw new InvalidOperationException($"unexpected EOF");
  }

  private static XPName PrefixedName(XmlReader reader) =>
    new("", reader.Prefix, reader.LocalName);

  public XPNodeRef Import(XPNodeRef node, int parent = 0)
  {
    if (!node.Valid)
      throw new InvalidOperationException($"invalid node");
    if (node.Doc == this && node.DocVersion == docVersion && node.Type.CanHaveContent)
    {
      // if copying a node that can have children from the live version of this doc,
      // check that we aren't copying into a child node
      var nindex = node.Canon.Index;
      ref var pnode = ref Latest(parent);
      while (true)
      {
        if (pnode.Index == nindex)
          throw new InvalidOperationException("cannot copy a parent into its child");
        if (pnode.Parent == -1)
          break;
        pnode = ref Latest(pnode.Parent);
      }
    }
    return ImportInternal(node, parent, false);
  }

  private XPNodeRef ImportInternal(XPNodeRef node, int parent, bool siblings)
  {
    var inode = XPNodeRef.Invalid;
    while (node.Valid)
    {
      inode = AddChild(parent, node.Type, prefixedName: node.Name, value: node.Value);
      ImportInternal(node.FirstAttr, inode.Index, true);
      ImportInternal(node.FirstContent, inode.Index, true);
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
    if (DocVersion != Doc.Version)
      throw new InvalidOperationException($"Cannot import to previous version");
    return Doc.Import(node, Canon.Index);
  }

  public XPNodeRef Import(XmlReader reader, bool interior = false)
  {
    if (DocVersion != Doc.Version)
      throw new InvalidOperationException($"Cannot import to previous version");
    return Doc.Import(reader, Canon.Index, interior);
  }

  public XPNodeRef Import(XPNodeRef node)
  {
    if (DocVersion != Doc.Version)
      throw new InvalidOperationException($"Cannot import to previous version");
    return Doc.Import(node, Canon.Index);
  }
}
