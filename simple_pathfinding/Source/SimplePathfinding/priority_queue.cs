#if true
//#define DEBUG_LOG
using System;
using System.Collections.Generic;
#if DEBUG_LOG
using Verse;
#endif


namespace SimplePathfinding
{
class PQNode
{
	internal PQNode parent;
	internal PQNode left;
	internal PQNode right;
	private readonly List<int> val = new List<int>();
	public int f;

	public void push(int v)
	{
		val.Add(v);
	}

	internal int pop(out int v)
	{
		v = val[0];
		val.RemoveAt(0);
		return val.Count;
	}

	public int remove(int v)
	{
		val.Remove(v);
		return val.Count;
	}

	public void clear()
	{
		left = null;
		right = null;
		parent = null;
		val.Clear();
	}
}

class PQ
{
#if DEBUG_LOG
	private int mQueueCount;
	private int mQueueCountMax;
	private int mInsertCount;
	private int mPopCount;
	private int mDecraseCount;
	private int mRemoveCount;
	private int mTraverseCount;
#endif
	private PQNode mRoot;
	private PQNode mPool;
	private PQNode mLeftMost;

	public PQNode push(int f, int v)
	{
#if DEBUG_LOG
		mInsertCount++;
#endif
		PQNode node = mLeftMost;
		if(node == null){// tree is empty
			node = getNood();
			node.f = f;
			node.push(v);
			mLeftMost = node;
			mRoot = node;
			return node;
		}

		{
			int df = f - node.f;
			if(df == 0){
				node.push(v);
				return node;
			}
			if(df < 0){
				PQNode new_node = getNood();
				new_node.f = f;
				new_node.push(v);
				new_node.parent = node;
				node.left = new_node;
				mLeftMost = new_node;
				return new_node;
			}
			PQNode p = node.parent;
			if(p != null && f < p.f && node.right != null){
				return push_sub(node.right, f, v);
			}
		}
		
		return push_sub(mRoot, f, v);
	}

	private PQNode push_sub(PQNode node, int f, int v)
	{
		for(;;){
			if(node.f == f){
				node.push(v);
				return node;
			}

			if(node.f < f){
				if(node.right == null){
					PQNode i = getNood();
					i.f = f;
					i.push(v);
					i.parent = node;
					node.right = i;
					return i;
				}else{
					node = node.right;
				}
			}else{
				if(node.left == null){
					PQNode i = getNood();
					i.f = f;
					i.push(v);
					i.parent = node;
					node.left = i;
					return i;
				}else{
					node = node.left;
				}
			}
#if DEBUG_LOG
			mTraverseCount++;
#endif
		}
	}
	
	public bool pop(out int v)
	{
#if DEBUG_LOG
		mPopCount++;
#endif
		PQNode n = mLeftMost;
		if(n == null){
			v = 0;
			return false;
		}
		
		int count = n.pop(out v);
		if(count == 0){
			PQNode p = n.parent;
			PQNode right = n.right;
			if(p == null){
				mRoot = right;
			}else{
				p.left = right;
			}
			if(right == null){
				mLeftMost = p;
			}else{
				right.parent = p;
				while(right.left != null){
					right = right.left;
#if DEBUG_LOG
					mTraverseCount++;
#endif
				}
				mLeftMost = right;
			}
			releaseOne(n);
		}

		return true;
	}

	public PQNode decrase(PQNode node, int f, int v)
	{
#if DEBUG_LOG
		mDecraseCount++;
#endif
		PQNode p = node.parent;

		int count = node.remove(v);
		if(count == 0){
			remove(node);
		}

		return push(f, v);
	}

	private void remove(PQNode node)
	{
#if DEBUG_LOG
		mRemoveCount++;
#endif
		PQNode new_node;
		
		int mask = (node.left != null) ? 1 : 0;
		if(node.right != null)
			mask |= 2;
		switch(mask){
		case 0:
			new_node = null;
			break;

		case 1:
			new_node = node.left;
			break;

		case 2:
			new_node = node.right;
			break;

		default:
		{
			PQNode max_node = node.left;
			while(max_node.right != null){
				max_node = max_node.right;
#if DEBUG_LOG
				mTraverseCount++;
#endif
			}

			new_node = max_node;
			max_node.right = node.right;
			node.right.parent = max_node;

			PQNode pp = max_node.parent;
			if(pp != node){
				pp.right = max_node.left;
				if(max_node.left != null)
					max_node.left.parent = pp;
				max_node.left = node.left;
				node.left.parent = max_node;
			}
		}
			break;
		}

		PQNode p = node.parent;
		if(p == null){
			mRoot = new_node;
		}else{
			if(p.left == node)
				p.left = new_node;
			else
				p.right = new_node;
		}
		if(new_node != null)
			new_node.parent = p;

		if(node == mLeftMost){
			if(new_node == null){
				mLeftMost = mLeftMost.parent;
			}else{
				PQNode r = new_node;
				while(r.left != null){
					r = r.left;
#if DEBUG_LOG
					mTraverseCount++;
#endif
				}
				mLeftMost = r;
			}
		}

		releaseOne(node);
	}

	public void clear(bool log)
	{
		releaseAll(mRoot);
		mRoot = null;
		mLeftMost = null;

#if DEBUG_LOG
		if(log){
			Log.Warning("Q L:" + mQueueCount);
			Log.Warning("Q M:" + mQueueCountMax);
			Log.Warning("Q I:" + mInsertCount);
			Log.Warning("Q P:" + mPopCount);
			Log.Warning("Q D:" + mDecraseCount);
			Log.Warning("Q R:" + mRemoveCount);
			Log.Warning("Q T:" + mTraverseCount);
		}
		mInsertCount = 0;
		mPopCount = 0;
		mDecraseCount = 0;
		mRemoveCount = 0;
		mTraverseCount = 0;
#endif
	}

	private void releaseOne(PQNode n)
	{
		n.clear();
		n.right = mPool;
		mPool = n;
	}

	private void releaseAll(PQNode n)
	{
		mRoot = null;
		if(n == null)
			return;
		releaseAll(n.left);
		releaseAll(n.right);
#if DEBUG_LOG
		mTraverseCount++;
#endif
		n.clear();
		n.right = mPool;
		mPool = n;
	}

	private PQNode getNood()
	{
		PQNode n = mPool;
		if(n != null){
			mPool = n.right;
			n.right = null;
			return n;
		}
#if DEBUG_LOG
		mQueueCount++;
		if(mQueueCount > mQueueCountMax)
			mQueueCountMax = mQueueCount;
#endif

		return new PQNode();
	}
}

}
#endif