
using System;
using System.Collections.Generic;
using XPP.Doc;
using XPP.Path;

namespace XPP.Patch;

public enum ActionType
{
  Invalid,
  Root,

  // high-level patch ops specified in xml
  // target node is patch element
  OpPatch,
  OpCopy,
  OpMerge,
  OpDelete,
  OpIf,
  OpIfAny,
  OpIfNone,
  OpWith,
  WithCtx, // run as patch with new context node

  // low-level patch actions that alter the document
  // target node is element to modify
  Insert, // insert a new xml node
  InsertText, // inserts a text node
  Set, // set the value of a text/attribute/etc node
  Remove, // remove an existing xml node
  Merge, // merge an existing node into another
}

public enum PatchPosition
{
  Replace,
  Append,
  Prepend,
  Before,
  After
}

public struct PatchAction
{
  public ActionType Type;
  public int InVersion;
  public int OutVersion;

  public XPNodeRef Context;
  public XPNodeRef Target;
  public XPNodeRef Source;
  public PatchPosition Position;

  // xpath query strings and ranges in log values list
  public string TargetPath;
  public Range TargetResult;
  public string SourcePath;
  public Range SourceResult;
}

public static class PatchActions
{
  public delegate void ActionDelegate(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action);

  public static readonly Dictionary<ActionType, ActionDelegate> Delegates = new() {
    { ActionType.Root, Root },
    { ActionType.OpPatch, OpPatch },
    { ActionType.OpCopy, OpCopy },
    { ActionType.OpMerge, OpMerge },
    { ActionType.OpDelete, OpDelete },
    { ActionType.OpIf, OpIf },
    { ActionType.OpIfAny, OpIfAny },
    { ActionType.OpIfNone, OpIfNone },
    { ActionType.OpWith, OpWith },
    { ActionType.WithCtx, WithCtx },
    { ActionType.Insert, Insert },
    { ActionType.InsertText, InsertText },
    { ActionType.Set, Set },
    { ActionType.Remove, Remove },
    { ActionType.Merge, Merge },
  };

