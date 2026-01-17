
using System;
using XPP.Doc;

namespace XPP.Path;

public ref partial struct Exec
{
  private bool AxisInit(AxisType axis, ref XPNavigator nav, out PathState state) => axis switch
  {
    AxisType.Ancestor => AxisAncestor.Init(ref nav, out state),
    AxisType.AncestorOrSelf => AxisAncestorOrSelf.Init(ref nav, out state),
    AxisType.Attribute => AxisAttribute.Init(ref nav, out state),
    AxisType.Child => AxisChild.Init(ref nav, out state),
    AxisType.Descendant => AxisDescendant.Init(ref nav, out state),
    AxisType.DescendantOrSelf => AxisDescendantOrSelf.Init(ref nav, out state),
    AxisType.Following => AxisFollowing.Init(ref nav, out state),
    AxisType.FollowingSibling => AxisFollowingSibling.Init(ref nav, out state),
    AxisType.Namespace => AxisNamespace.Init(ref nav, out state),
    AxisType.Parent => AxisParent.Init(ref nav, out state),
    AxisType.Preceding => AxisPreceding.Init(ref nav, out state),
    AxisType.PrecedingSibling => AxisPrecedingSibling.Init(ref nav, out state),
    AxisType.Self => AxisSelf.Init(ref nav, out state),
    _ => throw new InvalidOperationException($"{axis}"),
  };

  private bool AxisNext(AxisType axis, ref XPNavigator nav, ref PathState state) => axis switch
  {
    AxisType.Ancestor => AxisAncestor.Next(ref nav, ref state),
    AxisType.AncestorOrSelf => AxisAncestorOrSelf.Next(ref nav, ref state),
    AxisType.Attribute => AxisAttribute.Next(ref nav, ref state),
    AxisType.Child => AxisChild.Next(ref nav, ref state),
    AxisType.Descendant => AxisDescendant.Next(ref nav, ref state),
    AxisType.DescendantOrSelf => AxisDescendantOrSelf.Next(ref nav, ref state),
    AxisType.Following => AxisFollowing.Next(ref nav, ref state),
    AxisType.FollowingSibling => AxisFollowingSibling.Next(ref nav, ref state),
    AxisType.Namespace => AxisNamespace.Next(ref nav, ref state),
    AxisType.Parent => AxisParent.Next(ref nav, ref state),
    AxisType.Preceding => AxisPreceding.Next(ref nav, ref state),
    AxisType.PrecedingSibling => AxisPrecedingSibling.Next(ref nav, ref state),
    AxisType.Self => AxisSelf.Next(ref nav, ref state),
    _ => throw new InvalidOperationException($"{axis}"),
  };

  private interface IAxis
  {
    public abstract static bool Init(ref XPNavigator nav, out PathState state);
    public abstract static bool Next(ref XPNavigator nav, ref PathState state);
  }

  private class AxisAncestor : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return Next(ref nav, ref state);
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) =>
      nav.Parent(out var parent) && (nav = parent).Node.Valid;
  }
  private class AxisAncestorOrSelf : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return true;
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) =>
      AxisAncestor.Next(ref nav, ref state);
  }
  private class AxisAttribute : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return nav.FirstAttribute(out var attr) && (nav = attr).Node.Valid;
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) =>
      nav.NextAttribute(out var attr) && (nav = attr).Node.Valid;
  }
  private class AxisChild : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return nav.FirstChild(out var child) && (nav = child).Node.Valid;
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) =>
      nav.NextSibling(out var next) && (nav = next).Node.Valid;
  }
  private class AxisDescendant : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = new() { Base = nav };
      return Next(ref nav, ref state);
    }
    public static bool Next(ref XPNavigator nav, ref PathState state)
    {
      if (nav.FirstChild(out var next))
        return (nav = next).Node.Valid;
      while (nav.CompareTo(state.Base) != 0)
      {
        if (nav.NextSibling(out next))
          return (nav = next).Node.Valid;
        if (!nav.Parent(out next))
          throw new InvalidOperationException();
        nav = next;
      }
      return false;
    }
  }
  private class AxisDescendantOrSelf : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = new() { Base = nav };
      return true;
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) =>
      AxisDescendant.Next(ref nav, ref state);
  }
  private class AxisFollowing : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      if (nav.IsAttribute() || nav.IsNs())
      {
        if (!nav.Parent(out var next))
          throw new InvalidOperationException();
        nav = next;
      }
      state = default;
      while (true)
      {
        if (nav.NextSibling(out var next))
          return (nav = next).Node.Valid;
        if (!nav.Parent(out next))
          return false;
        nav = next;
      }
    }
    public static bool Next(ref XPNavigator nav, ref PathState state)
    {
      if (nav.FirstChild(out var next))
        return (nav = next).Node.Valid;
      while (true)
      {
        if (nav.NextSibling(out next))
          return (nav = next).Node.Valid;
        if (!nav.Parent(out next))
          return false;
        nav = next;
      }
    }
  }
  private class AxisFollowingSibling : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return Next(ref nav, ref state);
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) =>
      nav.NextSibling(out var next) && (nav = next).Node.Valid;
  }
  private class AxisNamespace : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return nav.FirstNamespace(out var next) && (nav = next).Node.Valid;
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) =>
      nav.NextNamespace(out var next) && (nav = next).Node.Valid;
  }
  private class AxisParent : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return nav.Parent(out var next) && (nav = next).Node.Valid;
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) => false;
  }
  private class AxisPreceding : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = new() { Base = nav, Up = true, Depth = 0 };
      return Next(ref nav, ref state);
    }
    public static bool Next(ref XPNavigator nav, ref PathState state)
    {
      if (!state.Up && nav.LastChild(out var next))
      {
        nav = next;
        state.Depth++;
        return true;
      }
      state.Up = false;
      while (true)
      {
        if (nav.PreviousSibling(out next))
          return (nav = next).Node.Valid;
        if (!nav.Parent(out next))
          return false;
        nav = next;
        state.Up = true;
        var skip = false;
        if (state.Depth == 0)
        {
          if (!state.Base.Parent(out next))
            throw  new InvalidOperationException();
          state.Base = next;
          skip = state.Base.CompareTo(nav) == 0;
        }
        else
          state.Depth--;
        if (!skip)
          return true;
      }
    }
  }
  private class AxisPrecedingSibling : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return Next(ref nav, ref state);
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) =>
      nav.PreviousSibling(out var next) && (nav = next).Node.Valid;
  }
  private class AxisSelf : IAxis
  {
    public static bool Init(ref XPNavigator nav, out PathState state)
    {
      state = default;
      return true;
    }
    public static bool Next(ref XPNavigator nav, ref PathState state) => false;
  }
}