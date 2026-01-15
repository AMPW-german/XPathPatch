
using System.Collections.Generic;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  private static IEnumerable<object[]> ParseTests => [
    ["A/B/*", new TestAst(AstType.Sep, "/",
      new(AstType.Sep, "/",
        new(AstType.NodeTest, "A"),
        new(AstType.NodeTest, "B")),
      new(AstType.NodeTest, "*"))],
    ["(A/B)[1]/@*", new TestAst(AstType.Sep, "/",
      new(AstType.ExprFilter, "[",
        new(AstType.Sep, "/",
          new(AstType.NodeTest, "A"),
          new(AstType.NodeTest, "B")),
        new(AstType.Value, "1")),
      new(AstType.Axis, "@",
        new(AstType.NodeTest, "*")))],
    ["(A/B)[@*][1]", new TestAst(AstType.ExprFilter, "[",
      new(AstType.ExprFilter, "[",
        new(AstType.Sep, "/",
          new(AstType.NodeTest, "A"),
          new(AstType.NodeTest, "B")),
        new(AstType.Axis, "@",
          new(AstType.NodeTest, "*"))),
      new(AstType.Value, "1"))],
    ["A/B[1]/@*", new TestAst(AstType.Sep, "/",
      new(AstType.Sep, "/",
        new(AstType.NodeTest, "A"),
        new(AstType.PathFilter, "[",
          new(AstType.NodeTest, "B"),
          new(AstType.Value, "1"))),
      new(AstType.Axis, "@",
        new(AstType.NodeTest, "*")))],
    ["A/child::B[1]/@*", new TestAst(AstType.Sep, "/",
      new(AstType.Sep, "/",
        new(AstType.NodeTest, "A"),
        new(AstType.PathFilter, "[",
          new(AstType.Axis, "child",
            new(AstType.NodeTest, "B")),
          new(AstType.Value, "1"))),
      new(AstType.Axis, "@",
        new(AstType.NodeTest, "*")))],
  ];

  public record class TestAst(AstType Type, string Token, TestAst Left = null, TestAst Right = null)
  {
    public static void Equals(TreeComparer t, string source, TestAst exp, AstNode[] nodes, int index)
    {
      if (exp == null && index == -1)
        return;
      if (exp == null)
        t.Compare("Node", false, "<null>",
          $"{nodes[index].Type} '{source[nodes[index].Token.Data]}'");
      else if (index == -1)
        t.Compare("Node", false, $"{exp.Type} '{exp.Token}'", "<null>");
      else
      {
        var act = nodes[index];
        var astr = source[act.Token.Data];
        t.Compare("Node", exp.Type == act.Type && exp.Token == astr,
          $"{exp.Type} '{exp.Token}'", $"{act.Type} '{astr}'");
      }
      var (e, a) = (exp?.Left, index == -1 ? -1 : nodes[index].Child0);
      if (e != null || a != -1)
        t.Child("Left", t => Equals(t, source, e, nodes, a));
      (e, a) = (exp?.Right, index == -1 ? -1 : nodes[index].Child1);
      if (e != null || a != -1)
        t.Child("Right", t => Equals(t, source, e, nodes, a));
    }
  }

  [TestMethod]
  [DynamicData(nameof(ParseTests))]
  public void TestParse(string source, TestAst expected)
  {
    var nodes = Parser.Parse(source).ToArray();
    var t = new TreeComparer(source);
    TestAst.Equals(t, source, expected, nodes, nodes.Length-1);
    t.Assert();
  }
}