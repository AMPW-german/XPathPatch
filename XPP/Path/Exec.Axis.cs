
using System;
using XPP.Doc;

namespace XPP.Path;

public ref partial struct Exec
{
  private bool NextAxis(int idx, out XPNavigator nav)
  {
    ref var state = ref states[idx];
    var axis = path.Paths[idx].Axis;
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
        _ => throw new InvalidOperationException($"{path.Paths[idx].Axis}"),
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
    return NextNode(idx + 1, out state.XPNavigator);
  }

  private bool NextAxisAncestor(ref PathState state, out XPNavigator nav)
  {
    var res = state.XPNavigator.Parent(out nav);
    state.XPNavigator = nav;
    return res;
  }

  private bool NextAxisAncestorOrSelf(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
    {
      nav = state.XPNavigator;
      return true;
    }
    var res = state.XPNavigator.Parent(out nav);
    state.XPNavigator = nav;
    return res;
  }

  private bool NextAxisAttribute(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
    {
      var res = state.XPNavigator.FirstAttribute(out nav);
      state.XPNavigator = nav;
      return res;
    }
    else
    {
      var res = state.XPNavigator.NextAttribute(out nav);
      state.XPNavigator = nav;
      return res;
    }
  }

  private bool NextAxisChild(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
    {
      var res = state.XPNavigator.FirstChild(out nav);
      state.XPNavigator = nav;
      return res;
    }
    else
    {
      var res = state.XPNavigator.NextSibling(out nav);
      state.XPNavigator = nav;
      return res;
    }
  }

  private bool NextAxisDescendant(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
      state.Base = state.XPNavigator;
    if (state.XPNavigator.FirstChild(out nav))
    {
      state.XPNavigator = nav;
      return true;
    }
    while (nav.CompareTo(state.Base) != 0)
    {
      if (state.XPNavigator.NextSibling(out nav))
      {
        state.XPNavigator = nav;
        return true;
      }
      if (!state.XPNavigator.Parent(out nav))
        throw new InvalidOperationException();
      state.XPNavigator = nav;
    }
    return false;
  }

  private bool NextAxisDescendantOrSelf(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
    {
      state.Base = state.XPNavigator;
      nav = state.XPNavigator;
      return true;
    }
    return NextAxisDescendant(ref state, out nav);
  }

  private bool NextAxisFollowing(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1 && (state.XPNavigator.IsAttribute() || state.XPNavigator.IsNs()))
    {
      if (!state.XPNavigator.Parent(out nav))
        throw new InvalidOperationException();
      state.XPNavigator = nav;
    }
    if (state.XPNavigator.FirstChild(out nav))
    {
      state.XPNavigator = nav;
      return true;
    }
    while (true)
    {
      if (state.XPNavigator.NextSibling(out nav))
      {
        state.XPNavigator = nav;
        return true;
      }
      if (!state.XPNavigator.Parent(out nav))
        return false;
      state.XPNavigator = nav;
    }
  }

  private bool NextAxisFollowingSibling(ref PathState state, out XPNavigator nav)
  {
    var res = state.XPNavigator.NextSibling(out nav);
    state.XPNavigator = nav;
    return res;
  }

  private bool NextAxisNamespace(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
    {
      var res = state.XPNavigator.FirstNamespace(out nav);
      state.XPNavigator = nav;
      return res;
    }
    else
    {
      var res = state.XPNavigator.NextNamespace(out nav);
      state.XPNavigator = nav;
      return res;
    }
  }

  private bool NextAxisParent(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
      return state.XPNavigator.Parent(out nav);
    nav = default;
    return false;
  }

  private bool NextAxisPreceding(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
    {
      state.Base = state.XPNavigator.Clone();
      state.Up = true;
      state.Depth = 0;
    }
    if (!state.Up && state.XPNavigator.LastChild(out nav))
    {
      state.XPNavigator = nav;
      state.Depth++;
      return true;
    }
    state.Up = false;
    while (true)
    {
      if (state.XPNavigator.PreviousSibling(out nav))
      {
        state.XPNavigator = nav;
        return true;
      }
      if (!state.XPNavigator.Parent(out nav))
        return false;
      state.XPNavigator = nav;
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

  private bool NextAxisPrecedingSibling(ref PathState state, out XPNavigator nav)
  {
    var res = state.XPNavigator.PreviousSibling(out nav);
    state.XPNavigator = nav;
    return res;
  }

  private bool NextAxisSelf(ref PathState state, out XPNavigator nav)
  {
    if (state.Index == -1)
    {
      nav = state.XPNavigator.Clone();
      return true;
    }
    nav = default;
    return false;
  }
}