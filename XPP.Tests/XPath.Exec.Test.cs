
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using XPP.Doc;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  private static IEnumerable<object[]> ExecNodeTests => Concat(
    ExecDoc("""
      <A>
        <B> <C /> </B>
        <B> <C /> </B>
      </A>
      """,
      new("A/B/C[1]", "A/B#0/C", "A/B#1/C"),
      new("(A/B/C)[1]", "A/B#0/C"))
  );

  [Serializable]
  public record class PathStep(string Name, XPType Type, int Index)
  {
    public static implicit operator PathStep(string path)
    {
      string desc, idx;
      var split = path.IndexOf('#');
      if (split == -1)
        (desc, idx) = (path, "0");
      else
        (desc, idx) = (path[..split], path[(split + 1)..]);

      var index = int.Parse(idx);
      return desc[0] switch
      {
        '\'' => new("", XPType.Text, index),
        '[' => new("", XPType.CData, index),
        '?' => new(desc[1..], XPType.ProcInst, index),
        '!' => new("", XPType.Comment, index),
        '@' => new(desc[1..], XPType.Attribute, index),
        ':' => new(desc[1..], XPType.Namespace, index),
        _ => new(desc, XPType.Element, index),
      };
    }

    public override string ToString()
    {
      var prefix = Type switch
      {
        XPType.Text => "'",
        XPType.CData => "[",
        XPType.ProcInst => "?",
        XPType.Comment => "!",
        XPType.Attribute => "@",
        XPType.Namespace => ":",
        _ => "",
      };
      return $"{prefix}{Name}#{Index}";
    }
  }

  public class Path : List<PathStep>, IEquatable<Path>
  {
    public bool Equals(Path other) => Enumerable.SequenceEqual(this, other);
    public override string ToString() => string.Join('/', this);
    public static implicit operator Path(string path) =>
      [.. path.Split('/').Select(p => (PathStep)p)];
  }

  [Serializable]
  public record class ExecTest(string XPath, params List<Path> Paths);

  private static IEnumerable<object[]> ExecDoc(string xml, params List<ExecTest> tests)
  {
    foreach (var test in tests)
      yield return [xml, test.XPath, test.Paths];
  }

  [TestMethod]
  [DynamicData(nameof(ExecNodeTests))]
  public void TestExecNode(string xml, string xpath, List<Path> expected)
  {
    var doc = XPDocument.New();
    doc.Import(XmlReader.Create(new StringReader(xml)));

    var actual = new List<Path>();
    foreach (var res in XPath.Exec(xpath, doc.LatestRoot))
    {
      if (res.Type is not XPValueType.NodeSet)
        throw new InvalidOperationException($"{res.Type}");
      actual.Add(NodeToPath(res.Node));
    }

    CollectionAssert.AreEqual(expected, actual, ListMsg(expected, actual));
  }

  private static Path NodeToPath(XPNodeRef node)
  {
    var path = new Path();
    while (node.Valid && node.Type is not XPType.Document)
    {
      var index = 0;
      var prev = node.PrevSibling;
      while (prev.Valid)
      {
        if (prev.Type == node.Type && prev.Name == node.Name)
          index++;
        prev = prev.PrevSibling;
      }
      path.Add(new(node.Name.Local, node.Type, index));
      node = node.Parent;
    }
    path.Reverse();
    return path;
  }
}