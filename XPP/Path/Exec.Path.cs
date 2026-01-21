
using System;
using System.Collections.Generic;
using XPP.Doc;

namespace XPP.Path;

public class ExecCtxSet
{
  public static readonly ExecCtxSet One = Constant(1);

  public static ExecCtxSet Constant(int length) => new()
  {
    length = length,
    constant = true,
  };

  private int length = 0;
  private bool constant = false;
  public int Length => length;

  public int Add()
  {
    if (constant) return 0;
    return length++;
  }

  private ExecCtxSet next = null;
  public ExecCtxSet Next => constant ? this : next ??= new();
}

public readonly struct ExecPathCtx(
  XPNavigator Nav, ExecCtxSet Set, int? Position = null)
{
  public readonly XPNavigator Nav = Nav;
  public readonly ExecCtxSet Set = Set;
  public readonly int Position = Position ?? Set.Add();
}

public abstract class ExecPathOp(ExecPathOp Parent, bool Forward)
{
  public readonly ExecPathOp Parent = Parent;
  public readonly bool Forward = Forward;
  protected XPNavigator navCtx;

  protected int Compare(XPNavigator left, XPNavigator right)
  {
    var cmp = left.CompareTo(right);
    return Forward ? cmp : -cmp;
  }

  public virtual void Init(XPNavigator navCtx)
  {
    this.navCtx = navCtx;
    Parent?.Init(navCtx);
  }
  public abstract bool Next(out ExecPathCtx next);

  public Iterator GetEnumerator() => new(this);
  public struct Iterator(ExecPathOp op)
  {
    private readonly ExecPathOp op = op;
    private ExecPathCtx next;

    public bool MoveNext() => op.Next(out next);
    public XPNodeRef Current => next.Nav.Node;
  }
}

public abstract class ExecPathOp1To1(ExecPathOp Parent)
  : ExecPathOp(Parent, Parent.Forward)
{
  public override bool Next(out ExecPathCtx next)
  {
    while (true)
    {
      if (!Parent.Next(out var pctx))
      {
        next = default;
        return false;
      }
      if (!Map(pctx, out var nextNav))
        continue;
      next = new(nextNav, pctx.Set.Next);
      return true;
    }
  }

  protected abstract bool Map(ExecPathCtx from, out XPNavigator to);
}

public abstract class ExecPathOpSingle() : ExecPathOp(null, true)
{
  private bool first = false;
  public override void Init(XPNavigator navCtx)
  {
    base.Init(navCtx);
    first = true;
  }

  public override bool Next(out ExecPathCtx next)
  {
    if (!first)
    {
      next = default;
      return false;
    }
    next = new(SingleNav, ExecCtxSet.One);
    return !(first = false);
  }

  protected abstract XPNavigator SingleNav { get; }
}

public class ExecPathOpContext() : ExecPathOpSingle()
{
  protected override XPNavigator SingleNav => navCtx;
}

public class ExecPathOpRoot() : ExecPathOpSingle
{
  protected override XPNavigator SingleNav => navCtx.Root();
}

public class ExecPathOpReverse(ExecPathOp Parent) : ExecPathOp(Parent, !Parent.Forward)
{
  private readonly List<ExecPathCtx> results = [];
  private bool first = false;

  public override void Init(XPNavigator navCtx)
  {
    base.Init(navCtx);
    first = true;
    results.Clear();
  }

  public override bool Next(out ExecPathCtx next)
  {
    if (first)
    {
      while (Parent.Next(out var pnext))
        results.Add(pnext);
    }
    var count = results.Count;
    if (count == 0)
    {
      next = default;
      return false;
    }
    next = results[^1];
    results.RemoveAt(count - 1);
    return true;
  }
}

public class ExecPathOpDedupe(ExecPathOp Parent) : ExecPathOp1To1(Parent)
{
  private XPNodeRef last;

  public override void Init(XPNavigator navCtx)
  {
    base.Init(navCtx);
    last = XPNodeRef.Invalid;
  }

  protected override bool Map(ExecPathCtx from, out XPNavigator to)
  {
    if (!last.SameAs(from.Nav.Node))
    {
      to = from.Nav;
      last = to.Node;
      return true;
    }
    to = default;
    return false;
  }
}