  public static void Root(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  { }

  public static void OpPatch(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var child = action.Target.FirstContent;
    while (child.Valid)
    {
      var op = deserializer.Deserialize(child);
      op?.Build(action, child);
      child = child.NextSibling;
    }
  }

  public static void OpCopy(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    foreach (ref var tgtVal in action.TargetResult)
    {
      if (tgtVal.Type is not XPValueType.NodeSet)
        throw new InvalidOperationException(
          $"Path must return NodeSet, not {tgtVal.Type}");
      var target = tgtVal.Node;
      if (!target.Valid)
        continue;
      var tgtType = target.Type;
      if (tgtType is XPType.Element)
        OpCopyElement(target, action);
      else if (tgtType.HasValue)
        OpCopyText(target, action);
      else
        throw new InvalidOperationException($"Invalid copy target type {tgtType}");
    }
  }

  private static void OpCopyElement(XPNodeRef target, PatchLog.ActionRef action)
  {
    var (pos, copyTgt) = action.Action.Position switch
    {
      PatchPosition.Replace => (PatchPosition.Before, target),
      PatchPosition.Append => (PatchPosition.Append, target),
      PatchPosition.Prepend =>
        target.FirstContent.Valid
        ? (PatchPosition.Before, target.FirstContent)
        : (PatchPosition.Append, target),
      PatchPosition.Before => (PatchPosition.Before, target),
      PatchPosition.After =>
        target.NextSibling.Valid
        ? (PatchPosition.Before, target.NextSibling)
        : (PatchPosition.Append, target.Parent),
      _ => throw new InvalidOperationException($"{action.Action.Position}"),
    };
    foreach (ref var src in action.SourceResult)
    {
      if (src.Type is XPValueType.NodeSet)
        action.AddChild(
          ActionType.Insert, target: copyTgt, source: src.Node, pos: pos);
      else
      {
        var insert = action.AddChild(
          ActionType.InsertText, target: copyTgt, pos: pos);
        insert.SetSourceResult(new ExecValue(src.StringValue));
      }
    }
  }

  private static void OpCopyText(XPNodeRef target, PatchLog.ActionRef action)
  {
    var sources = action.SourceResult;
    var strVal = "";
    switch (action.Action.Position)
    {
      case PatchPosition.Replace:
        if (sources.Length > 0)
          strVal = sources[^1].StringValue;
        break;
      case PatchPosition.After or PatchPosition.Append:
        if (sources.Length == 0)
          return;
        foreach (ref var src in sources)
          strVal += src.StringValue;
        strVal = target.Value + strVal;
        break;
      case PatchPosition.Before or PatchPosition.Prepend:
        if (sources.Length == 0)
          return;
        foreach (ref var src in sources)
          strVal += src.StringValue;
        strVal += target.Value;
        break;
      default:
        throw new InvalidOperationException($"{action.Action.Position}");
    }
    var copy = action.AddChild(ActionType.Set, target: target);
    copy.SetSourceResult(new ExecValue(strVal));
  }

  public static void OpMerge(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    foreach (var tgtVal in action.TargetResult)
    {
      if (tgtVal.Type != XPValueType.NodeSet)
        throw new InvalidOperationException(
          $"Path must return NodeSet, not {tgtVal.Type}");
      var target = tgtVal.Node;
      if (!target.Valid)
        continue;
      if (target.Type is not XPType.Element)
        throw new InvalidOperationException(
          $"Path must select Elements, not {target.Type}");
      foreach (var srcVal in action.SourceResult)
      {
        if (srcVal.Type != XPValueType.NodeSet)
          throw new InvalidOperationException(
            $"Path must return NodeSet, not {srcVal.Type}");
        var source = srcVal.Node;
        if (!source.Valid)
          continue;
        if (source.Type is not XPType.Element)
          throw new InvalidOperationException(
            $"Path must select Elements, not {source.Type}");
        action.AddChild(ActionType.Merge, target: target, source: source);
      }
    }
  }

  public static void OpDelete(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    foreach (ref var val in action.TargetResult)
    {
      if (val.Type != XPValueType.NodeSet)
        throw new InvalidOperationException(
          $"Path must return NodeSet, not {val.Type}");
      var node = val.Node.LatestVersion;
      if (node.Valid)
        action.AddChild(ActionType.Remove, target: node);
    }
  }

  public static void OpIf(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    // will be handled by IfAny/IfNone child ops
  }

  public static void OpIfAny(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var targets = action.Action.TargetPath != null
      ? action.TargetResult
      : action.Parent.TargetResult;

    var any = false;
    foreach (ref var val in targets)
    {
      any = val.BoolValue;
      break;
    }

    if (any)
      OpPatch(deserializer, action);
  }

  public static void OpIfNone(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var targets = action.Action.TargetPath != null
      ? action.TargetResult
      : action.Parent.TargetResult;

    var any = false;
    foreach (ref var val in targets)
    {
      any = val.BoolValue;
      break;
    }

    if (!any)
      OpPatch(deserializer, action);
  }

  public static void OpWith(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    foreach (ref var val in action.TargetResult)
    {
      if (val.Type is not XPValueType.NodeSet)
        throw new InvalidOperationException(
          $"Path must return NodeSet, not {val.Type}");
      action.AddChild(
        ActionType.WithCtx,
        context: val.Node
      );
    }
  }

  public static void WithCtx(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action
  ) => OpPatch(deserializer, action);

  public static void Insert(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var source = action.Source; // keep source version
    var target = action.Target.LatestVersion;
    if (!source.Valid || !target.Valid)
      return;
    action.Doc.NewVersion();
    target = target.LatestVersion;

    var inserted = action.Action.Position switch
    {
      PatchPosition.Append =>
        target.Import(source),
      PatchPosition.Before =>
       target.Parent.Import(source, before: target),
      // position should be reduced to Append or Before by this point
      _ => throw new InvalidOperationException($"{action.Action.Position}"),
    };

    if (inserted.Type is XPType.Element && action.Parent.Action.Type is ActionType.Merge)
      CleanMergeAttrs(inserted);
  }

  public static void InsertText(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var target = action.Target.LatestVersion;
    if (!target.Valid)
      return;
    action.Doc.NewVersion();
    target = target.LatestVersion;
    var val = action.SourceResult[0].StringValue;
    switch (action.Action.Position)
    {
      case PatchPosition.Append:
        target.AddChild(XPType.Text, "", value: val);
        break;
      case PatchPosition.Before:
        target.Parent.AddChild(XPType.Text, "", value: val, before: target);
        break;
      default:
        // position should be reduced to Append or Before by this point
        throw new InvalidOperationException($"{action.Action.Position}");
    }
  }

  public static void Set(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var node = action.Target.LatestVersion;
    if (!node.Valid)
      return;
    action.Doc.NewVersion();
    node.LatestVersion.SetValue(action.SourceResult[0].String);
  }

  public static void Remove(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var node = action.Target.LatestVersion;
    if (!node.Valid)
      return;
    action.Doc.NewVersion();
    node.LatestVersion.Remove();
  }

  public const string MergeIdAttr = "_MergeId";
  public const string DefaultMergeId = "Id";
  public const string AnyMergeId = "*";
  public const string NoneMergeId = "-";
  public const string MergePosAttr = "_MergePos";
  private static readonly XPName MergeIdAttrName = new("", "", MergeIdAttr);
  private static readonly XPName MergePosAttrName = new("", "", MergePosAttr);

  public static void Merge(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var source = action.Source;
    var target = action.Target.LatestVersion;
    if (!source.Valid || !target.Valid)
      return;

    var mergePos = PatchPosition.Append;
    if (source.Attribute(MergePosAttrName) is XPNodeRef { Valid: true } posAttr)
    {
      Enum.TryParse(posAttr.Value, out mergePos);
      if (mergePos is not (PatchPosition.Append or PatchPosition.Prepend))
        throw new InvalidOperationException(
          $"Invalid {MergePosAttr} '{posAttr.Value}'. must be Append or Prepend");
    }

    var srcAttr = source.FirstAttr;
    while (srcAttr.Valid)
    {
      if (srcAttr.Name == MergeIdAttrName || srcAttr.Name == MergePosAttrName)
      {
        srcAttr = srcAttr.NextSibling;
        continue;
      }
      var tgtAttr = target.Attribute(srcAttr.Name);
      if (tgtAttr.Valid)
        action.AddChild(ActionType.Set, target: tgtAttr, source: srcAttr);
      else if (mergePos is PatchPosition.Prepend && target.FirstAttr.Valid)
        action.AddChild(ActionType.Insert,
          target: target.FirstAttr, source: srcAttr, pos: PatchPosition.Before);
      else
        action.AddChild(ActionType.Insert,
          target: target, source: srcAttr, pos: PatchPosition.Append);

      srcAttr = srcAttr.NextSibling;
    }

    var srcChild = source.FirstContent;
    while (srcChild.Valid)
    {
      var mergeTgt = FindMergeTarget(srcChild, target);
      if (mergeTgt.Valid)
        action.AddChild(ActionType.Merge, target: mergeTgt, source: srcChild);
      else if (mergePos is PatchPosition.Prepend && target.FirstContent.Valid)
        action.AddChild(ActionType.Insert,
          target: target.FirstContent, source: srcChild, pos: PatchPosition.Before);
      else
        action.AddChild(ActionType.Insert,
          target: target, source: srcChild, pos: PatchPosition.Append);
      srcChild = srcChild.NextSibling;
    }
  }

  private static XPNodeRef FindMergeTarget(XPNodeRef source, XPNodeRef tgtParent)
  {
    if (source.Type is not XPType.Element)
      return XPNodeRef.Invalid;

    var mergeId = source.Attribute(DefaultMergeId).Valid ? DefaultMergeId : AnyMergeId;
    if (source.Attribute(MergeIdAttrName) is XPNodeRef { Valid: true } mergeIdAttr)
      mergeId = mergeIdAttr.Value;

    if (mergeId is NoneMergeId)
      return XPNodeRef.Invalid;

    if (mergeId is AnyMergeId)
    {
      var target = tgtParent.FirstContent;
      while (target.Valid)
      {
        if (target.Type is XPType.Element && target.Name == source.Name)
          return target;
        target = target.NextSibling;
      }
      return XPNodeRef.Invalid;
    }
    else
    {
      if (source.Attribute(mergeId) is not XPNodeRef { Valid: true } srcIdAttr)
        return XPNodeRef.Invalid;
      var srcId = srcIdAttr.Value;
      var target = tgtParent.FirstContent;
      while (target.Valid)
      {
        if (target.Type is XPType.Element
            && target.Attribute(mergeId) is XPNodeRef { Valid: true } tgtIdAttr
            && tgtIdAttr.Value == srcId)
          return target;
        target = target.NextSibling;
      }
      return XPNodeRef.Invalid;
    }
  }

  private static void CleanMergeAttrs(XPNodeRef node)
  {
    if (!node.Valid)
      return;
    if (node.Type is XPType.Attribute)
    {
      if (node.Name == MergeIdAttrName || node.Name == MergePosAttrName)
        node.Remove();
    }
    else if (node.Type is XPType.Element)
    {
      var attr = node.FirstAttr;
      while (attr.Valid)
      {
        var next = attr.NextSibling;
        CleanMergeAttrs(attr);
        attr = next;
      }
      var child = node.FirstContent;
      while (child.Valid)
      {
        CleanMergeAttrs(child);
        child = child.NextSibling;
      }
    }
  }
}