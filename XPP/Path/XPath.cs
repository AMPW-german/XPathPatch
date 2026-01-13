
using System;

namespace XPP.Path;

public partial class XPath
{
  public const int MAX_LENGTH = 1024;
  public readonly string Source;
  public readonly PathOp[] Paths;
  public readonly ValOp[] Vals;
  public readonly char[] Data;

  private XPath(string source, ReadOnlySpan<PathOp> paths, ReadOnlySpan<ValOp> vals, ReadOnlySpan<char> data)
  {
    Source = source;
    Paths = paths.ToArray();
    Vals = vals.ToArray();
    Data = data.ToArray();
  }

  public static XPath Parse(string source)
  {
    var nodes = Parser.Parse(source);
    Compiler.Compile(source, nodes, out var paths, out var vals, out var data);
    return new(source, paths, vals, data);
  }

  public TypedValue Exec<Nav>(Nav nav, out Exec<Nav> exec) where Nav : IXPathNav<Nav>
  {
    exec = new(this);
    return exec.Run(nav);
  }

  public static TypedValue Exec<Nav>(string source, Nav nav, out Exec<Nav> exec)
    where Nav : IXPathNav<Nav>
  {
    var xpath = Parse(source);
    exec = new(xpath);
    return exec.Run(nav);
  }
}