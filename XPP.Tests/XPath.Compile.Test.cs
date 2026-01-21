
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    var xpath = Compiler.Compile(entry.Expr, null);
    var t = new TreeComparer(entry.Expr);
    t.Child("Expr", t => XPathCExpr.TreeCompare(t, entry.Compiled, xpath));
    t.Assert();
  }
}

public partial class XPathCExpr
{
  private static readonly MethodInfo[] CmpMethods = [..
    typeof(XPathCExpr).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
      .Where(m => m.Name == nameof(CmpImpl))];

  public static void TreeCompare(TreeComparer t, XPathCExpr expected, ExecExprOp actual)
  {
    if (expected == null && actual == null)
      return;
    if (actual == null)
      t.CmpThrow("Expr", false, $"{expected.Type}", "<null>");

    var cmp = CmpMethods.FirstOrDefault(
      m => m.GetParameters()[2].ParameterType == actual.GetType())
      ?? throw new InvalidOperationException($"no comparison for {actual.GetType()}");

    try
    {
      cmp.Invoke(null, [t, expected, actual]);
    }
    catch (TargetInvocationException ex)
    {
      throw ex.InnerException;
    }
  }

  private static void CmpType(TreeComparer t, XPathCExpr expected, CExprType actual) =>
    t.Compare("Type", expected?.Type == actual,
      expected?.Type.ToString() ?? "<null>",
      $"{actual}");

  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpPath actual)
  {
    CmpType(t, expected, CExprType.Path);
    XPathCPath.TreeCompare(t, expected?.Path ?? [], actual.Path);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpConstant actual)
  {
    CmpType(t, expected, CExprType.Constant);
    switch (actual.Val.Type)
    {
      case XPValueType.Number:
        t.Compare("Val", expected?.Number == actual.Val.Number,
          expected?.Number, actual.Val.Number);
        break;
      case XPValueType.String:
        t.Compare("Val", expected?.String == actual.Val.String,
          expected?.String, actual.Val.String);
        break;
      default: throw new InvalidOperationException($"{actual.Val.Type}");
    }
  }
  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpNegate actual)
  {
    CmpType(t, expected, CExprType.Negate);
    t.Child("Base", t => TreeCompare(t, expected?.Left, actual.Base), true);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpLogic actual)
  {
    CmpType(t, expected, CExprType.Logic);
    t.Compare("Op", expected?.Op == actual.Op, expected?.Op, actual.Op);
    t.Child("Left", t => TreeCompare(t, expected?.Left, actual.Left), true);
    t.Child("Right", t => TreeCompare(t, expected?.Right, actual.Right), true);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpCompare actual)
  {
    CmpType(t, expected, CExprType.Compare);
    t.Compare("Op", expected?.Op == actual.Op, expected?.Op, actual.Op);
    t.Child("Left", t => TreeCompare(t, expected?.Left, actual.Left), true);
    t.Child("Right", t => TreeCompare(t, expected?.Right, actual.Right), true);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpMath actual)
  {
    CmpType(t, expected, CExprType.Math);
    t.Compare("Op", expected?.Op == actual.Op, expected?.Op, actual.Op);
    t.Child("Left", t => TreeCompare(t, expected?.Left, actual.Left), true);
    t.Child("Right", t => TreeCompare(t, expected?.Right, actual.Right), true);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpFunc actual)
  {
    CmpType(t, expected, CExprType.Func);
    t.Compare("Func", expected?.Func == actual.Func, expected?.Func, actual.Func);
    var eargs = expected?.Args ?? [];
    for (var i = 0; i < actual.Args.Length || i < eargs.Count; i++)
    {
      var earg = i < eargs.Count ? eargs[i] : null;
      var aarg = i < actual.Args.Length ? actual.Args[i] : null;
      t.Child($"Arg {i:D2}", t => TreeCompare(t, earg, aarg), true);
    }
  }
  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpVariable actual)
  {
    CmpType(t, expected, CExprType.Variable);
    t.Compare("Name", expected?.String == actual.Name, expected?.String, actual.Name);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCExpr expected, ExecExprOpUserFunc actual)
  {
    CmpType(t, expected, CExprType.UserFunc);
    t.Compare("Name", expected?.String == actual.Name, expected?.String, actual.Name);
    var eargs = expected?.Args ?? [];
    for (var i = 0; i < actual.Args.Length || i < eargs.Count; i++)
    {
      var earg = i < eargs.Count ? eargs[i] : null;
      var aarg = i < actual.Args.Length ? actual.Args[i] : null;
      t.Child($"Arg {i:D2}", t => TreeCompare(t, earg, aarg), true);
    }
  }
}

