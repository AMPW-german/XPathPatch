
using System;
using System.Collections.Generic;
using XPP.Doc;
using XPP.Utils;

namespace XPP.Path;

public struct PathOp(PathOpType type)
{
  public PathOpType Type = type;
  public PathOpMode Mode;
  // Mode Flags
  public bool Forward;
  public bool Dedupe;
  public bool Reverse;
  // Metadata
  public int RootNum;
  public int Length;

  // Union
  public int UnionL;
  public int UnionR;

  // Axis
  public AxisType Axis;

  // NodeType
  public NodeType NodeType;

  // NameTest
  public string Ns;
  public string Name;
  public XPType PType;

  // Filter/Expr
  public int Expr;
}

public struct ValOp(ValOpType type)
{
  public ValOpType Type = type;

  public int Left = -1;
  public int Right = -1;

  // Number
  public double Number;
  // String, Variable, UserFunc
  public string String;

  // Func
  public LibraryFunc Func;
  // Func and UserFunc
  public Range Args;
}

public ref struct Compiler
{
  public static void Compile(
    AstNode astRoot, out ReadOnlySpan<PathOp> paths, out ReadOnlySpan<ValOp> vals)
  {
    var compiler = new Compiler(astRoot);
    compiler.Compile();

    paths = compiler.paths.Span;
    vals = compiler.vals.Span;
  }

  // compile state of an AST node
  private class State()
  {
    public int PathStart = -1;
    public int PathIdx = -1;
    public int ValIdx = -1;
  }

  private class QueueEntry(AstNode Node, bool Path)
  {
    public AstNode Node = Node;
    public bool Path = Path;
  }

  private class CompileBuf<O, T>() : ThreadBuf<O, T>(XPath.MAX_LENGTH) where O : CompileBuf<O, T>, new();
  private class PathBuf : CompileBuf<PathBuf, PathOp>;
  private class ValBuf : CompileBuf<ValBuf, ValOp>;

  private readonly AstNode astRoot;

  // result output bufs
  private SpanBuf<PathOp> paths;
  private SpanBuf<ValOp> vals;

  // compile states by node
  private readonly Dictionary<AstNode, State> states = [];

  private readonly List<QueueEntry> queue = [];

  private Compiler(AstNode astRoot)
  {
    this.astRoot = astRoot;

    paths = PathBuf.Buf;
    vals = ValBuf.Buf;
  }

  private State StateOf(AstNode node)
  {
    if (!states.TryGetValue(node, out var state))
      states[node] = state = new();
    return state;
  }

  private void Compile()
  {
    QueueExpr(astRoot);
    DrainQueue();

    var rootNum = -1;
    for (var i = 0; i < paths.Length; i++)
    {
      if (paths[i].Type.IsRoot)
        rootNum++;
      paths[i].RootNum = rootNum;
    }
  }

  private void DrainQueue()
  {
    while (queue.Count > 0)
    {
      var entry = queue[^1];
      queue.RemoveAt(queue.Count-1);
      if (entry.Path)
        CompilePath(entry.Node);
      else
        CompileExpr(entry.Node);
    }
  }

  private void CompileExpr(AstNode node)
  {
    var compile = new ExprCompilePass();
    var backfill = new ExprBackfillPass();

    WalkExpr(node, ref compile);
    DrainQueue();
    WalkExpr(node, ref backfill);
  }

  private void CompilePath(AstNode node)
  {
    var state = StateOf(node);
    state.PathStart = paths.Length;

    var compile = new PathCompilePass();
    var backfill = new PathBackfillPass();

    WalkPath(node, ref compile);
    if (!paths[^1].Type.IsRoot)
    {
      compile.EnsureAxis(ref this);
      AddPath(new(PathOpType.Context));
    }
    DrainQueue();
    WalkPath(node, ref backfill);

    // after we have everything filled in, walk from the end and set mode and flags
    var index = state.PathStart;
    var count = 1;
    while (!paths[index + count - 1].Type.IsRoot) count++;

    var lastDupe = false;
    for (var i = index + count; --i >= index;)
    {
      ref var path = ref paths[i];
      if (path.Type.IsRoot)
      {
        lastDupe = false;
        path.Mode = PathOpMode.Linear;
        path.Forward = true;
        continue;
      }
      ref var nextPath = ref paths[i + 1];
      if (path.Type is PathOpType.Axis)
      {
        if (lastDupe)
          nextPath.Dedupe = true;
        var axis = path.Axis;
        path.Forward = axis.IsForward;
        if (path.Forward != nextPath.Forward)
          nextPath.Reverse = true;
        path.Mode = axis.CanInterleave || lastDupe ? PathOpMode.InsertExpand : PathOpMode.Linear;
        lastDupe = axis.CanDupe;
      }
      else
      {
        path.Mode = PathOpMode.Linear;
        path.Forward = nextPath.Forward;
      }
      if (path.Type is PathOpType.NameTest)
      {
        ref var next = ref paths[i + 1];
        if (next.Type != PathOpType.Axis)
          throw new InvalidOperationException();
        path.PType = next.Axis.PrincipalType;
      }
    }
    if (lastDupe)
      paths[index].Dedupe = true;
    if (!paths[index].Forward)
      paths[index].Reverse = true;

    // finally reverse the order
    paths[index..(index + count)].Reverse();
    paths[index].Length = count;
  }

  private void QueuePath(AstNode node) => queue.Add(new(node, true));
  private void QueueExpr(AstNode node) => queue.Add(new(node, false));

  private int AddPath(PathOp op) => paths.Add(op);
  private void AddExpr(ref State state, ValOp op)
  {
    if (state.ValIdx != -1)
      vals[state.ValIdx] = op;
    else
      state.ValIdx = vals.Add(op);
  }

  private int ReserveExpr(AstNode node)
  {
    var state = StateOf(node);
    if (state.ValIdx != -1)
      return state.ValIdx;
    return state.ValIdx = vals.Add(default);
  }

  private int ValOpIndex(AstNode node)
  {
    var state = StateOf(node);
    if (state.ValIdx == -1)
      throw new InvalidOperationException($"{node.Type}");
    return state.ValIdx;
  }

  private int PathStart(AstNode node)
  {
    var state = StateOf(node);
    if (state.PathStart == -1)
      throw new InvalidOperationException($"{node.Type}");
    return state.PathStart;
  }

  // Initial expression pass. compile nodes and queue paths
  private readonly struct ExprCompilePass : ICompilerPass
  {
    public void Visit(ref Compiler compiler, AstNode node, State state)
    {
      switch (node.Type)
      {
        case AstType.Root:
        case AstType.Sep:
        case AstType.Axis:
        case AstType.NodeTest:
        case AstType.ProcType:
        case AstType.PathFilter:
        case AstType.ExprFilter:
        case AstType.Union:
          compiler.ReserveExpr(node);
          compiler.QueuePath(node);
          break;
        case AstType.Value:
          compiler.AddExpr(ref state, node.Token.Type switch
          {
            TokenType.VarRef =>
              new(ValOpType.Variable) { String = node.Token.String[1..] },
            TokenType.String =>
              new(ValOpType.String) { String = node.Token.String[1..^1] },
            TokenType.Number =>
              new(ValOpType.Number) { Number = double.Parse(node.Token.String) },
            _ => throw new InvalidOperationException($"{node.Token.Type}"),
          });
          break;
        case AstType.FuncCall:
          var func = XPath.ParseLibraryFunc(node.Token.String);
          // reserve spot for func op, then reserve args
          compiler.ReserveExpr(node);
          var (argStart, argCount) = ReserveArgs(ref compiler, node.Child0);
          var (minArgs, maxArgs) = func.ArgCounts;
          if (argCount < minArgs || argCount > maxArgs)
            throw new InvalidOperationException(
              $"invalid argcount for {node.Token.String}: {argCount} <> [{minArgs},{maxArgs}]");
          var argRange = argStart..(argStart + argCount);
          if (func != LibraryFunc.Invalid)
            compiler.AddExpr(ref state,
              new(ValOpType.Func) { Func = func, Args = argRange });
          else
            compiler.AddExpr(ref state, new(ValOpType.UserFunc)
            {
              String = new(node.Token.String),
              Args = argRange,
            });
          break;
        case AstType.ArgList: break;
        case AstType.BoolOp:
        case AstType.CompareOp:
        case AstType.MathOp:
          compiler.ReserveExpr(node);
          compiler.ReserveExpr(node.Child0);
          compiler.ReserveExpr(node.Child1);
          compiler.AddExpr(ref state, new(node.Token.Type.AsValOp)
          {
            Left = compiler.ValOpIndex(node.Child0),
            Right = compiler.ValOpIndex(node.Child1),
          });
          break;
        case AstType.Negate:
          compiler.ReserveExpr(node);
          compiler.ReserveExpr(node.Child0);
          compiler.AddExpr(ref state,
            new(node.Token.Type.AsValOp) { Left = compiler.ValOpIndex(node.Child0) });
          break;
      }
    }

    private static (int first, int count) ReserveArgs(ref Compiler compiler, AstNode node)
    {
      if (node == null)
        return (0, 0);
      if (node.Type == AstType.ArgList)
      {
        var (first, cleft) = ReserveArgs(ref compiler, node.Child0);
        var (_, cright) = ReserveArgs(ref compiler, node.Child1);
        return (first, cleft + cright);
      }
      return (compiler.ReserveExpr(node), 1);
    }
  }

  // backfill path ops after paths are compiled
  private readonly struct ExprBackfillPass : ICompilerPass
  {
    public void Visit(ref Compiler compiler, AstNode node, State state)
    {
      switch (node.Type)
      {
        case AstType.Root:
        case AstType.Sep:
        case AstType.Axis:
        case AstType.NodeTest:
        case AstType.ProcType:
        case AstType.PathFilter:
        case AstType.ExprFilter:
        case AstType.Union:
          compiler.AddExpr(ref state,
            new(ValOpType.Path) { Left = compiler.PathStart(node) });
          break;
      }
    }
  }

  // assign all path nodes and queue child expressions and paths
  private struct PathCompilePass() : ICompilerPass
  {
    private bool hasAxis = false;

    public void EnsureAxis(ref Compiler compiler)
    {
      if (hasAxis)
        return;
      compiler.AddPath(new(PathOpType.Axis) { Axis = AxisType.Child });
      hasAxis = true;
    }

    public void Visit(ref Compiler compiler, AstNode node, State state)
    {
      switch (node.Type)
      {
        case AstType.Root:
          EnsureAxis(ref compiler);
          if (node.Token.Type == TokenType.OpSepDesc)
            compiler.AddPath(new(PathOpType.Axis) { Axis = AxisType.DescendantOrSelf });
          compiler.AddPath(new(PathOpType.Root));
          break;
        case AstType.Sep:
          EnsureAxis(ref compiler);
          if (node.Token.Type == TokenType.OpSepDesc)
            compiler.AddPath(new(PathOpType.Axis) { Axis = AxisType.DescendantOrSelf });
          hasAxis = false;
          break;
        case AstType.Axis:
          compiler.AddPath(new(PathOpType.Axis)
          {
            Axis = node.Token.Type switch
            {
              TokenType.AxisName => XPath.ParseAxisType(node.Token.String),
              TokenType.Attr => AxisType.Attribute,
              TokenType.Self => AxisType.Self,
              TokenType.Parent => AxisType.Parent,
              _ => throw new InvalidOperationException($"{node.Type} {node.Token.Type}"),
            }
          });
          hasAxis = true;
          break;
        case AstType.NodeTest:
          switch (node.Token.Type)
          {
            case TokenType.NtAny:
              compiler.AddPath(new(PathOpType.NameTest)
              {
                Ns = "",
                Name = "",
              });
              break;
            case TokenType.NtAnyNs:
              compiler.AddPath(new(PathOpType.NameTest)
              {
                Ns = TokNs(node.Token),
                Name = "",
              });
              break;
            case TokenType.NtName:
              compiler.AddPath(new(PathOpType.NameTest)
              {
                Ns = TokNs(node.Token),
                Name = TokName(node.Token),
              });
              break;
            case TokenType.NodeType:
              var ntype = XPath.ParseNodeType(node.Token.String);
              if (ntype != NodeType.Node)
                compiler.AddPath(new(PathOpType.NodeType) { NodeType = ntype });
              break;
            default:
              throw new InvalidOperationException($"{node.Type} {node.Token.Type}");
          }
          hasAxis = false;
          break;
        case AstType.ProcType:
          compiler.AddPath(
            new(PathOpType.NameTest) { Name = new(node.Token.String) });
          compiler.AddPath(
            new(PathOpType.NodeType) { NodeType = NodeType.ProcessingInstruction });
          hasAxis = false;
          break;
        case AstType.PathFilter:
          // filter index will be backfilled
          state.PathIdx = compiler.AddPath(new(PathOpType.Filter));
          compiler.QueueExpr(node.Child1);
          hasAxis = false;
          break;
        case AstType.ExprFilter:
          // path and expr index will be backfilled
          state.PathIdx = compiler.AddPath(new(PathOpType.Filter));
          compiler.AddPath(new(PathOpType.Expr));
          compiler.QueueExpr(node.Child0);
          compiler.QueueExpr(node.Child1);
          break;
        case AstType.Union:
          // path indices will be backfilled
          state.PathIdx = compiler.AddPath(new(PathOpType.Union));
          compiler.QueuePath(node.Child0);
          compiler.QueuePath(node.Child1);
          hasAxis = false;
          break;
        case AstType.Value or AstType.FuncCall:
          // expr index will be backfilled
          state.PathIdx = compiler.AddPath(new(PathOpType.Expr));
          compiler.QueueExpr(node);
          break;
        default:
          throw new InvalidOperationException($"{node.Type}");
      }
    }
  }

  // backfill filter and union target indices
  private struct PathBackfillPass() : ICompilerPass
  {
    public void Visit(ref Compiler compiler, AstNode node, State state)
    {
      switch (node.Type)
      {
        case AstType.PathFilter:
          compiler.paths[state.PathIdx] = new(PathOpType.Filter) { Expr = compiler.ValOpIndex(node.Child1) };
          break;
        case AstType.ExprFilter:
          compiler.paths[state.PathIdx] = new(PathOpType.Filter) { Expr = compiler.ValOpIndex(node.Child1) };
          compiler.paths[state.PathIdx + 1] = new(PathOpType.Expr) { Expr = compiler.ValOpIndex(node.Child0) };
          break;
        case AstType.Union:
          compiler.paths[state.PathIdx] = new(PathOpType.Union)
          {
            UnionL = compiler.PathStart(node.Child0),
            UnionR = compiler.PathStart(node.Child1),
          };
          break;
        case AstType.Value or AstType.FuncCall:
          compiler.paths[state.PathIdx] = new(PathOpType.Expr) { Expr = compiler.ValOpIndex(node.Child0) };
          break;
      }
    }
  }

  private static string TokNs(Token tok)
  {
    var str = tok.String;
    var idx = str.IndexOf(':');
    if (idx == -1)
      return "";
    return str[..idx];
  }

  private static string TokName(Token tok)
  {
    var str = tok.String;
    var idx = str.IndexOf(':');
    return str[(idx + 1)..];
  }

  private void WalkExpr<T>(AstNode node, ref T visitor) where T : struct, ICompilerPass, allows ref struct
  {
    switch (node.Type)
    {
      // path nodes start a new path. don't walk, just visit
      case AstType.Root:
      case AstType.Sep:
      case AstType.Axis:
      case AstType.NodeTest:
      case AstType.ProcType:
      case AstType.PathFilter:
      case AstType.ExprFilter:
      case AstType.Union:
      case AstType.Value: // Value is a leaf
        visitor.Visit(ref this, node, StateOf(node));
        break;

      case AstType.FuncCall:
        visitor.Visit(ref this, node, StateOf(node));
        if (node.Child0 != null)
          WalkExpr(node.Child0, ref visitor);
        break;
      case AstType.ArgList:
      case AstType.BoolOp:
      case AstType.CompareOp:
      case AstType.MathOp:
        visitor.Visit(ref this, node, StateOf(node));
        WalkExpr(node.Child0, ref visitor);
        WalkExpr(node.Child1, ref visitor);
        break;
      case AstType.Negate:
        visitor.Visit(ref this, node, StateOf(node));
        WalkExpr(node.Child0, ref visitor);
        break;
      default:
        throw new InvalidOperationException($"{node.Type}");
    }
  }

  private void WalkPath<T>(AstNode node, ref T visitor) where T : struct, ICompilerPass, allows ref struct
  {
    switch (node.Type)
    {
      case AstType.Root:
      case AstType.Axis:
        if (node.Child0 != null)
          WalkPath(node.Child0, ref visitor);
        visitor.Visit(ref this, node, StateOf(node));
        break;
      case AstType.Sep:
        WalkPath(node.Child1, ref visitor);
        visitor.Visit(ref this, node, StateOf(node));
        WalkPath(node.Child0, ref visitor);
        break;
      case AstType.NodeTest:
      case AstType.ProcType:
      case AstType.Union: // union paths are separate
        visitor.Visit(ref this, node, StateOf(node));
        break;
      case AstType.PathFilter:
        visitor.Visit(ref this, node, StateOf(node));
        WalkPath(node.Child0, ref visitor); // walk parent path, but not predicate expression
        break;
      case AstType.ExprFilter:
        // both children are treated as expressions
        visitor.Visit(ref this, node, StateOf(node));
        break;
      // variables and user funcs could produce nodes, so they are allowed as roots
      case AstType.Value when node.Token.Type is TokenType.VarRef:
      case AstType.FuncCall:
        visitor.Visit(ref this, node, StateOf(node));
        break;
      // expression nodes that couldn't produce nodes should not be children
      case AstType.Value:
      case AstType.ArgList:
      case AstType.BoolOp:
      case AstType.CompareOp:
      case AstType.MathOp:
      case AstType.Negate:
        throw new InvalidOperationException($"{node.Type} cannot be in a Path");
      default:
        throw new InvalidOperationException($"Unknown Ast node {node.Type}");
    }
  }

  private interface ICompilerPass
  {
    public void Visit(ref Compiler compiler, AstNode node, State state);
  }
}