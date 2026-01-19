
using System;
using XPP.Doc;

namespace XPP.Path;

public partial class XPath
{
  public const int MAX_LENGTH = 1024;
  public readonly string Source;
  public readonly PathOp[] Paths;
  public readonly ValOp[] Vals;

  private XPath(string source, ReadOnlySpan<PathOp> paths, ReadOnlySpan<ValOp> vals)
  {
    Source = source;
    Paths = paths.ToArray();
    Vals = vals.ToArray();
  }

  public static XPath Parse(string source)
  {
    var nodes = Parser.Parse(source);
    Compiler.Compile(nodes, out var paths, out var vals);
    return new(source, paths, vals);
  }

  public XPathExecution Exec(XPNodeRef ctx) => new(this, ctx);

  public static XPathExecution Exec(string source, XPNodeRef ctx) =>
    Parse(source).Exec(ctx);
}