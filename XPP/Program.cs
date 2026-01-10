
using System;
using System.Xml;
using System.Xml.XPath;
using XPP.Doc;
using XPP.Path;

namespace XPP;

public static class Program
{
  private const string Path = "//@id[.>0]/..";
  private const string XML = """
    <root>
      <a id="1">
        <b />
        text
      </a>
      <?proc stuff ?>
      <!-- comment -->
    </root>
    """;

  public static void Main(string[] args)
  {
    var doc = new XmlDocument();
    doc.LoadXml(XML);
    // doc.Load("C:/Program Files/Kitten Space Agency/Content/Core/DefaultAssets.xml");

    // TestXPath(doc);
    TestXPDoc(doc);
  }

  private static void TestXPDoc(XmlDocument doc)
  {
    var xpdoc = XPDocument.New();
    xpdoc.Import(doc);

    Console.WriteLine(xpdoc);
  }

  private static void TestXPath(XmlDocument doc)
  {
    var xpath = XPath.Parse(Path);
    DebugPrint(xpath);
    var exec = new Exec<NavAdapter>(xpath);

    var val = exec.Run(new(doc.DocumentElement.CreateNavigator()));

    var i = 0;
    while (exec.NextNode(val.Value.NodeSet, out var nav))
    {
      Console.WriteLine($"{i++} {nav.Node.NodeType}");
      Console.WriteLine($"  {nav.Node.OuterXml}");
    }
  }

  private static void DebugPrint(XPath xpath = null)
  {
    xpath ??= XPath.Parse("child::para[position()=5][attribute::type='warning']");
    Console.WriteLine($"PATH: {xpath.Source}");
    Console.WriteLine($"DATA: {new string(xpath.Data)}");
    for (var i = 0; i < xpath.Vals.Length; i++)
    {
      ref var val = ref xpath.Vals[i];
      Console.WriteLine($"V{i:00} {val.Type} {val.Left} {val.Right}");
    }
    for (var i = 0; i < xpath.Paths.Length; i++)
    {
      ref var path = ref xpath.Paths[i];
      Console.WriteLine($"P{i:00} {path.Type} {path.Paths} {path.Name}");
    }
  }
}

public struct NavAdapter(XPathNavigator node) : IXPathNav<NavAdapter>
{
  private readonly XPathNavigator node = node;

  public XmlNode Node => (XmlNode)node.UnderlyingObject;

  public NavAdapter Clone() => new(node.Clone());

  public NavAdapter Root()
  {
    var nav = Clone();
    nav.node.MoveToRoot();
    return nav;
  }

  public NodeType Type() => node.NodeType switch
  {
    XPathNodeType.Text or XPathNodeType.SignificantWhitespace or XPathNodeType.Whitespace => NodeType.Text,
    XPathNodeType.ProcessingInstruction => NodeType.ProcessingInstruction,
    XPathNodeType.Comment => NodeType.Comment,
    _ => NodeType.Node,
  };

  public bool IsAttribute() => node.NodeType == XPathNodeType.Attribute;

  public bool IsNs() => node.NodeType == XPathNodeType.Namespace;

  public bool HasNs(ReadOnlySpan<char> ns)
  {
    return ns.Equals(node.BaseURI, StringComparison.InvariantCulture);
  }

  public bool HasName(ReadOnlySpan<char> name)
  {
    return name.Equals(node.LocalName, StringComparison.InvariantCulture);
  }

  public int CompareTo(NavAdapter other) => node.ComparePosition(other.node) switch
  {
    XmlNodeOrder.Before => -1,
    XmlNodeOrder.Same => 0,
    XmlNodeOrder.After => 1,
    _ => throw new InvalidOperationException(),
  };

  public bool Parent(out NavAdapter nav)
  {
    nav = Clone();
    return nav.node.MoveToParent();
  }

  public bool FirstChild(out NavAdapter nav)
  {
    nav = Clone();
    return nav.node.MoveToFirstChild();
  }

  public bool LastChild(out NavAdapter nav)
  {
    nav = Clone();
    if (!nav.node.MoveToFirstChild())
      return false;
    while (nav.node.MoveToNext()) ;
    return true;
  }

  public bool NextSibling(out NavAdapter nav)
  {
    nav = Clone();
    return nav.node.MoveToNext();
  }

  public bool PreviousSibling(out NavAdapter nav)
  {
    nav = Clone();
    return nav.node.MoveToPrevious();
  }

  public bool FirstAttribute(out NavAdapter nav)
  {
    nav = Clone();
    return nav.node.MoveToFirstAttribute();
  }

  public bool NextAttribute(out NavAdapter nav)
  {
    nav = Clone();
    return nav.node.MoveToNextAttribute();
  }

  public bool FirstNamespace(out NavAdapter nav)
  {
    nav = Clone();
    return nav.node.MoveToFirstNamespace();
  }

  public bool NextNamespace(out NavAdapter nav)
  {
    nav = Clone();
    return nav.node.MoveToNextNamespace();
  }

  public int StringValue(Span<char> buffer) =>
    BuildStringValue(node.UnderlyingObject as XmlNode, buffer);

  // adapted from DocumentXPathNavigator.Value and XmlNode.InnerText
  private static int BuildStringValue(XmlNode node, Span<char> buffer)
  {
    if (node is XmlAttribute or XmlCharacterData)
    {
      var val = node.Value;
      val.AsSpan().CopyTo(buffer);
      return val.Length;
    }
    var child = node.FirstChild;
    var len = 0;
    while (child != null)
    {
      len += BuildStringValue(child, buffer[len..]);
      child = child.NextSibling;
    }
    return len;
  }
}