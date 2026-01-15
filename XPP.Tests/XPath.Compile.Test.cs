
using System;
using System.Collections.Generic;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  private static IEnumerable<object[]> CompileTests => [
    ["A/B/C[1]", new TestCExpr(
      ValOpType.Path,
      Path: [
        new(PathOpType.Context),
        new(PathOpType.Axis, Axis: AxisType.Child),
        new(PathOpType.NameTest, Ns: "", Name: "A"),
        new(PathOpType.Axis, Axis: AxisType.Child),
        new(PathOpType.NameTest, Ns: "", Name: "B"),
        new(PathOpType.Axis, Axis: AxisType.Child),
        new(PathOpType.NameTest, Ns: "", Name: "C"),
        new(PathOpType.Filter, Expr: new(ValOpType.Number, Number: 1)),
      ]
    )],
    ["(A/B/C)[1]", new TestCExpr(
      ValOpType.Path,
      Path: [
        new(PathOpType.Expr, Expr: new(ValOpType.Path, Path: [
          new(PathOpType.Context),
          new(PathOpType.Axis, Axis: AxisType.Child),
          new(PathOpType.NameTest, Ns: "", Name: "A"),
          new(PathOpType.Axis, Axis: AxisType.Child),
          new(PathOpType.NameTest, Ns: "", Name: "B"),
          new(PathOpType.Axis, Axis: AxisType.Child),
          new(PathOpType.NameTest, Ns: "", Name: "C"),
        ])),
        new(PathOpType.Filter, Expr: new(ValOpType.Number, Number: 1)),
      ]
    )],
    ["A/B[sum(C/@V)=15]", new TestCExpr(ValOpType.Path, Path: [
      new(PathOpType.Context),
      new(PathOpType.Axis, Axis: AxisType.Child),
      new(PathOpType.NameTest, Ns: "", Name: "A"),
      new(PathOpType.Axis, Axis: AxisType.Child),
      new(PathOpType.NameTest, Ns: "", Name: "B"),
      new(PathOpType.Filter, Expr: new(ValOpType.Eq,
        Left: new(ValOpType.Func, Func: LibraryFunc.Sum, Args: [
          new(ValOpType.Path, Path: [
            new(PathOpType.Context),
            new(PathOpType.Axis, Axis: AxisType.Child),
            new(PathOpType.NameTest, Ns: "", Name: "C"),
            new(PathOpType.Axis, Axis: AxisType.Attribute),
            new(PathOpType.NameTest, Ns: "", Name: "V"),
            new(PathOpType.Normalize)
          ])
        ]),
        Right: new(ValOpType.Number, Number: 15)
      )),
    ])],
  ];

  public record class TestCExpr(
    ValOpType Type,
    string String = null,
    double? Number = null,
    LibraryFunc? Func = null,
    TestCPath[] Path = null,
    TestCExpr Left = null,
    TestCExpr Right = null,
    TestCExpr[] Args = null)
  {
    public void Equals(TreeComparer t, XPath xpath, int index)
    {
      t.CmpThrow(
        "Index",
        index >= 0 && index < xpath.Vals.Length,
        $"{index}", $"0..{xpath.Vals.Length}");
      var op = xpath.Vals[index];
      t.CmpThrow("Type", Type == op.Type, Type, op.Type);
      if (String != null && xpath.Data[op.Value.String] is char[] opstr)
        t.CmpThrow("String", opstr.SequenceEqual(String), String, new(opstr));
      if (Number != null)
        t.CmpThrow("Number", Number == op.Value.Number, Number, op.Value.Number);
      if (Func != null)
        t.CmpThrow("Func", Func == op.Func, Func, op.Func);
      if (Path != null)
      {
        for (var i = 0; i < Path.Length; i++)
          t.Child($"Path {Path.Length - i - 1} P{op.Left + i:00}", t =>
            Path[Path.Length - i - 1].Equals(t, xpath, op.Left + i));
      }
      if (Left != null)
        t.Child($"Left", t => Left.Equals(t, xpath, op.Left));
      if (Right != null)
        t.Child($"Right", t => Right.Equals(t, xpath, op.Right));
      if (Args != null)
      {
        var start = op.Args.Start.Value;
        var end = op.Args.End.Value;
        var len = end - start;
        t.CmpThrow("Argc", Args.Length == len, Args.Length, len);
        for (var i = 0; i < len; i++)
          t.Child($"Arg {i}", t => Args[i].Equals(t, xpath, start + i));
      }
    }
  }
  public record class TestCPath(
    PathOpType Type,
    TestCPath UnionL = null, TestCPath UnionR = null,
    AxisType? Axis = null,
    NodeType? NodeType = null,
    string Ns = null,
    string Name = null,
    TestCExpr Expr = null,
    bool? Dedupe = null)
  {
    public void Equals(TreeComparer t, XPath xpath, int index)
    {
      t.CmpThrow("Index", index >= 0 && index < xpath.Paths.Length,
        $"{index}", $"{0..xpath.Paths.Length}");
      var op = xpath.Paths[index];
      t.CmpThrow("Type", Type == op.Type, Type, op.Type);
      if (UnionL != null) t.Child("UnionL", t => UnionL.Equals(t, xpath, op.Paths.Item1));
      if (UnionR != null) t.Child("UnionR", t => UnionR.Equals(t, xpath, op.Paths.Item1));
      if (Axis != null) t.CmpThrow("Axis", Axis == op.Axis, Axis, op.Axis);
      if (NodeType != null)
        t.CmpThrow("NodeType", NodeType == op.NodeType, NodeType, op.NodeType);
      if (Ns != null)
        t.CmpThrow("Ns", xpath.Data[op.Ns].SequenceEqual(Ns), Ns, new(xpath.Data[op.Ns]));
      if (Name != null)
        t.CmpThrow("Name", xpath.Data[op.Name].SequenceEqual(Name),
          Name, new(xpath.Data[op.Name]));
      if (Expr != null)
        t.Child($"Expr V{op.Expr:00}", t => Expr.Equals(t, xpath, op.Expr));
      if (Dedupe != null)
        t.CmpThrow("Dedupe", Dedupe == op.Dedupe, Dedupe, op.Dedupe);
    }
  }

  [TestMethod]
  [DynamicData(nameof(CompileTests))]
  public void TestCompile(string source, TestCExpr expr)
  {
    var xpath = XPath.Parse(source);
    var t = new TreeComparer(source);
    expr.Equals(t, xpath, 0);
    t.Assert();
  }
}