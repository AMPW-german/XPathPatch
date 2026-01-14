
using System;
using System.Runtime.InteropServices;
using XPP.Utils;

namespace XPP.Path;

public readonly struct PathOp(PathOpType type)
{
  public readonly PathOpType Type = type;

  private readonly int v0;
  private readonly int v1;
  private readonly int v2;
  private readonly int v3;

  // Union
  public (int, int) Paths { get => (v0, v1); init => (v0, v1) = value; }

  // Axis
  public AxisType Axis { get => (AxisType)v0; init => v0 = (int)value; }

  // NodeType
  public NodeType NodeType { get => (NodeType)v0; init => v0 = (int)value; }

  // NameTest
  public Range Ns { get => v0..v1; init => (v0, v1) = (value.Start.Value, value.End.Value); }
  public Range Name { get => v2..v3; init => (v2, v3) = (value.Start.Value, value.End.Value); }

  // Filter
  public int Filter { get => v0; init => v0 = value; }

  // Normalize
  public bool Dedupe { get => v0 != 0; init => v0 = value ? 1 : 0; }
}

public readonly struct ValOp(ValOpType type, int left = -1, int right = -1)
{
  public readonly ValOpType Type = type;
  public readonly int Left = left;
  public readonly int Right = right;
  public readonly CompileValue Value;

  public Range Args => Left..Right;
  public Range Name => Value.String;
  public LibraryFunc Func => (LibraryFunc)Value.NodeSet;

  // Func
  public ValOp(ValOpType type, LibraryFunc func, Range args) : this(type, args.Start.Value, args.End.Value) =>
    Value = new() { NodeSet = (int)func };
  // UserFunc
  public ValOp(ValOpType type, Range name, Range args) : this(type, args.Start.Value, args.End.Value) =>
    Value = new() { String = name };
  // Number, String, Variable (name in String)
  public ValOp(ValOpType type, CompileValue value) : this(type) => Value = value;
}

[StructLayout(LayoutKind.Explicit)]
public struct CompileValue
{
  [FieldOffset(0)]
  public bool Bool;
  [FieldOffset(0)]
  public double Number;
  [FieldOffset(0)]
  public Range String;
  [FieldOffset(0)]
  public int NodeSet;
}

