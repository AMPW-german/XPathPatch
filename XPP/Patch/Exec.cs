
using System;

namespace XPP.Patch;

public class PatchExecutor
{
  public readonly PatchDomain Domain;
  public readonly PatchLog Log;
  private PatchLog.ActionRef next;

  public PatchExecutor(PatchDomain Domain)
  {
    this.Domain = Domain;
    Log = new(Domain);
    next = Log.Root;

    foreach (var patch in Domain.Patches)
      next.AddChild(ActionType.OpPatch, target: patch);
  }

  public bool Done => Log.Root.Finished;
  public PatchLog.ActionRef Next => next;

  public bool Step()
  {
    if (!next.Valid)
      return false;
    var action = next;
    action.Start();
    if (!PatchActions.Delegates.TryGetValue(action.Action.Type, out var actionDelegate))
      throw new InvalidOperationException($"Invalid patch action {action.Action.Type}");

    actionDelegate(Domain.OpDeserializer, action);
    // if we have child ops, don't finish this action until they are executed
    if ((next = action.FirstChild).Valid)
      return true;

    // if we are the last child, finish parent and continue, otherwise move to next sibling
    while (action.Valid)
    {
      action.Finish();

      if ((next = action.NextSibling).Valid)
        return true;

      action = action.Parent;
    }

    // if we finished the root, we are done
    next = PatchLog.ActionRef.Invalid;
    return false;
  }

  public bool UntilFinished(PatchLog.ActionRef action)
  {
    if (!action.Valid)
      return !Done;
    do
    {
      if (action.Finished)
        return true;
    } while (Step());
    return false;
  }

  public bool UntilStarted(PatchLog.ActionRef action)
  {
    if (!action.Valid)
      return !Done;
    do
    {
      if (action.Started)
        return true;
    } while (Step());
    return false;
  }

  public bool StepOut() => UntilFinished(next.Parent);
  public bool StepOver() => UntilFinished(next);
  public void StepToEnd() => UntilFinished(Log.Root);
}