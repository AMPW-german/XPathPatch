
using System;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  [TestMethod]
  [DynamicData("Load", typeof(DataLoader<XPathEntry>), ["Xpath.xml"])]
  public void TestParse(XPathEntry entry, string err = null)
  {
    if (err != null)
      throw new InvalidOperationException(err);
    if (entry.Parsed == null)
      Assert.Inconclusive();

    var nodes = Parser.Parse(entry.Expr).ToArray();
    var t = new TreeComparer(entry.Expr);
    XPathAst.TreeCompare(t, entry.Expr, entry.Parsed, nodes, nodes.Length - 1);
    t.Assert();
  }
}

public partial class XPathAst
{
  public static void TreeCompare(
    TreeComparer t, string source, XPathAst exp, AstNode[] nodes, int index)
  {
    if (exp == null && index == -1)
      return;
    if (exp == null)
      t.Compare("Node", false, "<null>",
        $"{nodes[index].Type} '{source[nodes[index].Token.Data]}'");
    else if (index == -1)
      t.Compare("Node", false, $"{exp.Type} '{exp.Value}'", "<null>");
    else
    {
      var act = nodes[index];
      var astr = source[act.Token.Data];
      t.Compare("Node", exp.Type == act.Type && exp.Value == astr,
        $"{exp.Type} '{exp.Value}'", $"{act.Type} '{astr}'");
    }
    var (e, a) = (exp?.Left, index == -1 ? -1 : nodes[index].Child0);
    if (e != null || a != -1)
      t.Child("Left", t => TreeCompare(t, source, e, nodes, a));
    (e, a) = (exp?.Right, index == -1 ? -1 : nodes[index].Child1);
    if (e != null || a != -1)
      t.Child("Right", t => TreeCompare(t, source, e, nodes, a));
  }
}