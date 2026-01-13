
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using XPP.Doc;
using XPP.Patch;
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

  private const string TestFile = "C:\\Program Files\\Kitten Space Agency\\Content\\Core\\DefaultAssets.xml";
  private const string TestFolder = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Stationeers\\rocketstation_Data\\StreamingAssets";

  public static void Main(string[] args)
  {
    var doc = new XmlDocument();
    doc.LoadXml(XML);
    // doc.Load(TestFile);

    // TestXPath(new NavAdapter(doc.DocumentElement.CreateNavigator()));
    TestXPDoc(doc);
    // TestXPDocRead(doc);
    // SpeedTest(TestFolder);
    // TestReadCompare(TestFolder);
    // TestImportRead(TestFolder);
  }

  private static void TestXPDoc(XmlDocument doc)
  {
    var xpdoc = XPDocument.New();
    xpdoc.Import(doc);

    xpdoc.NewVersion();
    var parent = xpdoc.LatestRoot.FirstContent.FirstContent;
    // xpdoc.AddChild(
    //   parent.Index,
    //   XPType.Attribute,
    //   rawName: "name",
    //   value: "value"
    // );
    parent.SetAttribute("name", "value");
    xpdoc.NewVersion();
    parent = parent.LatestVersion;
    parent.SetAttribute("name", "value2");
    parent.AddAttribute("extra", "stuff");
    xpdoc.NewVersion();
    parent.LatestVersion.Parent.Import(parent);
    xpdoc.NewVersion();
    parent.LatestVersion.Attribute("name").Remove();
    xpdoc.NewVersion();
    var root = xpdoc.LatestRoot.FirstContent;
    root.Import(root.AtVersion(^1));

    for (var version = 0; version <= xpdoc.Version; version++)
      Console.WriteLine(xpdoc.ToString(version));

    // xpdoc.DebugDump();

    // const string path = "(//@* | //namespace::*)";
    // TestXPath(xpdoc.Root(0), path);
    // TestXPath(xpdoc.Root(1), path);
  }

  private static void TestXPDocRead(XmlDocument doc)
  {
    var xpdoc = XPDocument.New();
    xpdoc.Import(doc);

    Console.WriteLine($"DOC");
    PrintReader(new XmlNodeReader(doc.DocumentElement));

    Console.WriteLine($"XPDOC");
    PrintReader(new XPDocReader(xpdoc.LatestRoot));
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

  private static void TestReadCompare(string basePath)
  {
    foreach (var file in Directory.EnumerateFiles(basePath, "*.xml", SearchOption.AllDirectories))
    {
      try
      {
        var doc = new XmlDocument();
        doc.Load(file);
        var xpdoc = XPDocument.New();
        xpdoc.Import(doc);

        using var r0 = new XmlNodeReader(doc.DocumentElement);
        using var r1 = new XPDocReader(xpdoc.LatestRoot);

        CompareRead(file, r0, r1);
      }
      catch
      {
        var doc = new XmlDocument();
        doc.Load(file);
        TestXPDocRead(doc);
        throw;
      }
    }
  }

  private static void TestImportRead(string basePath)
  {
    foreach (var file in Directory.EnumerateFiles(basePath, "*.xml", SearchOption.AllDirectories))
    {
      try
      {
        var doc = new XmlDocument();
        doc.Load(file);
        var xpdoc = XPDocument.New();
        xpdoc.Import(XmlReader.Create(file));

        using var r0 = new XmlNodeReader(doc.DocumentElement);
        using var r1 = new XPDocReader(xpdoc.LatestRoot);

        CompareRead(file, r0, r1);
      }
      catch
      {
        var doc = new XmlDocument();
        doc.Load(file);
        TestXPDocRead(doc);
        throw;
      }
    }
  }

  private static void CompareRead(string file, XmlReader r0, XmlReader r1)
  {
    var doc = new XmlDocument();
    doc.Load(file);
    var xpdoc = XPDocument.New();
    xpdoc.Import(doc);

    var e0 = false;
    var e1 = false;

    static string norm(string str) => str.Replace("\r\n", "\n");

    while (true)
    {
      if (e0 && !e1)
      {
        if (!r1.Read() || r1.NodeType != XmlNodeType.EndElement)
          throw new InvalidOperationException($"empty mismatch {e0}/{e1} in {file}");
      }
      else if (!e0 && e1)
      {
        if (!r0.Read() || r0.NodeType != XmlNodeType.EndElement)
          throw new InvalidOperationException($"empty mismatch {e0}/{e1} in {file}");
      }

      var n0 = r0.Read();
      var n1 = r1.Read();
      if (n0 != n1)
        throw new InvalidOperationException($"Read mismatch {n0}/{n1} in {file}");

      if (!n0)
        break;

      e0 = r0.IsEmptyElement;
      e1 = r1.IsEmptyElement;

      var inf0 = (r0.NodeType, r0.NamespaceURI, r0.Prefix, r0.LocalName, norm(r0.Value));
      var inf1 = (r1.NodeType, r1.NamespaceURI, r1.Prefix, r1.LocalName, norm(r1.Value));

      AssertEqual("Node", file, inf0, inf1);

      while (true)
      {
        var a0 = r0.MoveToNextAttribute();
        var a1 = r1.MoveToNextAttribute();

        if (a0 != a1)
          throw new InvalidOperationException($"NextAttr mismatch {a0}/{a1} in {file}");
        if (!a0)
          break;

        var ainf0 = (r0.NodeType, r0.NamespaceURI, r0.Prefix, r0.LocalName, norm(r0.Value));
        var ainf1 = (r1.NodeType, r1.NamespaceURI, r1.Prefix, r1.LocalName, norm(r1.Value));

        AssertEqual("Attr", file, ainf0, ainf1);
      }
    }
  }

  private static void AssertEqual<T1, T2, T3, T4, T5>(string type, string file, (T1, T2, T3, T4, T5) v1, (T1, T2, T3, T4, T5) v2)
  {
    if (EqualityComparer<(T1, T2, T3, T4, T5)>.Default.Equals(v1, v2))
      return;

    var sb = new StringBuilder();
    sb.Append(type).Append(" mismatch (");

    void addCmp<T>(T l, T r)
    {
      if (EqualityComparer<T>.Default.Equals(l, r))
      {
        sb.Append(l);
        return;
      }
      else if (typeof(T) == typeof(string))
      {
        var ls = l as string;
        var rs = r as string;
        sb.Append('"').Append(ls ?? "<null>").Append('"');
        sb.Append('[').Append(ls?.Length ?? 0).Append(']');
        sb.Append('/');
        sb.Append('"').Append(rs ?? "<null>").Append('"');
        sb.Append('[').Append(rs?.Length ?? 0).Append(']');
      }
      else
        sb.Append(l).Append('/').Append(r);
    }
    addCmp(v1.Item1, v2.Item1);
    sb.Append(", ");
    addCmp(v1.Item2, v2.Item2);
    sb.Append(", ");
    addCmp(v1.Item3, v2.Item3);
    sb.Append(", ");
    addCmp(v1.Item4, v2.Item4);
    sb.Append(", ");
    addCmp(v1.Item5, v2.Item5);
    sb.Append(") in ").Append(file);
    throw new InvalidOperationException(sb.ToString());
  }

  private static void SpeedTest(string basePath)
  {
    var stopwatch = Stopwatch.StartNew();
    var docs = new List<XmlDocument>();
    // var rawDocs = new List<byte[]>();
    foreach (var file in Directory.EnumerateFiles(basePath, "*.xml", SearchOption.AllDirectories))
    {
      if (file.Contains("\\Language\\"))
        continue;
      var doc = new XmlDocument();
      doc.Load(file);
      docs.Add(doc);
      // rawDocs.Add(File.ReadAllBytes(file));
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
      // foreach (var doc in rawDocs)
      {
        xpdoc.NewVersion();
        xpdoc.Import(doc, root.LatestVersion.Id.Index);
        // xpdoc.Import(
        //   XmlReader.Create(new MemoryStream(doc)),
        //   root.LatestVersion.Index);
      }
      total += xpdoc.TotalNodes;
    }
    stopwatch.Stop();
    Console.WriteLine($"imported {docs.Count * ITER_COUNT} docs with {total} nodes in {stopwatch.Elapsed.TotalMilliseconds:0.##}ms");
    // Console.WriteLine($"imported {rawDocs.Count * ITER_COUNT} docs with {total} nodes in {stopwatch.Elapsed.TotalMilliseconds:0.##}ms");
  }

  private static void TestXPath(XPNodeRef node, string path = null)
  {
    var xpath = XPath.Parse(path ?? Path);
    DebugPrint(xpath);

    var i = 0;
    foreach (var val in xpath.Exec(node))
    {
      var strVal = val.Type switch
      {
        XPValueType.NodeSet => val.Node.Nav.OuterXml(),
        _ => val.StringValue,
      };
      Console.WriteLine($"{i++} {val.Type} {strVal}");
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