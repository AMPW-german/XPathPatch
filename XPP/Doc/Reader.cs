
using System;
using System.Xml;

namespace XPP.Doc;

public class XPDocReader(XPNodeRef _node) : XmlReader
{
  private readonly NameTable nt = new();
  private State state = State.Init(_node);

  public override int AttributeCount
  {
    get
    {
      var attr = state.FirstAttr();
      var count = 0;
      while (attr.Valid)
      {
        count++;
        attr = attr.NextAttr();
      }
      return count;
    }
  }

  public override string BaseURI => nt.Add("");

  public override int Depth => state.Depth;

  public override bool EOF => state.Started && !state.Valid;

  public override bool IsEmptyElement =>
    state.Node.Type is XPType.Element && !state.End && !state.FirstContent().Valid;

  public override string LocalName => nt.Add(state.Node.Name.Local);

  public override string NamespaceURI => nt.Add(state.Node.Name.NsUri);

  public override XmlNameTable NameTable => nt; // TODO: should we use this in doc?

  public override XmlNodeType NodeType
  {
    get
    {
      return state.Node.Type switch
      {
        XPType.Invalid => XmlNodeType.None,
        XPType.Document => XmlNodeType.Document,
        XPType.Element => state.End ? XmlNodeType.EndElement : XmlNodeType.Element,
        XPType.Text => XmlNodeType.Text,
        XPType.CData => XmlNodeType.CDATA,
        XPType.ProcInst => XmlNodeType.ProcessingInstruction,
        XPType.Comment => XmlNodeType.Comment,
        { IsAttribute: true } when state.FakeAttrText => XmlNodeType.Text,
        XPType.Attribute => XmlNodeType.Attribute,
        XPType.Namespace => XmlNodeType.Attribute,
        _ => XmlNodeType.None,
      };
    }
  }

  public override string Prefix => nt.Add(state.Node.Name.Prefix);

  public override ReadState ReadState =>
    !state.Started
      ? ReadState.Initial
      : state.Valid
        ? ReadState.Interactive
        : ReadState.EndOfFile;

  public override string Value => state.Node.Value;

  public override string GetAttribute(int i)
  {
    var idx = i;
    var attr = state.FirstAttr();
    while (i > 0 && attr.Valid)
    {
      attr = attr.NextAttr();
      i--;
    }
    if (!attr.Valid)
      throw new IndexOutOfRangeException($"{idx}");
    var val = attr.Node.Value;
    return string.IsNullOrEmpty(val) ? null : val;
  }

  private State FindAttr(string name)
  {
    XPName.Parts(name, out var prefix, out var local);
    var attr = state.FirstAttr();
    var isNs = prefix.SequenceEqual(XPName.XMLNS_PREFIX);
    while (attr.Valid)
    {
      var aname = attr.Node.Name;
      if (isNs && attr.Node.Type is XPType.Namespace && local.SequenceEqual(aname.Local))
        return attr;
      if (!isNs && prefix.SequenceEqual(aname.Prefix) && local.SequenceEqual(aname.Local))
        return attr;
      attr = attr.NextAttr();
    }
    return State.Invalid;
  }

  private State FindAttr(string name, string namespaceURI)
  {
    var attr = state.FirstAttr();
    while (attr.Valid)
    {
      var aname = attr.Node.Name;
      if (aname.NsUri == namespaceURI && aname.Local == name)
        return attr;
      attr = attr.NextAttr();
    }
    return State.Invalid;
  }

  public override string GetAttribute(string name)
  {
    var val = FindAttr(name).Node.Value;
    return string.IsNullOrEmpty(val) ? null : val;
  }

  public override string GetAttribute(string name, string namespaceURI)
  {
    var val = FindAttr(name, namespaceURI).Node.Value;
    return string.IsNullOrEmpty(val) ? null : val;
  }

  public override string LookupNamespace(string prefix) => nt.Add(prefix switch
  {
    "" => prefix,
    XPName.XML_PREFIX => XPName.XML_URI,
    XPName.XMLNS_PREFIX => XPName.XMLNS_URI,
    _ when state.Node.ResolveName(prefix, "", out var name) => name.NsUri,
    _ => null,
  });

