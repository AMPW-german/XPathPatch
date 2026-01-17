
using System;
using System.Collections.Generic;
using System.Linq;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  private static IEnumerable<object[]> LoadCompileTests() =>
    DataLoader<XPathEntry>.LoadFilter("Xpath.xml", e => e.Compiled != null);

  [TestMethod]
  [DynamicData(nameof(LoadCompileTests))]
  public void TestCompile(XPathEntry entry, string err = null)
  {
    if (err != null)
      throw new InvalidOperationException(err);
    var xpath = XPath.Parse(entry.Expr);
    var t = new TreeComparer(entry.Expr);
    XPathCExpr.TreeCompare(t, entry.Compiled, xpath, 0);
    for (var i = 0; i < xpath.Vals.Length; i++)
    {
      var v = xpath.Vals[i];
      t.Add($"V{i:00} {v.Type} {v.Left} {v.Right}");
    }
    for (var i = 0; i < xpath.Paths.Length; i++)
    {
      var p = xpath.Paths[i];
      t.Add($"P{i:00} {p.Type} {p.Length} {p.Expr} {p.RootNum}");
    }
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
    if (expr.String != null)
      t.CmpThrow("String", expr.String == op.String, expr.String, op.String);
    if (expr.Number != null)
      t.CmpThrow("Number", expr.Number == op.Number, expr.Number, op.Number);
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
    t.CmpThrow("Mode", path.Mode == op.Mode, path.Mode, op.Mode);
    t.CmpThrow("Forward", path.Forward == op.Forward, path.Forward, op.Forward);
    t.CmpThrow("Dedupe", path.Dedupe == op.Dedupe, path.Dedupe, op.Dedupe);
    t.CmpThrow("Reverse", path.Reverse == op.Reverse, path.Reverse, op.Reverse);
    if (path.UnionL != null)
      ListCompare(t, "UnionL", path.UnionL, xpath, op.UnionL);
    if (path.UnionR != null)
      ListCompare(t, "UnionR", path.UnionL, xpath, op.UnionR);
    if (path.Axis != null) t.CmpThrow("Axis", path.Axis == op.Axis, path.Axis, op.Axis);
    if (path.NodeType != null)
      t.CmpThrow("NodeType", path.NodeType == op.NodeType, path.NodeType, op.NodeType);
    if (path.Ns != null)
      t.CmpThrow("Ns", path.Ns == op.Ns, path.Ns, op.Ns);
    if (path.Name != null)
      t.CmpThrow("Name", path.Name == op.Name, path.Name, op.Name);
    if (path.Expr != null)
      t.Child($"Expr V{op.Expr:00}",
        t => XPathCExpr.TreeCompare(t, path.Expr, xpath, op.Expr));
  }

  public static void ListCompare(
    TreeComparer t, string name, List<XPathCPath> paths, XPath xpath, int index)
  {
    if ((paths?.Count ?? 0) == 0)
      return;
    var cpaths = new List<int>();
    var ccount = xpath.Paths[index].Length;
    for (var i = 0; i < ccount; i++)
      cpaths.Add(index + i);
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