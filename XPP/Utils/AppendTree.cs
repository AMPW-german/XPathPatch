
using System;

namespace XPP.Utils;

public class AppendTree<T> where T : struct
{
  private readonly AppendList<Node> nodes = [];

  public NodeRef Root => new(this, 0);

  public AppendTree(T rootValue)
  {
    Add(-1, rootValue);
  }

  private NodeRef Add(int parent, T value)
  {
    var index = nodes.Add(new()
    {
      Parent = parent,
      NextSibling = -1,
      FirstChild = -1,
      LastChild = -1,
      Value = value,
    });
    if (parent != -1)
    {
      ref var pnode = ref nodes[parent];
      if (pnode.LastChild != -1)
        nodes[pnode.LastChild].NextSibling = index;
      pnode.LastChild = index;
      if (pnode.FirstChild == -1)
        pnode.FirstChild = index;
    }
    return new(this, index);
  }

  public struct Node
  {
    public static readonly Node Invalid = new()
    {
      Parent = -1,
      NextSibling = -1,
      FirstChild = -1,
      LastChild = -1,
      Value = default,
    };

    public int Parent;
    public int NextSibling;
    public int FirstChild;
    public int LastChild;
    public T Value;
  }

  public readonly struct NodeRef(AppendTree<T> Tree, int Index)
  {
    public static readonly NodeRef Invalid = new(null, -1);

    public readonly AppendTree<T> Tree = Tree;
    public readonly int Index = Index;

    public bool Valid => Tree != null && Index >= 0;

    public NodeRef Parent => new(Tree, Node.Parent);
    public NodeRef NextSibling => new(Tree, Node.NextSibling);
    public NodeRef FirstChild => new(Tree, Node.FirstChild);

    public NodeRef AddChild(T value)
    {
      if (!Valid) throw new InvalidOperationException();
      return Tree.Add(Index, value);
    }

    public ref readonly Node Node
    {
      get
      {
        if (!Valid)
          return ref Node.Invalid;
        return ref Tree.nodes[Index];
      }
    }

    public ref T Value
    {
      get
      {
        if (!Valid) throw new InvalidOperationException();
        return ref Tree.nodes[Index].Value;
      }
    }

    public ChildEnumerator GetEnumerator() => new(this);
  }

  public struct ChildEnumerator(NodeRef parent)
  {
    private readonly NodeRef parent = parent;
    private bool init = false;
    private NodeRef current;

    public bool MoveNext()
    {
      if (init)
        current = current.NextSibling;
      else
      {
        current = parent.FirstChild;
        init = true;
      }
      return current.Valid;
    }

    public NodeRef Current => current;
  }
}