  private bool MoveIfValid(State next)
  {
    if (!next.Valid)
      return false;
    state = next;
    return true;
  }

  public override bool MoveToAttribute(string name) => MoveIfValid(FindAttr(name));
  public override bool MoveToAttribute(string name, string ns) =>
    MoveIfValid(FindAttr(name, ns));
  public override bool MoveToElement() => MoveIfValid(state.ToElement());
  public override bool MoveToFirstAttribute() => MoveIfValid(state.FirstAttr());
  public override bool MoveToNextAttribute() => MoveIfValid(state.NextAttr());
  public override bool Read() => MoveIfValid(state.Next());
  public override bool ReadAttributeValue() => MoveIfValid(state.AttrVal());
  public override void ResolveEntity() => throw new NotImplementedException();

  private struct State()
  {
    public static readonly State Invalid = new() { Started = true };

    public bool Started = false;
    public XPNodeRef Node = XPNodeRef.Invalid;
    public int AttrIndex = -1;
    public int Depth = -1;
    public bool End = false;
    public bool FakeAttrText = false;

    public readonly bool Valid => Node.Valid && Depth >= 0;

    public static State Init(XPNodeRef root) => new()
    {
      Started = false,
      Node = root.Type is XPType.Document ? root.FirstContent : root,
      AttrIndex = 0,
      Depth = 0,
      End = false,
    };

    private State With(
      bool? started = null,
      XPNodeRef? node = null,
      int? attrIndex = null,
      int? depth = null,
      bool? end = null,
      bool? fakeAttrText = null)
    {
      var copy = this;
      if (started.HasValue) copy.Started = started.Value;
      if (node.HasValue) copy.Node = node.Value;
      if (attrIndex.HasValue) copy.AttrIndex = attrIndex.Value;
      if (depth.HasValue) copy.Depth = depth.Value;
      if (end.HasValue) copy.End = end.Value;
      copy.FakeAttrText = fakeAttrText ?? false;
      return copy;
    }

    public State Next()
    {
      if (!Started)
        return With(started: true);
      if (!Valid)
        return Invalid;
      State next;
      var state = this;
      var type = Node.Type;
      if (type.IsAttribute)
        return ToElement().Next();
      if (type is XPType.Element && !state.End)
      {
        next = state.FirstContent();
        if (next.Valid)
          return next;
      }
      next = NextContent();
      if (next.Valid)
        return next;
      return Parent();
    }

    public State FirstAttr()
    {
      if (End || AttrIndex == -1 || !Valid)
        return Invalid;
      return With(started: true, node: Node.FirstAttr, depth: Depth + 1, attrIndex: 0);
    }

    public State NextAttr()
    {
      if (Node.Type.IsAttribute)
      {
        if (Depth <= 0)
          return Invalid;
        return With(started: true, node: Node.NextSibling, attrIndex: AttrIndex + 1);
      }
      return FirstAttr();
    }

    public State AttrVal()
    {
      if (FakeAttrText || !Node.Type.IsAttribute)
        return Invalid;
      return With(fakeAttrText: true);
    }

    public State ToElement()
    {
      if (!Node.Type.IsAttribute)
        return Invalid;
      return With(node: Node.Parent, attrIndex: -1, depth: Depth - 1);
    }

    public State FirstContent()
    {
      if (End)
        return Invalid;
      return With(started: true, node: Node.FirstContent, attrIndex: 0, depth: Depth + 1);
    }

    public State NextContent()
    {
      if (!Node.Type.IsContent || Depth <= 0)
        return Invalid;
      return With(node: Node.NextSibling, attrIndex: 0, end: false);
    }

    public State Parent()
    {
      if (!Node.Type.IsContent)
        return Invalid;
      return With(node: Node.Parent, attrIndex: -1, depth: Depth - 1, end: true);
    }

    public State ToEnd()
    {
      if (Node.Type is not XPType.Element)
        return Invalid;
      return With(started: true, attrIndex: -1, end: true);
    }
  }
}