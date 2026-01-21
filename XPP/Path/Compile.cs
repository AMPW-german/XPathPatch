
using System;
using System.Collections.Generic;
using XPP.Doc;

namespace XPP.Path;

public class Compiler
{
  public static ExecExprOp Compile(string source, IXPathUserContext userContext) =>
    new Compiler(userContext).CompileExpr(Parser.Parse(source));

  private readonly IXPathUserContext userContext;
  private Compiler(IXPathUserContext userContext) => this.userContext = userContext;

  private ExecExprOp CompileExpr(AstNode astNode)
  {
    switch (astNode.Type)
    {
      case AstType.Root or AstType.Sep or AstType.Axis or AstType.NodeTest or
          AstType.ProcType or AstType.PathFilter or AstType.ExprFilter or AstType.Union:
        return new ExecExprOpPath(CompilePath(astNode));
      case AstType.Value:
        return astNode.Token.Type switch
        {
          TokenType.VarRef => new ExecExprOpVariable(userContext, astNode.Token.String[1..]),
          TokenType.String => new ExecExprOpConstant(new(astNode.Token.String[1..^1])),
          TokenType.Number => new ExecExprOpConstant(
            new(double.Parse(astNode.Token.String))),
          _ => throw new InvalidOperationException($"{astNode.Token.Type}"),
        };
      case AstType.FuncCall:
        var args = new List<ExecExprOp>();
        BuildArgList(args, astNode.Left);
        var func = XPath.ParseLibraryFunc(astNode.Token.String);
        if (func == LibraryFunc.Invalid)
          return new ExecExprOpUserFunc(userContext, astNode.Token.String, [.. args]);
        return new ExecExprOpFunc(func, [.. args]);
      case AstType.BoolOp:
        return new ExecExprOpLogic(
          CompileExpr(astNode.Left), CompileExpr(astNode.Right), astNode.Token.Type);
      case AstType.CompareOp:
        return new ExecExprOpCompare(
          CompileExpr(astNode.Left), CompileExpr(astNode.Right), astNode.Token.Type);
      case AstType.MathOp:
        return new ExecExprOpMath(
          CompileExpr(astNode.Left), CompileExpr(astNode.Right), astNode.Token.Type);
      case AstType.Negate:
        return new ExecExprOpNegate(CompileExpr(astNode.Left));
      case AstType.ArgList:
      default:
        throw new InvalidOperationException($"{astNode.Type}");
    }
  }

  private void BuildArgList(List<ExecExprOp> args, AstNode astNode)
  {
    if (astNode is null)
      return;
    if (astNode.Type is AstType.ArgList)
    {
      BuildArgList(args, astNode.Left);
      BuildArgList(args, astNode.Right);
    }
    else
      args.Add(CompileExpr(astNode));
  }

  private class PathNode(AstNode node, PathNode prev = null, ExecExprOp expr = null)
  {
    public AstType Type = node.Type;
    public Token Token = node.Token;
    public PathNode Prev = prev;
    public ExecExprOp Expr = expr;
    public ExecExprOp InExpr;
    public ExecPathOp UnionL;
    public ExecPathOp UnionR;
  }

  private PathNode BuildPath(AstNode astNode, PathNode prev = null)
  {
    PathNode usedPrev = null;
    PathNode res = null;
    try
    {
      return res = astNode.Type switch
      {
        AstType.Root => BuildPath(astNode.Left, new(astNode)),
        AstType.Sep => BuildPath(astNode.Right,
          new(astNode, BuildPath(astNode.Left, usedPrev = prev))),
        AstType.Axis => astNode.Token.Type switch
        {
          TokenType.AxisName => BuildPath(astNode.Left, new(astNode, usedPrev = prev)),
          TokenType.Attr => BuildPath(astNode.Left, new(astNode, usedPrev = prev)),
          TokenType.Self or TokenType.Parent => new(astNode, usedPrev = prev),
          _ => throw new InvalidOperationException($"{astNode.Token.Type}"),
        },
        AstType.NodeTest or AstType.ProcType => new(astNode, usedPrev = prev),
        AstType.PathFilter => new(astNode, BuildPath(astNode.Left, usedPrev = prev),
          CompileExpr(astNode.Right)),
        AstType.ExprFilter => new(astNode, expr: CompileExpr(astNode.Right))
        {
          InExpr = CompileExpr(astNode.Left),
        },
        AstType.Union => new(astNode)
        {
          UnionL = CompilePath(astNode.Left),
          UnionR = CompilePath(astNode.Right)
        },
        AstType.BoolOp or AstType.CompareOp or AstType.MathOp
        or AstType.Negate or AstType.FuncCall or AstType.Value =>
          new(astNode) { InExpr = CompileExpr(astNode) },
        _ => throw new InvalidOperationException($"{astNode.Type}"),
      };
    }
    finally
    {
      if (prev != null && usedPrev == null && res != null)
        throw new InvalidOperationException($"unused prev node for {astNode.Type}");
    }
  }

  private class PathStep
  {
    public bool Root;
    public PathNode Axis;
    public PathNode NodeTest;
    public List<PathNode> Filters;
  }

