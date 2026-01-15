
using System;
using System.Collections.Generic;
using XPP.Path;

namespace XPP.Tests;

public partial class XPathTests
{
  [TestMethod]
  [DynamicData("Load", typeof(DataLoader<XPathEntry>), ["XPath.xml"])]
  public void TestTokenize(XPathEntry entry, string err = null)
  {
    if (err != null)
      throw new InvalidOperationException(err);
    if (entry.Tokens == null || entry.Tokens.Count == 0)
      Assert.Inconclusive();
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