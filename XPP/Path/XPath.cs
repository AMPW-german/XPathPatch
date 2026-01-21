
using XPP.Doc;

namespace XPP.Path;

public partial class XPath
{
  public readonly string Source;
  public readonly ExecExprOp Compiled;

  private XPath(string source, ExecExprOp compiled)
  {
    Source = source;
    Compiled = compiled;
  }

  public static XPath Parse(string source) => new(source, Compiler.Compile(source));

  public ExecResult Exec(XPNodeRef ctx) => Compiled.Evaluate(ctx);

  public static ExecResult Exec(string source, XPNodeRef ctx) => Parse(source).Exec(ctx);
}