  private (PathStep, PathNode) BuildStep(PathNode node)
  {
    var step = new PathStep();

    if (node.Type is AstType.Root or AstType.Union || node.InExpr != null)
    {
      step.Root = true;
      step.Axis = node;
      if (node.Type is AstType.ExprFilter)
        step.Filters = [node];
      return (step, null);
    }

    if (node.Type is AstType.Sep)
    {
      step.Axis = node;
      return (step, node.Prev);
    }

    while (node?.Type is AstType.PathFilter)
    {
      step.Filters ??= [];
      step.Filters.Add(node);
      node = node.Prev;
    }
    step.Filters?.Reverse();

    if (node?.Type is AstType.NodeTest or AstType.ProcType)
    {
      step.NodeTest = node;
      node = node.Prev;
    }

    if (node?.Type is AstType.Axis)
    {
      step.Axis = node;
      node = node.Prev;
    }

    if (node?.Type is AstType.Sep && node.Token.Type is TokenType.OpSep)
      node = node.Prev;

    if (step.Axis == null && step.NodeTest == null)
      throw new InvalidOperationException();

    return (step, node);
  }

  private ExecPathOp CompileStep(PathStep step, ExecPathOp path)
  {
    var axis = AxisType.Child;
    switch (step.Axis?.Type)
    {
      case AstType.Root:
        return step.Axis.Token.Type switch
        {
          TokenType.OpSep => new ExecPathOpRoot(),
          TokenType.OpSepDesc => new ExecPathOpAxisDescendantOrSelf(new ExecPathOpRoot()),
          _ => throw new InvalidOperationException($"{step.Axis.Token.Type}"),
        };
      case AstType.Sep:
        axis = step.Axis.Token.Type switch
        {
          TokenType.OpSepDesc => AxisType.DescendantOrSelf,
          _ => throw new InvalidOperationException($"{step.Axis.Token.Type}"),
        };
        break;
      case AstType.ExprFilter:
        path = new ExecPathOpExpr(step.Axis.InExpr);
        axis = AxisType.Self;
        break;
      case AstType.Axis:
        axis = step.Axis.Token.Type switch
        {
          TokenType.Attr => AxisType.Attribute,
          TokenType.Self => AxisType.Self,
          TokenType.Parent => AxisType.Parent,
          TokenType.AxisName => XPath.ParseAxisType(step.Axis.Token.String),
          _ => throw new InvalidOperationException($"{step.Axis.Token.Type}"),
        };
        break;
      case AstType.Union:
        return new ExecPathOpUnion(step.Axis.UnionL, step.Axis.UnionR);
      case AstType.PathFilter:
      case AstType.Value:
      case AstType.FuncCall:
      case AstType.BoolOp:
      case AstType.CompareOp:
      case AstType.MathOp:
      case AstType.Negate:
        return new ExecPathOpExpr(step.Axis.InExpr);
      case null:
        break;
      case AstType.NodeTest:
      case AstType.ProcType:
      case AstType.ArgList:
      default:
        throw new InvalidOperationException($"{step.Axis.Type}");
    }

    path ??= new ExecPathOpContext();

    if (path.Forward != axis.IsForward)
      path = new ExecPathOpReverse(path);
    if (axis != AxisType.Self)
      path = ExecPathOpAxis.Make(path, axis);

    switch (step.NodeTest?.Type)
    {
      case AstType.NodeTest:
        path = step.NodeTest.Token.Type switch
        {
          TokenType.NodeType when
            XPath.ParseNodeType(step.NodeTest.Token.String) is NodeType nodeType =>
              nodeType is NodeType.Node ? path : new ExecPathOpNodeType(path, nodeType),
          TokenType.NtAny =>
            new ExecPathOpNameTest(path, axis.PrincipalType, "", ""),
          TokenType.NtAnyNs =>
            new ExecPathOpNameTest(path, axis.PrincipalType,
              TokNs(step.NodeTest.Token), ""),
          TokenType.NtName =>
            new ExecPathOpNameTest(path, axis.PrincipalType,
              TokNs(step.NodeTest.Token), TokName(step.NodeTest.Token)),
          _ => throw new InvalidOperationException($"{step.NodeTest.Token.Type}"),
        };
        break;
      case AstType.ProcType:
        path = new ExecPathOpNameTest(path, XPType.ProcInst, "",
          step.NodeTest.Token.String[1..^1]);
        break;
      case null: break;
      default:
        throw new InvalidOperationException($"{step.NodeTest.Type}");
    }

    var fcount = step.Filters?.Count ?? 0;
    for (var i = 0; i < fcount; i++)
      path = new ExecPathOpFilter(path, step.Filters[i].Expr);

    if (axis.CanDupe)
      path = new ExecPathOpDedupe(path);

    return path;
  }

  private static string TokNs(Token tok)
  {
    var idx = tok.String.IndexOf(':');
    if (idx == -1)
      return "";
    return tok.String[..idx];
  }
  private static string TokName(Token tok) =>
    tok.String[(tok.String.IndexOf(':') + 1)..];

  private ExecPathOp CompilePath(AstNode astNode)
  {
    var node = BuildPath(astNode);
    var steps = new List<PathStep>();
    while (node != null)
    {
      var (step, prev) = BuildStep(node);
      steps.Add(step);
      node = prev;
    }
    steps.Reverse();

    ExecPathOp path = null;
    foreach (var step in steps)
      path = CompileStep(step, path);

    if (!path.Forward)
      path = new ExecPathOpReverse(path);

    return path;
  }
}