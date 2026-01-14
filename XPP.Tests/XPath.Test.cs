using System;
using System.Collections.Generic;
using System.Text;
using XPP.Path;

namespace XPP.Tests;

[TestClass]
public class XPathTests
{
  [Serializable]
  public record struct TestToken(TokenType Type, string Value)
  {
    public static implicit operator TestToken((TokenType, string) pair) =>
      new(pair.Item1, pair.Item2);
  }
  private static List<TestToken> Toks(params List<TestToken> toks) => toks;

  private static IEnumerable<object[]> TokenizeTests => [
    ["A/B/*", Toks(
      (TokenType.NtName, "A"),
      (TokenType.OpSep, "/"),
      (TokenType.NtName, "B"),
      (TokenType.OpSep, "/"),
      (TokenType.NtAny, "*")
    )],
    ["(A/B)[1]", Toks(
      (TokenType.POpen, "("),
      (TokenType.NtName, "A"),
      (TokenType.OpSep, "/"),
      (TokenType.NtName, "B"),
      (TokenType.PClose, ")"),
      (TokenType.BOpen, "["),
      (TokenType.Number, "1"),
      (TokenType.BClose, "]")
    )],
  ];

  [TestMethod]
  [DynamicData(nameof(TokenizeTests))]
  public void TestTokenize(string source, List<TestToken> expected)
  {
    var actual = new List<TestToken>();
    var tokenizer = new Tokenizer(source);
    while (!tokenizer.EOF && tokenizer.Next(out var tok))
      actual.Add((tok.Type, source[tok.Data]));

    CollectionAssert.AreEqual(expected, actual, ListMsg(expected, actual));
  }

  public record class TestAst(AstType Type, string Token, TestAst Left = null, TestAst Right = null);

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

  [TestMethod]
  [DynamicData(nameof(ParseTests))]
  public void TestParse(string source, TestAst expected)
  {
    var nodes = Parser.Parse(source).ToArray();
    var sb = new StringBuilder("\n");
    var match = true;
    var indent = "";
    void AddA(int aidx)
    {
      sb.Append(indent).Append("> ");
      if (aidx == -1)
        sb.AppendLine("null");
      else
        sb.AppendLine($"{nodes[aidx].Type} '{source[nodes[aidx].Token.Data]}'");
    }
    void AddE(TestAst exp, bool eq = false)
    {
      sb.Append(indent);
      sb.Append(eq ? "= " : "< ");
      if (exp == null)
        sb.AppendLine("null");
      else
        sb.AppendLine($"{exp.Type} '{exp.Token}'");
    }
    void Check(TestAst exp, int aidx)
    {
      if (exp == null && aidx == -1)
        return;
      if (exp is null != aidx is -1)
      {
        AddA(aidx);
        AddE(exp);
        match = false;
      }
      else
      {
        var act = nodes[aidx];
        if (act.Type == exp.Type && source[act.Token.Data] == exp.Token)
          AddE(exp, true);
        else
        {
          AddA(aidx);
          AddE(exp);
          match = false;
        }
      }
      indent += "  ";
      Check(exp?.Left, aidx == -1 ? -1 : nodes[aidx].Child0);
      Check(exp?.Right, aidx == -1 ? -1 : nodes[aidx].Child1);
      indent = indent[2..];
    }
    Check(expected, nodes.Length - 1);
    if (!match)
      Assert.Fail(sb.ToString());
  }

  private static string ListMsg<T>(List<T> expected, List<T> actual)
  {
    var sb = new StringBuilder();
    sb.AppendLine();
    for (var i = 0; i < expected.Count && i < actual.Count; i++)
    {
      if (EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
        sb.AppendLine($"{i} = {expected[i]}");
      else
      {
        sb.AppendLine($"{i} < {expected[i]}");
        sb.AppendLine($"{i} > {actual[i]}");
      }
    }
    for (var i = actual.Count; i < expected.Count; i++)
      sb.AppendLine($"{i} < {expected[i]}");
    for (var i = expected.Count; i < actual.Count; i++)
      sb.AppendLine($"{i} > {actual[i]}");
    return sb.ToString();
  }
}