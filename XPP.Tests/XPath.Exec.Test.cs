
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using XPP.Doc;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  private static IEnumerable<object[]> LoadExecTests(string fname)
  {
    Exception loadex = null;
    IEnumerable<XPathExecEntry> entries = [];
    try
    {
      entries = DataLoader<XPathExecEntry>.Load(fname);
    }
    catch (Exception ex)
    {
      loadex = ex;
    }
    if (loadex != null)
    {
      yield return [null, null, loadex];
      yield break;
    }
    foreach (var entry in entries)
    {
      foreach (var test in entry.Tests)
        yield return [entry.Doc, test];
    }
  }

  public static string TestExecNodeDisplayName(MethodInfo method, object[] args) =>
    $"{method.Name}:{(args[1] as XPathExecTest)?.Id ?? "ERROR"}";

  [TestMethod]
  [DynamicData(nameof(LoadExecTests), ["XPath.Exec.xml"],
    DynamicDataDisplayName = nameof(TestExecNodeDisplayName))]
  [DynamicData(nameof(LoadExecTests), ["XPath.Axis.xml"],
    DynamicDataDisplayName = nameof(TestExecNodeDisplayName))]
  public void TestExecNode(XmlElement srcDoc, XPathExecTest test, Exception ex = null)
  {
    if (ex != null)
      throw new Exception("Data Load Failed", ex);

    var doc = XPDocument.New();
    doc.LatestRoot.Import(srcDoc);
    var actual = new List<Path>();
    foreach (var res in XPath.Exec(test.Path, doc.LatestRoot))
    {
      if (res.Type is not XPValueType.NodeSet)
        throw new InvalidOperationException($"{res.Type}");
      actual.Add(Path.FromNode(res.Node));
    }
    var expected = new List<Path>();
    foreach (var match in test.Matches)
      expected.Add(match);

    CollectionAssert.AreEqual(expected, actual, ListMsg(expected, actual));
  }
}