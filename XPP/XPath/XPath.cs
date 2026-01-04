
using System;

namespace XPP.XPatch;

public partial class XPath
{
  public const int MAX_LENGTH = 1024;
  public readonly string Source;
  public readonly AstNode[] Nodes;

  private XPath(string source, ReadOnlySpan<AstNode> exprs)
  {
    Source = source;
    Nodes = exprs.ToArray();
  }

  public static XPath Parse(string source) => new(source, Parser.Parse(source));
}