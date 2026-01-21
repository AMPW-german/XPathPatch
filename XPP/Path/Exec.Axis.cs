
using System;
using XPP.Doc;
using XPP.Utils;

namespace XPP.Path;

public abstract class ExecPathOpAxis(ExecPathOp Parent, AxisType Type)
  : ExecPathOp(Parent, Type.IsForward)
{
  public static ExecPathOp Make(ExecPathOp parent, AxisType axis) => axis switch
  {
    AxisType.Ancestor => new ExecPathOpAxisAncestor(parent),
    AxisType.AncestorOrSelf => new ExecPathOpAxisAncestorOrSelf(parent),
    AxisType.Attribute => new ExecPathOpAxisAttribute(parent),
    AxisType.Child => new ExecPathOpAxisChild(parent),
    AxisType.Descendant => new ExecPathOpAxisDescendant(parent),
    AxisType.DescendantOrSelf => new ExecPathOpAxisDescendantOrSelf(parent),
    AxisType.Following => new ExecPathOpAxisFollowing(parent),
    AxisType.FollowingSibling => new ExecPathOpAxisFollowingSibling(parent),
    AxisType.Namespace => new ExecPathOpAxisNamespace(parent),
    AxisType.Parent => new ExecPathOpAxisParent(parent),
    AxisType.Preceding => new ExecPathOpAxisPreceding(parent),
    AxisType.PrecedingSibling => new ExecPathOpAxisPrecedingSibling(parent),
    AxisType.Self => new ExecPathOpAxisSelf(parent),
    _ => throw new InvalidOperationException($"{axis}"),
  };

  public readonly AxisType Type = Type;

  protected enum AxPreState { PrevSib, LastChild, Parent }
  protected struct AxisState
  {
    public XPNavigator Base;
    public bool Valid;
    public XPNavigator Cur;
    public ExecCtxSet Set;
    // state for preceding
    public AxPreState PState;
    public int Depth;
  }

  private readonly AppendList<AxisState> istack = Type.CanInterleave ? [] : null;
  private bool stackFirst = false;

  private AxisState nextState;
  private AxisState parentState;
  private bool first = false;

  public override void Init(XPNavigator navCtx)
  {
    if (Forward != Parent.Forward)
      throw new InvalidOperationException();
    base.Init(navCtx);
    first = true;
    nextState.Valid = parentState.Valid = false;
  }

  public override bool Next(out ExecPathCtx next)
  {
    if (first)
    {
      first = false;
      parentState.Valid = true;
      FillParent();
      nextState = parentState;
      if (!nextState.Valid)
      {
        next = default;
        return false;
      }
      parentState.Valid = true;
      FillParent();
    }

    if (!nextState.Valid)
    {
      next = default;
      return false;
    }

    next = new(nextState.Cur, nextState.Set.Next);
    UpdateNext();
    return true;
  }

  private void UpdateNext()
  {
    ref var parent = ref parentState;
    ref var next = ref nextState;
    NextCtx(ref next);
    if (!next.Valid)
    {
      PrepareNext();
      return;
    }

    if (istack == null)
      return;
    var curNext = next.Cur;
    if (stackFirst)
    {
      ref var top = ref istack[^1];
      if (Compare(curNext, top.Cur) <= 0)
        return;
      (next, top) = (top, next);
    }
    else if (parent.Valid)
    {
      if (Compare(next.Cur, parent.Cur) <= 0)
        return;
      istack.Add(next);
      next = parent;
      FillParent();
    }
    stackFirst = istack.Length > 0 &&
      (!parent.Valid || Compare(curNext, parent.Cur) <= 0);
  }

  private void PrepareNext()
  {
    if (istack == null)
    {
      nextState = parentState;
      FillParent();
      return;
    }
    if (stackFirst)
    {
      nextState = istack[^1];
      istack.Length--;
    }
    else
    {
      nextState = parentState;
      FillParent();
    }
    ref var parent = ref parentState;
    if (istack.Length > 0)
      stackFirst = !parent.Valid || Compare(istack[^1].Cur, parent.Cur) <= 0;
    else
      stackFirst = false;
  }

  private void FillParent()
  {
    ref var pstate = ref parentState;
    if (!pstate.Valid)
      return;
    pstate.Valid = false;
    pstate.Base = default;
    while (!pstate.Valid)
    {
      if (!(pstate.Valid = Parent.Next(out var next)))
        return;
      pstate.Cur = next.Nav;
      pstate.Set = new();

      InitCtx(ref pstate);
    }
  }

  protected abstract void InitCtx(ref AxisState state);
  protected abstract void NextCtx(ref AxisState state);
}