public partial class XPathCPath
{
  private static readonly MethodInfo[] CmpMethods = [..
    typeof(XPathCPath).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
      .Where(m => m.Name == nameof(CmpImpl))];

  public static void TreeCompare(
    TreeComparer t, List<XPathCPath> expList, ExecPathOp actFinal)
  {
    var actList = new List<ExecPathOp>();
    while (actFinal != null)
    {
      actList.Add(actFinal);
      actFinal = actFinal.Parent;
    }
    actList.Reverse();
    TreeCompare(t, expList, actList);
  }

  public static void TreeCompare(
    TreeComparer t, List<XPathCPath> expList, List<ExecPathOp> actList)
  {
    for (var i = 0; i < expList.Count || i < actList.Count; i++)
    {
      var expected = i < expList.Count ? expList[i] : null;
      var actual = i < actList.Count ? actList[i] : null;

      t.Child($"Path {i:D2}", t =>
      {
        if (actual == null)
          t.CmpThrow("Expr", false, $"{expected.Type}", "<null>");

        var cmp = CmpMethods.FirstOrDefault(
          m => m.GetParameters()[2].ParameterType.IsAssignableFrom(actual.GetType()))
          ?? throw new InvalidOperationException($"no comparison for {actual.GetType()}");

        try
        {
          cmp.Invoke(null, [t, expected, actual]);
        }
        catch (TargetInvocationException ex)
        {
          throw ex.InnerException;
        }
      }, true);
    }
  }

  private static void CmpBase(
    TreeComparer t, XPathCPath expected, ExecPathOp actual, CPathType atype)
  {
    t.Compare("Type", expected?.Type == atype,
      expected?.Type.ToString() ?? "<null>",
      $"{atype}");
    t.Compare("Fwd", expected?.Forward == actual.Forward,
      expected?.Forward.ToString() ?? "<null>",
      $"{actual.Forward}");
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpContext actual)
  {
    CmpBase(t, expected, actual, CPathType.Context);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpRoot actual)
  {
    CmpBase(t, expected, actual, CPathType.Root);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpReverse actual)
  {
    CmpBase(t, expected, actual, CPathType.Reverse);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpDedupe actual)
  {
    CmpBase(t, expected, actual, CPathType.Dedupe);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpUnion actual)
  {
    CmpBase(t, expected, actual, CPathType.Union);
    t.Child("UnionL", t => TreeCompare(t, expected?.UnionL ?? [], actual.Left), true);
    t.Child("UnionR", t => TreeCompare(t, expected?.UnionR ?? [], actual.Right), true);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpExpr actual)
  {
    CmpBase(t, expected, actual, CPathType.Expr);
    t.Child("Expr", t => XPathCExpr.TreeCompare(t, expected?.Expr, actual.Expr), true);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpNodeType actual)
  {
    CmpBase(t, expected, actual, CPathType.NodeType);
    t.Compare("NodeType", expected?.NodeType == actual.Type,
      expected?.NodeType.ToString() ?? "<null>", $"{actual.Type}");
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpNameTest actual)
  {
    CmpBase(t, expected, actual, CPathType.NameTest);
    t.Compare("Ns", expected?.Ns == actual.Ns,
      expected?.Ns ?? "<null>", actual.Ns);
    t.Compare("Name", expected?.Name == actual.Name,
      expected?.Name ?? "<null>", actual.Name);
    t.Compare("PType", expected?.PType == actual.PType,
      expected?.PType.ToString() ?? "<null>", $"{actual.Name}");
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpFilter actual)
  {
    CmpBase(t, expected, actual, CPathType.Filter);
    t.Child("Filter",
      t => XPathCExpr.TreeCompare(t, expected?.Expr, actual.Filter), true);
  }
  private static void CmpImpl(
    TreeComparer t, XPathCPath expected, ExecPathOpAxis actual)
  {
    CmpBase(t, expected, actual, CPathType.Axis);
    t.Compare("Axis", expected?.Axis == actual.Type,
      expected?.Axis.ToString() ?? "<null>", $"{actual.Type}");
  }
}