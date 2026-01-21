
using System;
using System.Collections.Generic;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  private static IEnumerable<object[]> LoadParseTests() =>
    DataLoader<XPathEntry>.LoadFilter("Xpath.xml", e => e.Parsed != null);

  [TestMethod]
  [DynamicData(nameof(LoadParseTests))]
  public void TestParse(XPathEntry entry, string err = null)
  {
    if (err != null)
      throw new InvalidOperationException(err);

    var root = Parser.Parse(entry.Expr);
    var t = new TreeComparer(entry.Expr);
    XPathAst.TreeCompare(t, entry.Parsed, root);
    t.Assert();
  }
}

public partial class XPathAst
{
  public static void TreeCompare(
    TreeComparer t, XPathAst exp, AstNode act)
  {
    if (exp == null && act == null)
      return;
    if (exp == null)
      t.Compare("Node", false, "<null>",
        $"{act.Type} '{act.Token.String}'");
    else if (act == null)
      t.Compare("Node", false, $"{exp.Type} '{exp.Value}'", "<null>");
    else
    {
      var astr = act.Token.String;
      t.Compare("Node", exp.Type == act.Type && exp.Value == astr,
        $"{exp.Type} '{exp.Value}'", $"{act.Type} '{astr}'");
    }
    var (e, a) = (exp?.Left, act?.Left);
    if (e != null || a != null)
      t.Child("Left", t => TreeCompare(t, e, a));
    (e, a) = (exp?.Right, act?.Right);
    if (e != null || a != null)
      t.Child("Right", t => TreeCompare(t, e, a));
  }
}