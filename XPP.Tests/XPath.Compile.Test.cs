
using System;
using System.Collections.Generic;
using System.Linq;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  [TestMethod]
  [DynamicData("Load", typeof(DataLoader<XPathEntry>), ["Xpath.xml"])]
  public void TestCompile(XPathEntry entry, string err = null)
  {
    if (err != null)
      throw new InvalidOperationException(err);
    if (entry.Compiled == null)
      Assert.Inconclusive();
    var xpath = XPath.Parse(entry.Expr);
    var t = new TreeComparer(entry.Expr);
    XPathCExpr.TreeCompare(t, entry.Compiled, xpath, 0);
    t.Assert();
  }
}

public partial class XPathCExpr
{
  public static void TreeCompare(TreeComparer t, XPathCExpr expr, XPath xpath, int index)
  {
    t.CmpThrow(
      "Index",
      index >= 0 && index < xpath.Vals.Length,
      $"{index}", $"0..{xpath.Vals.Length}");
    var op = xpath.Vals[index];
    t.CmpThrow("Type", expr.Type == op.Type, expr.Type, op.Type);
    if (expr.String != null && xpath.Data[op.Value.String] is char[] opstr)
      t.CmpThrow("String", opstr.SequenceEqual(expr.String), expr.String, new(opstr));
    if (expr.Number != null)
      t.CmpThrow("Number", expr.Number == op.Value.Number, expr.Number, op.Value.Number);
    if (expr.Func != null)
      t.CmpThrow("Func", expr.Func == op.Func, expr.Func, op.Func);
    if (expr.Path != null)
      XPathCPath.ListCompare(t, "Path", expr.Path, xpath, op.Left);
    if (expr.Left != null)
      t.Child($"Left", t => TreeCompare(t, expr.Left, xpath, op.Left));
    if (expr.Right != null)
      t.Child($"Right", t => TreeCompare(t, expr.Right, xpath, op.Right));
    if ((expr.Args?.Count ?? 0) != 0)
    {
      var start = op.Args.Start.Value;
      var end = op.Args.End.Value;
      var len = end - start;
      t.CmpThrow("Argc", expr.Args.Count == len, expr.Args.Count, len);
      for (var i = 0; i < len; i++)
        t.Child($"Arg {i}", t => TreeCompare(t, expr.Args[i], xpath, start + i));
    }
  }
}

public partial class XPathCPath
{
  public static void TreeCompare(TreeComparer t, XPathCPath path, XPath xpath, int index)
  {
    t.CmpThrow("Index", index >= 0 && index < xpath.Paths.Length,
      $"{index}", $"{0..xpath.Paths.Length}");
    var op = xpath.Paths[index];
    t.CmpThrow("Type", path.Type == op.Type, path.Type, op.Type);
    if (path.UnionL != null)
      ListCompare(t, "UnionL", path.UnionL, xpath, op.Paths.Item1);
    if (path.UnionR != null)
      ListCompare(t, "UnionR", path.UnionL, xpath, op.Paths.Item2);
    if (path.Axis != null) t.CmpThrow("Axis", path.Axis == op.Axis, path.Axis, op.Axis);
    if (path.NodeType != null)
      t.CmpThrow("NodeType", path.NodeType == op.NodeType, path.NodeType, op.NodeType);
    if (path.Ns != null)
      t.CmpThrow("Ns",
        xpath.Data[op.Ns].SequenceEqual(path.Ns), path.Ns, new(xpath.Data[op.Ns]));
    if (path.Name != null)
      t.CmpThrow("Name", xpath.Data[op.Name].SequenceEqual(path.Name),
        path.Name, new(xpath.Data[op.Name]));
    if (path.Expr != null)
      t.Child($"Expr V{op.Expr:00}",
        t => XPathCExpr.TreeCompare(t, path.Expr, xpath, op.Expr));
    if (path.Dedupe != null)
      t.CmpThrow("Dedupe", path.Dedupe == op.Dedupe, path.Dedupe, op.Dedupe);
  }

  public static void ListCompare(
    TreeComparer t, string name, List<XPathCPath> paths, XPath xpath, int index)
  {
    if ((paths?.Count ?? 0) == 0)
      return;
    var cpaths = new List<int>();
    var xidx = index;
    while (true)
    {
      cpaths.Add(xidx);
      if (xpath.Paths[xidx].Type.IsRoot)
        break;
      xidx++;
    }
    cpaths.Reverse();
    t.Compare(
      $"{name}.Count", cpaths.Count == paths.Count,
      string.Join(',', paths.Select(p => p.Type)),
      string.Join(',', cpaths.Select(i => xpath.Paths[i].Type)));
    if (paths.Count != cpaths.Count)
      return;
    for (var i = 0; i < paths.Count; i++)
      t.Child($"{name} {i} P{cpaths[i]:00}", t =>
        TreeCompare(t, paths[i], xpath, cpaths[i]));
  }
}