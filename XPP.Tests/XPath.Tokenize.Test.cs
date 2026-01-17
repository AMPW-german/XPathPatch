
using System;
using System.Collections.Generic;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  private static IEnumerable<object[]> LoadTokenizeTests() =>
    DataLoader<XPathEntry>.LoadFilter("XPath.xml", e => e.Tokens.Count > 0);

  [TestMethod]
  [DynamicData(nameof(LoadTokenizeTests))]
  public void TestTokenize(XPathEntry entry, Exception ex = null)
  {
    if (ex != null)
      throw new Exception("Data Load failed", ex);
    var actual = new List<XPathTok>();
    var tokenizer = new Tokenizer(entry.Expr);
    while (!tokenizer.EOF && tokenizer.Next(out var tok))
      actual.Add(new() { Type = tok.Type, Value = entry.Expr[tok.Data] });

    CollectionAssert.AreEqual(entry.Tokens, actual, ListMsg(entry.Tokens, actual));
  }
}

public partial class XPathTok : IEquatable<XPathTok>
{
  public bool Equals(XPathTok other) => Type == other.Type && Value == other.Value;
  public override bool Equals(object obj) => Equals(obj as XPathTok);
  public override int GetHashCode() => HashCode.Combine(Type, Value);
  public override string ToString() => $"{Type} '{Value}'";
}