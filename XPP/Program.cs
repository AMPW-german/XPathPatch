
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using XPP.Doc;
using XPP.Path;

namespace XPP;

public static class Program
{
  private const string Path = "//@id[.>0]/..";
  private const string XML = """
    <root xmlns:x="http://example.ns">
      <a x:id="1">
        <x:b />
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

    // TestXPath(new NavAdapter(doc.DocumentElement.CreateNavigator()));
    // TestXPDoc(doc);
    // TestXPDocRead(doc);
    SpeedTest("C://Program Files (x86)/Steam/steamapps/common/Stationeers/rocketstation_Data/StreamingAssets");
  }

  private static void TestXPDoc(XmlDocument doc)
  {
    var xpdoc = XPDocument.New();
    xpdoc.Import(doc);

    xpdoc.NewVersion();
    var parent = xpdoc.LatestRoot.FirstContent.FirstContent;
    xpdoc.AddChild(
      parent.Index,
      XPType.Attribute,
      rawName: "name",
      value: "value"
    );
    Console.WriteLine(xpdoc.ToString());
    Console.WriteLine(xpdoc.ToString(0));

    // xpdoc.DebugDump();

    const string path = "(//@* | //namespace::*)";
    TestXPath(xpdoc.Root(0).Nav, path);
    TestXPath(xpdoc.Root(1).Nav, path);
  }

  private static void TestXPDocRead(XmlDocument doc)
  {
    var xpdoc = XPDocument.New();
    xpdoc.Import(doc);

    Console.WriteLine($"DOC");
    PrintReader(new XmlNodeReader(doc.DocumentElement));

    Console.WriteLine($"XPDOC");
    PrintReader(new DocReader(xpdoc.LatestRoot));
  }

  private static void PrintReader(XmlReader r)
  {
    while (r.Read())
    {
      Console.WriteLine($"- {r.NodeType} {r.Name} {r.Value}");
      while (r.MoveToNextAttribute())
      {
        Console.WriteLine($"- {r.NodeType} {r.Name} {r.Value}");
      }
    }
  }

  private static void SpeedTest(string basePath)
  {
    var stopwatch = Stopwatch.StartNew();
    var docs = new List<XmlDocument>();
    foreach (var file in Directory.EnumerateFiles(basePath, "*.xml", SearchOption.AllDirectories))
    {
      if (file.Contains("\\Language\\"))
        continue;
      var doc = new XmlDocument();
      doc.Load(file);
      docs.Add(doc);
    }
    stopwatch.Stop();
    Console.WriteLine($"loaded {docs.Count} files in {stopwatch.Elapsed.TotalMilliseconds:0.##}ms");

    stopwatch.Restart();
    const int ITER_COUNT = 100;
    var total = 0;
    for (var i = 0; i < ITER_COUNT; i++)
    {
      var xpdoc = XPDocument.New();
      var root = xpdoc.AddChild(0, XPType.Element, "Root");
      foreach (var doc in docs)
      {
        xpdoc.NewVersion();
        xpdoc.Import(doc, root.LatestVersion.Index);
      }
      total += xpdoc.TotalNodes;
    }
    stopwatch.Stop();
    Console.WriteLine($"imported {docs.Count*ITER_COUNT} docs with {total} nodes in {stopwatch.Elapsed.TotalMilliseconds:0.##}ms");
  }

  private static void TestXPath<Nav>(Nav nav, string path = null) where Nav : IXPathNav<Nav>
  {
    var xpath = XPath.Parse(path ?? Path);
    DebugPrint(xpath);
    var exec = new Exec<Nav>(xpath);

    var val = exec.Run(nav);

    var i = 0;
    while (exec.NextNode(val.Value.NodeSet, out nav))
    {
      Console.WriteLine($"{i++} {nav.Type()} attr:{nav.IsAttribute()} ns:{nav.IsNs()}");
      Console.WriteLine($"  {nav.OuterXml()}");
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

  public string OuterXml() => Node.OuterXml;
}