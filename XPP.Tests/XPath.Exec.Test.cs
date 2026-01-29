
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
    List<ExecValue> actual = [.. XPath.Exec(test.Path, doc.LatestRoot)];

    var t = new TreeComparer(test.Path);

    for (var i = 0; i < test.Expected.Count || i < actual.Count; i++)
    {
      Compare(t, i,
        i < test.Expected.Count ? test.Expected[i] : null,
        i < actual.Count ? actual[i] : null
      );
    }

    t.Assert();
  }

  private void Compare(TreeComparer t, int index, XPathExecValue expected, ExecValue? actual)
  {
    var estr = expected switch
    {
      null => "<null>",
      XPathBoolValue b => $"bool {b.Value}",
      XPathNumberValue n => $"number {n.Value:0.########}",
      XPathStringValue s => $"string '{s.Value}'",
      XPathNodeValue n => ((Path)n.Path).ToString(),
      _ => throw new InvalidOperationException($"node {expected}"),
    };

    var astr = actual?.Type switch
    {
      null => "<null>",
      XPValueType.Bool => $"bool {actual.Value.Bool}",
      XPValueType.Number => $"number {actual.Value.Number:0.########}",
      XPValueType.String => $"string '{actual.Value.String}'",
      XPValueType.NodeSet => $"node {Path.FromNode(actual.Value.Node)}",
      _ => throw new InvalidOperationException($"{actual?.Type}"),
    };

    var match = (expected, actual) switch
    {
      (XPathBoolValue e, { Type: XPValueType.Bool } a) => e.Value == a.Bool,
      (XPathNumberValue e, { Type: XPValueType.Number } a) => e.Value == a.Number,
      (XPathStringValue e, { Type: XPValueType.String } a) => e.Value == a.String,
      (XPathNodeValue e, { Type: XPValueType.NodeSet } a) =>
        Path.FromNode(a.Node).Equals((Path)e),
      _ => false,
    };

    t.Compare($"{index}", match, estr, astr);
  }
}