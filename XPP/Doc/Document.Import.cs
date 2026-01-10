
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
        if (attr.Prefix != "xmlns")
          return AddChild(parent, XPType.Attribute,
            prefixedName: PrefixedName(node), value: attr.Value);
        return AddChild(parent, XPType.Namespace,
          rawName: attr.LocalName, value: attr.Value);
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
    var children = el.ChildNodes;
    for (var i = 0; i < children.Count; i++)
      Import(children[i], elRef.Index);
    return elRef;
  }

  private static XPName PrefixedName(XmlNode node) =>
    new("", node.Prefix, node.LocalName);
}