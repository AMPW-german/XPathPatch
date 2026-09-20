
using System;
using System.Collections.Generic;

namespace XPP.Patch;

public class PatchExecutor
{
  public readonly PatchDomain Domain;
  public readonly PatchAction Root;
  public readonly List<PatchAction> ExecStack = [];

  public event Action<PatchAction> ActionStarted;
  public event Action<PatchAction> ActionCompleted;
  public event Action<PatchAction> ActionFailed;

  public PatchExecutor(PatchDomain Domain)
  {
    this.Domain = Domain;
    Root = PatchAction.NewRoot(Domain);

    foreach (var patch in Domain.Patches)
      Root.AddChild(ActionType.OpPatch, target: patch);

    ExecStack.Add(Root);
  }

  public bool Done => Root.Finished;
  public PatchAction Next => ExecStack.Count > 0 ? ExecStack[^1] : null;
  public PatchAction Last { get; private set; }
  public bool HasError => Next?.Error != null;

  public bool Step()
  {
    var action = Next;
    if (action == null)
      return false;
    if (action.Error != null)
      return false;
    try
    {
      if (!PatchActions.Delegates.TryGetValue(action.Type, out var actionDelegate))
      {
        throw new InvalidOperationException($"Invalid patch action {action.Type}");
      }
      Last = action;
      action.Start();
      Notify(ActionStarted, action);
      actionDelegate(Domain.OpDeserializer, action);
      // if we have child ops, don't finish this action until they are executed
      if (action.Children.Count > 0)
      {
        ExecStack.Add(action.Children[0]);
        return true;
      }

      // if we are the last child, finish parent and continue, otherwise move to next sibling
      while (action != null)
      {
        action.Finish();
        Notify(ActionCompleted, action);
        var parent = action.Parent;
        if (parent != null && parent.Children.Count > action.ChildIndex + 1)
        {
          ExecStack[^1] = parent.Children[action.ChildIndex + 1];
          return true;
        }

        ExecStack.RemoveAt(ExecStack.Count - 1);
        action = action.Parent;
      }

      // if we finished the root, we are done
      return false;
    }
    catch (Exception ex)
    {
      action.Error = ex;
      Notify(ActionFailed, action);
      if (Next != action)
        ExecStack.Add(action);
      return false;
    }
  }

  private static void Notify(Action<PatchAction> handlers, PatchAction action)
  {
    if (handlers == null)
      return;

    foreach (Action<PatchAction> handler in handlers.GetInvocationList())
    {
      try
      {
        handler(action);
      }
      catch
      {
      }
    }
  }

  public bool UntilFinished(PatchAction action)
  {
    if (action == null)
      return !Done;
    do
    {
      if (action.Finished)
        return true;
    } while (Step());
    return false;
  }

  public bool UntilStarted(PatchAction action)
  {
    if (action == null)
      return !Done;
    do
    {
      if (action.Started)
        return true;
    } while (Step());
    return false;
  }

  public bool StepOut() => UntilFinished(Next?.Parent);
  public bool StepOver() => UntilFinished(Next);
  public void StepToEnd() => UntilFinished(Root);
}