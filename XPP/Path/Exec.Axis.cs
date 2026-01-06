
using System;

namespace XPP.Path;

public ref partial struct Exec<Nav>
{
  private bool NextAxis(int idx, out Nav nav)
  {
    ref var state = ref states[idx];
    var axis = Path.Paths[idx].Axis;
    while (true)
    {
      if (!NextInit(idx, ref state))
      {
        nav = default;
        return false;
      }

      if (axis switch
      {
        AxisType.Ancestor => NextAxisAncestor(ref state, out nav),
        AxisType.AncestorOrSelf => NextAxisAncestorOrSelf(ref state, out nav),
        AxisType.Attribute => NextAxisAttribute(ref state, out nav),
        AxisType.Child => NextAxisChild(ref state, out nav),
        AxisType.Descendant => NextAxisDescendant(ref state, out nav),
        AxisType.DescendantOrSelf => NextAxisDescendantOrSelf(ref state, out nav),
        AxisType.Following => NextAxisFollowing(ref state, out nav),
        AxisType.FollowingSibling => NextAxisFollowingSibling(ref state, out nav),
        AxisType.Namespace => NextAxisNamespace(ref state, out nav),
        AxisType.Parent => NextAxisParent(ref state, out nav),
        AxisType.Preceding => NextAxisPreceding(ref state, out nav),
        AxisType.PrecedingSibling => NextAxisPrecedingSibling(ref state, out nav),
        AxisType.Self => NextAxisSelf(ref state, out nav),
        _ => throw new InvalidOperationException($"{Path.Paths[idx].Axis}"),
      })
      {
        state.Index++;
        return true;
      }

      state.Index = -1;
    }
  }

  private bool NextInit(int idx, ref PathState state)
  {
    if (state.Index != -1)
      return true;
    return NextNode(idx + 1, out state.Nav);
  }

  private bool NextAxisAncestor(ref PathState state, out Nav nav)
  {
    var res = state.Nav.Parent(out nav);
    state.Nav = nav;
    return res;
  }

  private bool NextAxisAncestorOrSelf(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
    {
      nav = state.Nav;
      return true;
    }
    var res = state.Nav.Parent(out nav);
    state.Nav = nav;
    return res;
  }

  private bool NextAxisAttribute(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
    {
      var res = state.Nav.FirstAttribute(out nav);
      state.Nav = nav;
      return res;
    }
    else
    {
      var res = state.Nav.NextAttribute(out nav);
      state.Nav = nav;
      return res;
    }
  }

  private bool NextAxisChild(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
    {
      var res = state.Nav.FirstChild(out nav);
      state.Nav = nav;
      return res;
    }
    else
    {
      var res = state.Nav.NextSibling(out nav);
      state.Nav = nav;
      return res;
    }
  }

  private bool NextAxisDescendant(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
      state.Base = state.Nav;
    if (state.Nav.FirstChild(out nav))
    {
      state.Nav = nav;
      return true;
    }
    while (nav.CompareTo(state.Base) != 0)
    {
      if (state.Nav.NextSibling(out nav))
      {
        state.Nav = nav;
        return true;
      }
      if (!state.Nav.Parent(out nav))
        throw new InvalidOperationException();
      state.Nav = nav;
    }
    return false;
  }

  private bool NextAxisDescendantOrSelf(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
    {
      state.Base = state.Nav;
      nav = state.Nav;
      return true;
    }
    return NextAxisDescendant(ref state, out nav);
  }

  private bool NextAxisFollowing(ref PathState state, out Nav nav)
  {
    if (state.Index == -1 && (state.Nav.IsAttribute() || state.Nav.IsNs()))
    {
      if (!state.Nav.Parent(out nav))
        throw new InvalidOperationException();
      state.Nav = nav;
    }
    if (state.Nav.FirstChild(out nav))
    {
      state.Nav = nav;
      return true;
    }
    while (true)
    {
      if (state.Nav.NextSibling(out nav))
      {
        state.Nav = nav;
        return true;
      }
      if (!state.Nav.Parent(out nav))
        return false;
      state.Nav = nav;
    }
  }

  private bool NextAxisFollowingSibling(ref PathState state, out Nav nav)
  {
    var res = state.Nav.NextSibling(out nav);
    state.Nav = nav;
    return res;
  }

  private bool NextAxisNamespace(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
    {
      var res = state.Nav.FirstNamespace(out nav);
      state.Nav = nav;
      return res;
    }
    else
    {
      var res = state.Nav.NextNamespace(out nav);
      state.Nav = nav;
      return res;
    }
  }

  private bool NextAxisParent(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
      return state.Nav.Parent(out nav);
    nav = default;
    return false;
  }

  private bool NextAxisPreceding(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
    {
      state.Base = state.Nav.Clone();
      state.Up = true;
      state.Depth = 0;
    }
    if (!state.Up && state.Nav.LastChild(out nav))
    {
      state.Nav = nav;
      state.Depth++;
      return true;
    }
    state.Up = false;
    while (true)
    {
      if (state.Nav.PreviousSibling(out nav))
      {
        state.Nav = nav;
        return true;
      }
      if (!state.Nav.Parent(out nav))
        return false;
      state.Nav = nav;
      state.Up = true;
      var skip = false;
      if (state.Depth == 0)
      {
        if (!state.Base.Parent(out var bparent))
          throw new InvalidOperationException();
        state.Base = bparent;
        skip = state.Base.CompareTo(nav) == 0;
      }
      else
        state.Depth--;
      if (!skip)
        return true;
    }
  }

  private bool NextAxisPrecedingSibling(ref PathState state, out Nav nav)
  {
    var res = state.Nav.PreviousSibling(out nav);
    state.Nav = nav;
    return res;
  }

  private bool NextAxisSelf(ref PathState state, out Nav nav)
  {
    if (state.Index == -1)
    {
      nav = state.Nav.Clone();
      return true;
    }
    nav = default;
    return false;
  }
}