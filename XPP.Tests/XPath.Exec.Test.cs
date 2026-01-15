
using System;
using System.Collections.Generic;
using System.IO;
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
      actual.Add(Path.FromNode(res.Node));
    }

    CollectionAssert.AreEqual(expected, actual, ListMsg(expected, actual));
  }
}