
using System;
using XPP.Doc;
using XPP.Utils;

namespace XPP.Path;

public ref partial struct Exec
{
  private struct PathContext : IComparable<PathContext>
  {
    public XPNavigator Nav;
    public int AxisRoot;
    public int Position;
    public int Count;

    public int CompareTo(PathContext other) => Nav.CompareTo(other.Nav);
  }

  private struct PathState
  {
    public XPNavigator Base;

    public int Depth;
    public AxPreState PState;
  }
  private enum AxPreState { PrevSib, LastChild, Parent }

  private struct PathBuffers
  {
    public PooledAppendList<PathContext> Nodes;
    public PooledAppendList<PathContext> PrevNodes;
    public PooledAppendList<int> Counts;
    public PooledAppendList<PathState> States;

    public void Init()
    {
      Nodes ??= [];
      PrevNodes ??= [];
      Counts ??= [];
      States ??= [];
    }

    public void Swap()
    {
      (Nodes, PrevNodes) = (PrevNodes, Nodes);
      Nodes.Length = 0;
      States.Length = 0;
    }
  }

  private ref struct PathStep(
    ref readonly PathOp Op, ref PathBuffers Buffers, XPNavigator Ctx)
  {
    public ref readonly PathOp Op = ref Op;
    public ref PathBuffers Buffers = ref Buffers;
    public readonly XPNavigator Ctx = Ctx;
    public int Index = 0;

    public ref PathContext Prev => ref Buffers.PrevNodes[Index];
    public ref PathState State => ref Buffers.States[Prev.AxisRoot];
    public bool Done => Index >= Buffers.PrevNodes.Length;

    public void Init(bool reindex)
    {
      var counts = Buffers.Counts;
      var prevNodes = Buffers.PrevNodes;
      switch (Op.Mode)
      {
        case PathOpMode.Linear: break;
        case PathOpMode.InsertExpand: break;
        default: throw new InvalidOperationException($"{Op.Mode}");
      }
      if (reindex)
      {
        counts.Length = 0;
        for (var i = 0; i < prevNodes.Length; i++)
        {
          ref var node = ref prevNodes[i];
          node.AxisRoot = i;
          node.Position = 0;
          counts.Add(0);
        }
      }
      else
      {
        for (var i = 0; i < counts.Length; i++)
          counts[i] = 0;
      }
    }

    public void Add(PathContext ctx)
    {
      if (Op.Dedupe && Buffers.Nodes.Length > 0
          && Buffers.Nodes[^1].Nav.SameAs(ctx.Nav))
        return;
      Buffers.Nodes.Add(new()
      {
        Nav = ctx.Nav,
        AxisRoot = ctx.AxisRoot,
        Position = Buffers.Counts[ctx.AxisRoot]++,
      });
    }

    public void AddRoot(XPNavigator nav)
    {
      if (Buffers.Counts.Length == 0)
        Buffers.Counts.Add(0);
      Buffers.Nodes.Add(new()
      {
        Nav = nav,
        AxisRoot = 0,
        Position = Buffers.Counts[0]++,
        Count = 1,
      });
    }

    public void UpdateIndex()
    {
      switch (Op.Mode)
      {
        case PathOpMode.Linear: break;
        case PathOpMode.InsertExpand:
          var prevs = Buffers.PrevNodes;
          ref var node = ref prevs[Index];
          var fwd = Op.Forward;
          for (var i = Index; i < prevs.Length - 1; i++)
          {
            ref var next = ref prevs[i + 1];
            var cmp = node.CompareTo(next);
            if ((fwd && cmp <= 0) || (!fwd && cmp >= 0))
              break;
            (node, next) = (next, node);
            node = ref next;
          }
          break;
        default: throw new InvalidOperationException($"{Op.Mode}");
      }
    }

    public void FinishIndex()
    {
      switch (Op.Mode)
      {
        case PathOpMode.Linear: Index++; break;
        case PathOpMode.InsertExpand: Index++; break;
        default: throw new InvalidOperationException($"{Op.Mode}");
      }
    }
  }

  public void Dispose()
  {
    for (var i = 0; i < pathBufs.Length; i++)
    {
      ref var bufs = ref pathBufs[i];
      bufs.Nodes?.Dispose();
      bufs.PrevNodes?.Dispose();
      bufs.Counts?.Dispose();
      bufs.States?.Dispose();
    }
  }

  private void ExecPath(int entry, XPNavigator ctx)
  {
    ref var bufs = ref pathBufs[path.Paths[entry].RootNum];
    bufs.Init();

    var pathLength = path.Paths[entry].Length;

    for (var i = 0; i < pathLength; i++)
    {
      bufs.Swap();
      var step = new PathStep(in path.Paths[entry + i], ref bufs, ctx);
      ExecPathStep(ref step);
    }
  }

  private void ExecPathStep(ref PathStep step)
  {
    switch (step.Op.Type)
    {
      case PathOpType.Context: PathStepContext(ref step); break;
      case PathOpType.Root: PathStepRoot(ref step); break;
      case PathOpType.Union: PathStepUnion(ref step); break;
      case PathOpType.Expr: PathStepExpr(ref step); break;
      case PathOpType.Axis: PathStepAxis(ref step); break;
      case PathOpType.NodeType: PathStepNodeType(ref step); break;
      case PathOpType.NameTest: PathStepNameTest(ref step); break;
      case PathOpType.Filter: PathStepFilter(ref step); break;
      default: throw new InvalidOperationException($"{step.Op.Type}");
    }
    var nodes = step.Buffers.Nodes;
    var counts = step.Buffers.Counts;
    if (step.Op.Reverse)
    {
      for (var (i, j) = (0, nodes.Length - 1); i < j; i++, j--)
      {
        ref var ni = ref nodes[i];
        ref var nj = ref nodes[j];
        (ni, nj) = (nj, ni);
      }
    }
    for (var i = 0; i < nodes.Length; i++)
      nodes[i].Count = counts[nodes[i].AxisRoot];
  }

  private void PathStepContext(ref PathStep step)
  {
    step.AddRoot(step.Ctx);
  }

  private void PathStepRoot(ref PathStep step)
  {
    step.AddRoot(step.Ctx.Root());
  }

  private void PathStepUnion(ref PathStep step)
  {
    ref readonly var op = ref step.Op;
    ExecPath(op.UnionL, step.Ctx);
    ExecPath(op.UnionR, step.Ctx);
    var lnodes = PathResult(op.UnionL);
    var rnodes = PathResult(op.UnionR);

    var li = 0;
    var ri = 0;

    while (li < lnodes.Length && ri < rnodes.Length)
    {
      var ln = lnodes[li].Nav;
      var rn = rnodes[ri].Nav;
      var cmp = ln.CompareTo(rn);
      step.AddRoot(cmp <= 0 ? ln : rn);
      if (cmp <= 0) li++;
      if (cmp >= 0) ri++;
    }

    while (li < lnodes.Length) step.AddRoot(lnodes[li++].Nav);
    while (ri < rnodes.Length) step.AddRoot(rnodes[ri++].Nav);
  }

  private void PathStepExpr(ref PathStep step)
  {
    ref readonly var op = ref step.Op;
    var val = GetValue(op.Expr, new() { Nav = step.Ctx });
    if (val.Type is not XPValueType.NodeSet)
      throw new InvalidOperationException(
        $"Expression must produce NodeSet, not {val.Type}");
    foreach (var node in PathResult(val.NodeSet))
      step.AddRoot(node.Nav);
  }

  private void PathStepAxis(ref PathStep step)
  {
    var axis = step.Op.Axis;
    var prevNodes = step.Buffers.PrevNodes;
    var states = step.Buffers.States;
    for (var i = 0; i < prevNodes.Length; i++)
    {
      ref var node = ref prevNodes[i];
      if (!AxisInit(axis, ref node.Nav, out var state))
        continue;
      var idx = states.Length;
      prevNodes[idx] = node;
      states.Add(state);
    }
    prevNodes.Length = states.Length;
    step.Init(true);
    while (!step.Done)
    {
      ref var node = ref step.Prev;
      ref var state = ref step.State;
      step.Add(node);
      if (AxisNext(axis, ref node.Nav, ref state))
        step.UpdateIndex();
      else
        step.FinishIndex();
    }
  }

  private void PathStepNodeType(ref PathStep step)
  {
    ref readonly var op = ref step.Op;
    step.Init(false);
    while (!step.Done)
    {
      ref var node = ref step.Prev;
      if (op.NodeType == NodeType.Node || node.Nav.Type() == op.NodeType)
        step.Add(node);

      step.FinishIndex();
    }
  }

  private void PathStepNameTest(ref PathStep step)
  {
    ref readonly var op = ref step.Op;
    step.Init(false);
    while (!step.Done)
    {
      ref var node = ref step.Prev;
      if (node.Nav.Node.Type == op.PType && (op.Ns.Length, op.Name.Length) switch
      {
        (_, > 0) => node.Nav.HasNs(op.Ns) && node.Nav.HasName(op.Name),
        ( > 0, 0) => node.Nav.HasNs(op.Ns),
        _ => true,
      })
        step.Add(node);

      step.FinishIndex();
    }
  }

  private void PathStepFilter(ref PathStep step)
  {
    ref readonly var op = ref step.Op;
    step.Init(false);
    while (!step.Done)
    {
      ref var node = ref step.Prev;
      var val = GetValue(op.Expr, node);
      if (val.Type switch
      {
        XPValueType.Number => node.Position + 1 == val.Number,
        _ => BoolValue(val),
      })
        step.Add(node);

      step.FinishIndex();
    }
  }
}