public class ExecPathOpAxisAncestor(
  ExecPathOp Parent, AxisType Axis = AxisType.Ancestor) : ExecPathOpAxis(Parent, Axis)
{
  protected override void InitCtx(ref AxisState state) => NextCtx(ref state);
  protected override void NextCtx(ref AxisState state) =>
    state.Valid = state.Cur.Parent(out state.Cur);
}
public class ExecPathOpAxisAncestorOrSelf(ExecPathOp Parent)
  : ExecPathOpAxisAncestor(Parent, AxisType.AncestorOrSelf)
{
  protected override void InitCtx(ref AxisState state) => state.Valid = true;
}
public class ExecPathOpAxisAttribute(ExecPathOp Parent)
  : ExecPathOpAxis(Parent, AxisType.Attribute)
{
  protected override void InitCtx(ref AxisState state) =>
    state.Valid = state.Cur.FirstAttribute(out state.Cur);
  protected override void NextCtx(ref AxisState state) =>
    state.Valid = state.Cur.NextAttribute(out state.Cur);
}
public class ExecPathOpAxisChild(ExecPathOp Parent) : ExecPathOpAxis(Parent, AxisType.Child)
{
  protected override void InitCtx(ref AxisState state) =>
    state.Valid = state.Cur.FirstChild(out state.Cur);
  protected override void NextCtx(ref AxisState state) =>
    state.Valid = state.Cur.NextSibling(out state.Cur);
}
public class ExecPathOpAxisDescendant(
  ExecPathOp Parent, AxisType Axis = AxisType.Descendant) : ExecPathOpAxis(Parent, Axis)
{
  protected override void InitCtx(ref AxisState state)
  {
    state.Base = state.Cur;
    NextCtx(ref state);
  }
  protected override void NextCtx(ref AxisState state)
  {
    ref var cur = ref state.Cur;
    ref var valid = ref state.Valid;
    if (valid = cur.FirstChild(out var next))
    {
      cur = next;
      return;
    }
    while (!cur.SameAs(state.Base))
    {
      if (valid = cur.NextSibling(out next))
      {
        cur = next;
        return;
      }
      if (!(valid = cur.Parent(out cur)))
        throw new InvalidOperationException();
    }
    state.Valid = false;
  }
}
public class ExecPathOpAxisDescendantOrSelf(ExecPathOp Parent)
  : ExecPathOpAxisDescendant(Parent, AxisType.DescendantOrSelf)
{
  protected override void InitCtx(ref AxisState state)
  {
    state.Base = state.Cur;
    state.Valid = true;
  }
}
public class ExecPathOpAxisFollowing(ExecPathOp Parent)
  : ExecPathOpAxis(Parent, AxisType.Following)
{
  protected override void InitCtx(ref AxisState state)
  {
    ref var cur = ref state.Cur;
    if (cur.Node.Type.IsAttribute)
    {
      if (!(state.Valid = cur.Parent(out cur)))
        return;
    }
    NextNoChild(ref state);
  }

  protected override void NextCtx(ref AxisState state)
  {
    if (state.Valid = state.Cur.FirstChild(out var next))
    {
      state.Cur = next;
      return;
    }
    NextNoChild(ref state);
  }

  private void NextNoChild(ref AxisState state)
  {
    ref var valid = ref state.Valid;
    ref var cur = ref state.Cur;
    while (true)
    {
      if (valid = cur.NextSibling(out var next))
      {
        cur = next;
        return;
      }
      if (!(valid = cur.Parent(out cur)))
        return;
    }
  }
}
public class ExecPathOpAxisFollowingSibling(ExecPathOp Parent)
  : ExecPathOpAxis(Parent, AxisType.FollowingSibling)
{
  protected override void InitCtx(ref AxisState state) => NextCtx(ref state);
  protected override void NextCtx(ref AxisState state) =>
    state.Valid = state.Cur.NextSibling(out state.Cur);
}
public class ExecPathOpAxisNamespace(ExecPathOp Parent)
  : ExecPathOpAxis(Parent, AxisType.Namespace)
{
  protected override void InitCtx(ref AxisState state) =>
    state.Valid = state.Cur.FirstNamespace(out state.Cur);
  protected override void NextCtx(ref AxisState state) =>
    state.Valid = state.Cur.NextNamespace(out state.Cur);
}
public class ExecPathOpAxisParent(ExecPathOp Parent)
  : ExecPathOpAxis(Parent, AxisType.Parent)
{
  protected override void InitCtx(ref AxisState state) =>
    state.Valid = state.Cur.Parent(out state.Cur);
  protected override void NextCtx(ref AxisState state) => state.Valid = false;
}
public class ExecPathOpAxisPreceding(ExecPathOp Parent)
  : ExecPathOpAxis(Parent, AxisType.Preceding)
{
  protected override void InitCtx(ref AxisState state)
  {
    state.Base = state.Cur;
    state.Depth = 0;
    state.PState = AxPreState.PrevSib;
    NextCtx(ref state);
  }

  protected override void NextCtx(ref AxisState state)
  {
    ref var cur = ref state.Cur;
    ref var valid = ref state.Valid;
    while (true)
    {
      switch (state.PState)
      {
        case AxPreState.PrevSib:
          {
            if (cur.PreviousSibling(out var next))
            {
              // if we have a previous sibling, move to it and try its children first
              cur = next;
              state.PState = AxPreState.LastChild;
              continue;
            }
            // otherwise go to parent and try again
            state.PState = AxPreState.Parent;
            continue;
          }
        case AxPreState.LastChild:
          {
            if (cur.LastChild(out var next))
            {
              // if we have a child, move to last and try its children
              cur = next;
              state.Depth++;
              continue;
            }
            // otherwise return this node, and move to previous sibling next
            state.PState = AxPreState.PrevSib;
            state.Valid = true;
            return;
          }
        case AxPreState.Parent:
          {
            // if we are at the root, we are done
            if (!(valid = cur.Parent(out cur)))
              return;
            // otherwise its prev sibling will be next after possibly returning this
            state.PState = AxPreState.PrevSib;
            if (state.Depth == 0)
            {
              // if we are moving further upwards, move the base up
              if (!state.Base.Parent(out state.Base))
                throw new InvalidOperationException();
              // if base matches, we are at an ancestor, so don't return it
              if (cur.SameAs(state.Base))
                continue;
            }
            else
              state.Depth--;
            return;
          }
        default:
          throw new InvalidOperationException($"{state.PState}");
      }
    }
  }
}
public class ExecPathOpAxisPrecedingSibling(ExecPathOp Parent) : ExecPathOpAxis(Parent, AxisType.PrecedingSibling)
{
  protected override void InitCtx(ref AxisState state) => NextCtx(ref state);
  protected override void NextCtx(ref AxisState state) =>
    state.Valid = state.Cur.PreviousSibling(out state.Cur);
}
public class ExecPathOpAxisSelf(ExecPathOp Parent) : ExecPathOpAxis(Parent, AxisType.Self)
{
  protected override void InitCtx(ref AxisState state) => state.Valid = true;
  protected override void NextCtx(ref AxisState state) => state.Valid = false;
}
