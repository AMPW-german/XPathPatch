
using System;
using XPP.Doc;
using XPP.Path;

namespace XPP.Patch;

public enum ActionType
{
  Invalid,
  Root,
  Context,

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

  // low-level patch actions that alter the document
  // target node is element to modify
  Insert, // insert a new xml node
  Replace, // replace an existing xml node
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

  public XPNodeId Context;
  public XPNodeId Target;
  public XPNodeId Source;
  public PatchPosition Position;

  // xpath query strings and ranges in log values list
  public string TargetPath;
  public Range TargetResult;
  public string SourcePath;
  public Range SourceResult;
}

public static class PatchActions
{

  public static void Root(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  { }

  public static void Context(
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
    throw new NotImplementedException();
  }

  public static void OpMerge(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }

  public static void OpDelete(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    var val = XPath.Exec(action.Action.TargetPath, action.Context.Nav, out var exec);

    if (val.Type is not Path.ValueType.NodeSet)
      throw new InvalidOperationException(
        $"Path should return NodeSet, not {val.Type}");

    action.Log.Domain.Doc.NewVersion();

    while (exec.NextNode(val.Value.NodeSet, out var nav))
    {
      var node = nav.Node.LatestVersion;
      if (node.Valid)
        node.Remove();
    }
  }

  public static void OpIf(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }

  public static void OpIfAny(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }

  public static void OpIfNone(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }

  public static void OpWith(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }

  public static void Insert(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }

  public static void Replace(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }

  public static void Remove(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }

  public static void Merge(
    PatchOpDeserializer deserializer, PatchLog.ActionRef action)
  {
    throw new NotImplementedException();
  }
}