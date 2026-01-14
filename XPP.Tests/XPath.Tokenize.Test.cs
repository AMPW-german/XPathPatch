
using System;
using System.Collections.Generic;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
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

  [Serializable]
  public record struct TestToken(TokenType Type, string Value)
  {
    public static implicit operator TestToken((TokenType, string) pair) =>
      new(pair.Item1, pair.Item2);
  }
  private static List<TestToken> Toks(params List<TestToken> toks) => toks;

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
}