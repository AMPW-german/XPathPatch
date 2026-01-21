
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

  public static XPath Parse(
    string source, IXPathUserContext userContext = null
  ) => new(source, Compiler.Compile(source, userContext));

  public ExecResult Exec(XPNodeRef ctx) => Compiled.Evaluate(ctx);

  public static ExecResult Exec(
    string source, XPNodeRef ctx, IXPathUserContext userContext = null
  ) => Parse(source, userContext).Exec(ctx);
}