public ref struct Compiler
{
  public static void Compile(
    ReadOnlySpan<char> source, ReadOnlySpan<AstNode> nodes,
    out ReadOnlySpan<PathOp> paths, out ReadOnlySpan<ValOp> vals, out ReadOnlySpan<char> data)
  {
    var compiler = new Compiler(source, nodes);
    compiler.Compile();

    paths = compiler.paths.Span;
    vals = compiler.vals.Span;
    data = compiler.data.Span;
  }

  // compile state of an AST node
  private struct State()
  {
    public int PathStart = -1;
    public int PathIdx = -1;
    public int PathPrev = -1;
    public int PathNext = -1;
    public StateFlags Flags = 0;
    public int ValIdx = -1;
  }

  [Flags]
  private enum StateFlags
  {
    IsOrdered = 1,
    IsDeduped = 2,
    IsNorm = IsOrdered | IsDeduped,
  }

  private class CompileBuf<O, T>() : ThreadBuf<O, T>(XPath.MAX_LENGTH) where O : CompileBuf<O, T>, new();
  private class PathBuf : CompileBuf<PathBuf, PathOp>;
  private class ValBuf : CompileBuf<ValBuf, ValOp>;
  private class DataBuf : CompileBuf<DataBuf, char>;
  private class StateBuf : CompileBuf<StateBuf, State>;
  private class QueueBuf : CompileBuf<QueueBuf, int>;

  private readonly ReadOnlySpan<char> source;
  private readonly ReadOnlySpan<AstNode> nodes;

  // result output bufs
  private SpanBuf<PathOp> paths;
  private SpanBuf<ValOp> vals;
  private SpanBuf<char> data;

  // compile states by node
  private readonly Span<State> states;
  // compile queue (<0 is ~pathIdx)
  private SpanBuf<int> queue;
  private int queuePos = 0;

  private Compiler(ReadOnlySpan<char> source, ReadOnlySpan<AstNode> nodes)
  {
    this.source = source;
    this.nodes = nodes;

    paths = PathBuf.Buf;
    vals = ValBuf.Buf;
    data = DataBuf.Buf;

    states = StateBuf.Span;
    states[..nodes.Length].Fill(new());

    queue = QueueBuf.Buf;
  }

  private void Compile()
  {
    QueueExpr(nodes.Length - 1);
    DrainQueue();
  }

  private void DrainQueue()
  {
    while (queuePos < queue.Length)
    {
      var idx = queue[queuePos++];
      if (idx < 0)
        CompilePath(~idx);
      else
        CompileExpr(idx);
    }
  }

  private void CompileExpr(int index)
  {
    var compile = new ExprCompilePass();
    var backfill = new ExprBackfillPass();

    WalkExpr(index, ref compile);
    DrainQueue();
    WalkExpr(index, ref backfill);
  }

  private void CompilePath(int index)
  {
    states[index].PathStart = paths.Length;

    var mark = new PathMarkPass();
    var compile = new PathCompilePass();
    var backfill = new PathBackfillPass();

    WalkPath(index, ref mark);
    mark.FinishMark(ref this);

    WalkPath(index, ref compile);
    if (!paths[^1].Type.IsRoot)
    {
      compile.EnsureAxis(ref this);
      AddPath(new(PathOpType.Context));
    }
    DrainQueue();
    WalkPath(index, ref backfill);
  }

  private void QueuePath(int index) => queue.Add(~index);
  private void QueueExpr(int index) => queue.Add(index);

  private int AddPath(PathOp op) => paths.Add(op);
  private void AddExpr(ref State state, ValOp op)
  {
    if (state.ValIdx != -1)
      vals[state.ValIdx] = op;
    else
      state.ValIdx = vals.Add(op);
  }

  private int ReserveExpr(int idx)
  {
    ref var state = ref states[idx];
    if (state.ValIdx != -1)
      return state.ValIdx;
    return state.ValIdx = vals.Add(default);
  }

  private int ValOpIndex(int idx)
  {
    ref State state = ref states[idx];
    if (state.ValIdx == -1)
      throw new InvalidOperationException($"{idx}");
    return state.ValIdx;
  }

  private int PathStart(int idx)
  {
    ref State state = ref states[idx];
    if (state.PathStart == -1)
      throw new InvalidOperationException($"{idx}");
    return state.PathStart;
  }

  // Initial expression pass. compile nodes and queue paths
  private readonly struct ExprCompilePass : ICompilerPass
  {
    public void Visit(ref Compiler compiler, int idx, ref readonly AstNode node, ref State state)
    {
      switch (node.Type)
      {
        case AstType.Root:
        case AstType.Sep:
        case AstType.Axis:
        case AstType.NodeTest:
        case AstType.ProcType:
        case AstType.PathFilter:
        case AstType.Union:
          compiler.ReserveExpr(idx);
          compiler.QueuePath(idx);
          break;
        case AstType.Value:
          compiler.AddExpr(ref state, node.Token.Type switch
          {
            TokenType.VarRef => new(ValOpType.Variable, compiler.AddDataVal(node.Token, 1, 0)),
            TokenType.String => new(ValOpType.String, compiler.AddDataVal(node.Token, 1, 1)),
            TokenType.Number => new(ValOpType.Number,
              new CompileValue { Number = double.Parse(compiler.TokStr(node.Token)) }),
            _ => throw new InvalidOperationException($"{node.Token.Type}"),
          });
          break;
        case AstType.FuncCall:
          var func = XPath.ParseLibraryFunc(compiler.TokStr(node.Token));
          // reserve spot for func op, then reserve args
          var argStart = 1 + compiler.ReserveExpr(idx);
          var argCount = ReserveArgs(ref compiler, node.Child0);
          var argRange = argStart..(argStart + argCount);
          if (func != LibraryFunc.Invalid)
            compiler.AddExpr(ref state, new(ValOpType.Func, func, argRange));
          else
            compiler.AddExpr(ref state, new(ValOpType.UserFunc, compiler.AddData(node.Token), argRange));
          break;
        case AstType.ArgList: break;
        case AstType.BoolOp:
        case AstType.CompareOp:
        case AstType.MathOp:
          compiler.ReserveExpr(idx);
          compiler.ReserveExpr(node.Child0);
          compiler.ReserveExpr(node.Child1);
          compiler.AddExpr(ref state, new(
            node.Token.Type.AsValOp,
            compiler.ValOpIndex(node.Child0),
            compiler.ValOpIndex(node.Child1)));
          break;
        case AstType.Negate:
          compiler.ReserveExpr(idx);
          compiler.ReserveExpr(node.Child0);
          compiler.AddExpr(ref state, new(
            node.Token.Type.AsValOp,
            compiler.ValOpIndex(node.Child0)));
          break;
      }
    }

    private static int ReserveArgs(ref Compiler compiler, int idx)
    {
      if (idx == -1)
        return 0;
      ref readonly AstNode node = ref compiler.nodes[idx];
      if (node.Type == AstType.ArgList)
        return ReserveArgs(ref compiler, node.Child0) + ReserveArgs(ref compiler, node.Child1);
      compiler.ReserveExpr(idx);
      return 1;
    }
  }

  // backfill path ops after paths are compiled
  private readonly struct ExprBackfillPass : ICompilerPass
  {
    public void Visit(ref Compiler compiler, int idx, ref readonly AstNode node, ref State state)
    {
      switch (node.Type)
      {
        case AstType.Root:
        case AstType.Sep:
        case AstType.Axis:
        case AstType.NodeTest:
        case AstType.ProcType:
        case AstType.PathFilter:
        case AstType.Union:
          compiler.AddExpr(ref state, new(ValOpType.Path, compiler.PathStart(idx)));
          break;
      }
    }
  }

  // mark path next nodes and flags
  private struct PathMarkPass() : ICompilerPass
  {
    private int last = -1;

    public void Visit(ref Compiler compiler, int idx, ref readonly AstNode node, ref State state)
    {
      if (last != -1)
      {
        compiler.states[last].PathNext = idx;
        state.PathPrev = last;
      }
      last = idx;
    }

    public void FinishMark(ref Compiler compiler)
    {
      var idx = last;
      StateFlags lastFlags = StateFlags.IsNorm;
      while (idx != -1)
      {
        ref readonly var node = ref compiler.nodes[idx];
        ref var state = ref compiler.states[idx];

        // set flags based on what the previous node will receive
        state.Flags = node.Type switch
        {
          AstType.Root => StateFlags.IsNorm,
          AstType.Sep => StateFlags.IsNorm,
          AstType.Axis when compiler.ParseAxisType(node.Token) is AxisType axis =>
            FlagOrdered(axis.IsForward) | FlagDeduped(!axis.CanDupe && !lastFlags.HasFlag(StateFlags.IsDeduped)),
          AstType.NodeTest => lastFlags,
          AstType.ProcType => lastFlags,
          AstType.PathFilter => lastFlags,
          AstType.Union => StateFlags.IsNorm,
          _ => throw new InvalidOperationException($"{node.Type}"),
        };

        idx = state.PathPrev;
        lastFlags = state.Flags;
      }
    }

    private static StateFlags FlagOrdered(bool ordered) => ordered ? StateFlags.IsOrdered : 0;
    private static StateFlags FlagDeduped(bool ordered) => ordered ? StateFlags.IsDeduped : 0;
  }
  private AxisType ParseAxisType(Token tok) => tok.Type switch
  {
    TokenType.AxisName => XPath.ParseAxisType(TokStr(tok)),
    TokenType.Attr => AxisType.Attribute,
    TokenType.Self => AxisType.Self,
    TokenType.Parent => AxisType.Parent,
    _ => throw new InvalidOperationException($"{tok.Type}"),
  };

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

    private void EnsureNorm(ref Compiler compiler, StateFlags nextFlags)
    {
      if (nextFlags == StateFlags.IsNorm)
        return;
      compiler.AddPath(new(PathOpType.Normalize) { Dedupe = !nextFlags.HasFlag(StateFlags.IsDeduped) });
    }

    public void Visit(ref Compiler compiler, int idx, ref readonly AstNode node, ref State state)
    {
      if (state.PathPrev == -1)
        EnsureNorm(ref compiler, state.Flags);

      var nextFlags = state.PathNext != -1 ? compiler.states[state.PathNext].Flags : StateFlags.IsNorm;
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
          EnsureNorm(ref compiler, nextFlags);
          hasAxis = false;
          break;
        case AstType.Axis:
          compiler.AddPath(new(PathOpType.Axis)
          {
            Axis = node.Token.Type switch
            {
              TokenType.AxisName => XPath.ParseAxisType(compiler.TokStr(node.Token)),
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
            case TokenType.NtAny: break;
            case TokenType.NtAnyNs:
              compiler.AddPath(new(PathOpType.NameTest) { Ns = compiler.AddNs(node.Token), Name = 0..0 });
              break;
            case TokenType.NtName:
              compiler.AddPath(new(PathOpType.NameTest)
              {
                Ns = compiler.AddNs(node.Token),
                Name = compiler.AddName(node.Token)
              });
              break;
            case TokenType.NodeType:
              var ntype = XPath.ParseNodeType(compiler.TokStr(node.Token));
              if (ntype != NodeType.Node)
                compiler.AddPath(new(PathOpType.NodeType) { NodeType = ntype });
              break;
            default:
              throw new InvalidOperationException($"{node.Type} {node.Token.Type}");
          }
          hasAxis = false;
          break;
        case AstType.ProcType:
          compiler.AddPath(new(PathOpType.NameTest) { Name = compiler.AddData(node.Token) });
          compiler.AddPath(new(PathOpType.NodeType) { NodeType = NodeType.ProcessingInstruction });
          hasAxis = false;
          break;
        case AstType.PathFilter:
          // filter index will be backfilled
          state.PathIdx = compiler.AddPath(new(PathOpType.Filter));
          compiler.QueueExpr(node.Child1);
          hasAxis = false;
          break;
        case AstType.Union:
          // path indices will be backfilled
          state.PathIdx = compiler.AddPath(new(PathOpType.Union));
          compiler.QueuePath(node.Child0);
          compiler.QueuePath(node.Child1);
          hasAxis = false;
          break;
        default:
          throw new InvalidOperationException($"{node.Type}");
      }
    }
  }

  // backfill filter and union target indices
  private struct PathBackfillPass() : ICompilerPass
  {
    public void Visit(ref Compiler compiler, int idx, ref readonly AstNode node, ref State state)
    {
      switch (node.Type)
      {
        case AstType.PathFilter:
          compiler.paths[state.PathIdx] = new(PathOpType.Filter) { Filter = compiler.ValOpIndex(node.Child1) };
          break;
        case AstType.Union:
          compiler.paths[state.PathIdx] = new(PathOpType.Union)
          {
            Paths = (compiler.PathStart(node.Child0), compiler.PathStart(node.Child1))
          };
          break;
      }
    }
  }

  private ReadOnlySpan<char> TokStr(Token tok) => source[tok.Data];

  private Range AddData(Token tok) => data.AddRange(TokStr(tok));

  private Range AddNs(Token tok)
  {
    var str = TokStr(tok);
    var idx = str.IndexOf(':');
    if (idx == -1)
      return 0..0;
    return data.AddRange(str[..idx]);
  }

  private Range AddName(Token tok)
  {
    var str = TokStr(tok);
    var idx = str.IndexOf(':');
    return data.AddRange(str[(idx + 1)..]);
  }

  private CompileValue AddDataVal(Token tok, int strim, int etrim)
  {
    var str = TokStr(tok);
    return new() { String = data.AddRange(str[strim..^etrim]) };
  }

  private void WalkExpr<T>(int idx, ref T visitor) where T : struct, ICompilerPass, allows ref struct
  {
    ref readonly var node = ref nodes[idx];
    switch (node.Type)
    {
      // path nodes start a new path. don't walk, just visit
      case AstType.Root:
      case AstType.Sep:
      case AstType.Axis:
      case AstType.NodeTest:
      case AstType.ProcType:
      case AstType.PathFilter:
      case AstType.Union:
      case AstType.Value: // Value is a leaf
        visitor.Visit(ref this, idx, in node, ref states[idx]);
        break;

      case AstType.FuncCall:
        visitor.Visit(ref this, idx, in node, ref states[idx]);
        if (node.Child0 != -1)
          WalkExpr(node.Child0, ref visitor);
        break;
      case AstType.ArgList:
      case AstType.BoolOp:
      case AstType.CompareOp:
      case AstType.MathOp:
        visitor.Visit(ref this, idx, in node, ref states[idx]);
        WalkExpr(node.Child0, ref visitor);
        WalkExpr(node.Child1, ref visitor);
        break;
      case AstType.Negate:
        visitor.Visit(ref this, idx, in node, ref states[idx]);
        WalkExpr(node.Child0, ref visitor);
        break;
      default:
        throw new InvalidOperationException($"{node.Type}");
    }
  }

  private void WalkPath<T>(int idx, ref T visitor) where T : struct, ICompilerPass, allows ref struct
  {
    ref readonly var node = ref nodes[idx];
    switch (node.Type)
    {
      case AstType.Root:
      case AstType.Axis:
        if (node.Child0 != -1)
          WalkPath(node.Child0, ref visitor);
        visitor.Visit(ref this, idx, in node, ref states[idx]);
        break;
      case AstType.Sep:
        WalkPath(node.Child1, ref visitor);
        visitor.Visit(ref this, idx, in node, ref states[idx]);
        WalkPath(node.Child0, ref visitor);
        break;
      case AstType.NodeTest:
      case AstType.ProcType:
      case AstType.Union: // union paths are separate
        visitor.Visit(ref this, idx, in node, ref states[idx]);
        break;
      case AstType.PathFilter:
        visitor.Visit(ref this, idx, in node, ref states[idx]);
        WalkPath(node.Child0, ref visitor); // walk parent path, but not predicate expression
        break;
      // expression nodes should not be direct children of any path node except Filter
      case AstType.Value:
      case AstType.FuncCall:
      case AstType.ArgList:
      case AstType.BoolOp:
      case AstType.CompareOp:
      case AstType.MathOp:
      case AstType.Negate:
      default:
        throw new InvalidOperationException($"Path {node.Type}");
    }
  }

  private interface ICompilerPass
  {
    public void Visit(ref Compiler compiler, int idx, ref readonly AstNode node, ref State state);
  }
}