public class ExecPathOpUnion(ExecPathOp Left, ExecPathOp Right) : ExecPathOp(null, true)
{
  public readonly ExecPathOp Left = Left;
  public readonly ExecPathOp Right = Right;

  private bool first = false;
  private bool leftDone = false;
  private ExecPathCtx leftNext;
  private bool rightDone = false;
  private ExecPathCtx rightNext;

  public override void Init(XPNavigator navCtx)
  {
    if (!Left.Forward) throw new InvalidOperationException($"{Left.GetType().Name}");
    if (!Right.Forward) throw new InvalidOperationException($"{Right.GetType().Name}");
    base.Init(navCtx);
    Left.Init(navCtx);
    Right.Init(navCtx);
    first = true;
  }

  public override bool Next(out ExecPathCtx next)
  {
    if (first)
    {
      leftDone = !Left.Next(out leftNext);
      rightDone = !Right.Next(out rightNext);
      first = false;
    }

    if (leftDone && rightDone)
    {
      next = default;
      return false;
    }

    if (leftDone)
    {
      next = rightNext;
      rightDone = !Right.Next(out rightNext);
      return true;
    }

    if (rightDone)
    {
      next = leftNext;
      leftDone = !Left.Next(out leftNext);
      return true;
    }

    if (Compare(leftNext.Nav, rightNext.Nav) <= 0)
    {
      next = leftNext;
      leftDone = !Left.Next(out leftNext);
    }
    else
    {
      next = rightNext;
      rightDone = !Right.Next(out rightNext);
    }
    return true;
  }
}

public class ExecPathOpExpr(ExecExprOp Expr) : ExecPathOp(null, true)
{
  public readonly ExecExprOp Expr = Expr;
  private bool first = false;
  private ExecPathOp nodeSet;
  private ExecCtxSet set;

  public override void Init(XPNavigator navCtx)
  {
    base.Init(navCtx);
    first = true;
    set = new();
  }

  public override bool Next(out ExecPathCtx next)
  {
    if (first)
    {
      var val = Expr.Value(new(navCtx, ExecCtxSet.One));
      if (val.Type != XPValueType.NodeSet)
        throw new InvalidOperationException(
          $"Expression must produce NodeSet, not {val.Type}");
      nodeSet = val.NodeSet;
      first = false;
    }
    if (!nodeSet.Next(out var pnext))
    {
      next = default;
      return false;
    }
    next = new(pnext.Nav, set);
    return true;
  }
}

public class ExecPathOpNodeType(ExecPathOp Parent, NodeType Type) : ExecPathOp1To1(Parent)
{
  public readonly NodeType Type = Type;

  protected override bool Map(ExecPathCtx from, out XPNavigator to)
  {
    if (from.Nav.Type() == Type)
    {
      to = from.Nav;
      return true;
    }
    to = default;
    return false;
  }
}

public class ExecPathOpNameTest(ExecPathOp Parent, XPType PType, string Ns, string Name)
  : ExecPathOp1To1(Parent)
{
  public readonly XPType PType = PType;
  public readonly string Ns = Ns;
  public readonly string Name = Name;

  protected override bool Map(ExecPathCtx from, out XPNavigator to)
  {
    var nav = from.Nav;
    if (nav.Node.Type == PType && (Ns.Length, Name.Length) switch
    {
      (_, > 0) => nav.HasNs(Ns) && nav.HasName(Name),
      ( > 0, 0) => nav.HasNs(Ns),
      _ => true,
    })
    {
      to = nav;
      return true;
    }
    to = default;
    return false;
  }
}

public class ExecPathOpFilter(ExecPathOp Parent, ExecExprOp Filter)
  : ExecPathOp1To1(Parent)
{
  public readonly ExecExprOp Filter = Filter;

  protected override bool Map(ExecPathCtx from, out XPNavigator to)
  {
    var val = Filter.Value(from);
    if (val.Type switch
    {
      XPValueType.Number => from.Position + 1 == val.Number,
      _ => val.BoolValue,
    })
    {
      to = from.Nav;
      return true;
    }
    to = default;
    return false;
  }
}