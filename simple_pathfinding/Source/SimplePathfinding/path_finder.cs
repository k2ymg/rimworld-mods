//#define DEBUG_LOG_ERROR
//#define DEBUG_DRAW_OPEN_NODE
//#define DEBUG_DRAW_CLOSE_NODE
//#define DEBUG_DRAW_REGION_PATH
//#define DEBUG_DRAW_NODESET
//#define DEBUG_DRAW_NODESET_TARGET

using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;


namespace SimplePathfinding {
[StaticConstructorOnStartup]
public static class PatchPathFinder {
	private static readonly Func<PathFinder, Map> getMap = Utils.FieldGetter<PathFinder, Map>("map");

	static PatchPathFinder()
	{
		try{
			var harmony = HarmonyInstance.Create("com.github.k2ymg.improveperformance");
			patch(harmony);
		}catch(Exception e){
			Log.Error(e.ToString());
		}
	}

	public static bool PathFinder_ctor(Map map, PathFinder __instance)
	{
		FieldInfo field = __instance.GetType().GetField("map", BindingFlags.NonPublic | BindingFlags.Instance);
		if(field == null){
			Log.Error("Failed to get mMap of PathFinder");
			return true;
		}
		field.SetValue(__instance, map);

		return false;
	}

	public static bool PathFinder_FindPath(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode, PathFinder __instance, ref PawnPath __result)
	{
		Map map = getMap(__instance);

		__result = MyPathFinder.FindPath(start, dest, traverseParms, peMode, map);

		return false;
	}

	public static void patch(HarmonyInstance harmony)
	{
		Type target_class = typeof(PatchPathFinder);
		Type original_class = typeof(PathFinder);

		MethodInfo target_method = target_class.GetMethod("PathFinder_ctor");
		if(target_method == null){
			Log.Error("PathFinder_ctor failed");
			return;
		}
		MethodBase original_method = original_class.GetConstructor(new Type[]{typeof(Map)});
		if(original_method == null){
			Log.Error("PathFinder.ctor failed");
			return;
		}
		harmony.Patch(original_method, new HarmonyMethod(target_method));

		target_method = target_class.GetMethod("PathFinder_FindPath");
		if(target_method == null){
			Log.Error("PathFinder_FindPath failed");
			return;
		}
		original_method = original_class.GetMethod("FindPath",
			new Type[]{typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode)});
		if(original_method == null){
			Log.Error("PathFinder.FindPath failed");
			return;
		}
		harmony.Patch(original_method, new HarmonyMethod(target_method));
	}
}

public static class MyPathFinder
{
	public static PawnPath FindPath(IntVec3 start, LocalTargetInfo dest, TraverseParms tp, PathEndMode peMode, Map map)
	{
		if(tp.mode == TraverseMode.ByPawn){
			Pawn pawn = tp.pawn;
			if(!pawn.CanReach(dest, peMode, Danger.Deadly, tp.canBash, tp.mode))
				return PawnPath.NotFound;
		}else if(!map.reachability.CanReach(start, dest, peMode, tp)){
			return PawnPath.NotFound;
		}
		
		PawnPath ret = PawnPath.NotFound;

		if(setup(start, dest, tp, peMode, map))
			return PawnPath.NotFound;

		ret = findPath(start.x, start.z);
#if DEBUG_LOG_ERROR
		if(ret == PawnPath.NotFound){
			Pawn pawn = tp.pawn;
			Log.Error("path not found: MapSize=" + sMapW + "," + sMapH);
			Log.Error("path not found: start=" + start.x + "," + start.z);
			Log.Error("path not found: dstRoot=" + sEndX + "," + sEndY);
			Log.Error("path not found: dstRect=" + sDestRect);
			if(pawn != null){
				Log.Error("path not found: name=" + pawn.Name);
			}
		
			sMap.debugDrawer.FlashCell(new IntVec3(sEndX, 0, sEndY), 0.5f, "X", 100);
			sMap.debugDrawer.FlashCell(new IntVec3(sDestX0, 0, sDestY0), 0.5f, "LT", 100);
			sMap.debugDrawer.FlashCell(new IntVec3(sDestX1, 0, sDestY1), 0.5f, "RB", 100);
			sMap.debugDrawer.FlashLine(new IntVec3(start.x, 0, start.z), new IntVec3(sEndX, 0, sEndY), 100, SimpleColor.Yellow);
		}
#endif

		OpenSet_release();
		NodeSet_release();

		return ret;
	}

	private static bool setup(IntVec3 s, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode, Map map)
	{
		sMap = map;
		sMapW = map.Size.x;
		sMapH = map.Size.z;

		if(s.x < 0 || s.y < 0 || s.z < 0 || s.x >= sMapW || s.z >= sMapH){
#if DEBUG_LOG_ERROR
			Log.Error("Start point is out side of map:" + s);
#endif
			return true;
		}
		IntVec3 e = dest.Cell;
		if(e.x < 0 || e.y < 0 || e.z < 0 || e.x >= sMapW || e.z >= sMapH){
#if DEBUG_LOG_ERROR
			Log.Error("End point is out side of map:" + e);
#endif
			return true;
		}

		if(dest.HasThing && dest.Thing.Map != map){
#if DEBUG_LOG_ERROR
			Log.Error("Tartget thing is not this map:" + dest.Thing.ThingID);
#endif
			return true;
		}

		int w = (sMapW + NodeSetBitMask) >> NodeSetBitShift;
		sMapW4 = w;
		int h = (sMapH + NodeSetBitMask) >> NodeSetBitShift;
		int wh_size = w * h;
		if(sNodeSet == null || sNodeSet.Length < wh_size)
			sNodeSet = new NodeSet[wh_size];

		sEndX = dest.Cell.x;
		sEndY = dest.Cell.z;

		sPathGrid = sMap.pathGrid;
		sPathGridArray = sMap.pathGrid.pathGrid;
		sEdifice = sMap.edificeGrid.InnerArray;
		sTopGrid = sMap.terrainGrid.topGrid;
		sBlueprint = sMap.blueprintGrid.InnerArray;

		sTraverseParms = traverseParms;
		Pawn pawn = traverseParms.pawn;
		if(pawn != null){
			if(!pawn.Spawned || pawn.Map != map){
#if DEBUG_LOG_ERROR
				Log.Error("Pawn is not spawned or diff map");
#endif
				return true;
			}

			sCardinal = pawn.TicksPerMoveCardinal;
			sDiagonal = pawn.TicksPerMoveDiagonal;
			sAvoidGrid = pawn.GetAvoidGrid(true);
			sAllowedArea = allowedArea(pawn);
			sDrafted = pawn.Drafted;
			sShouldCollideWithPawns = PawnUtility.ShouldCollideWithPawns(pawn);
		}else{
			sCardinal = DefaultMoveTicksCardinal;
			sDiagonal = DefaultMoveTicksDiagonal;
			sAvoidGrid = null;
			sAllowedArea = null;
			sDrafted = false;
			sShouldCollideWithPawns = false;
		}
		sD2C = sDiagonal - (2 * sCardinal);

		sAllowWater = 
			traverseParms.mode != TraverseMode.NoPassClosedDoorsOrWater &&
			traverseParms.mode != TraverseMode.PassAllDestroyableThingsNotWater;
		sPassAllDestroyableThings =
			traverseParms.mode == TraverseMode.PassAllDestroyableThings ||
			traverseParms.mode == TraverseMode.PassAllDestroyableThingsNotWater;

		setupDestRect(dest, traverseParms, peMode, sMap);

		return false;
	}

	private static PawnPath findPath(int x, int y)
	{
		int cg = 0;
		int xy = (x << 16) | y;
		Node_setStart(x, y);
		sParentXY = xy;

		for(;;){
			if(insideDest(x, y))
				goto found;

			sCurrentX = x;
			sCurrentY = y;
			sCurrentXY = xy;
			processNodes(x, y, cg);

			if(!sOpenNodeQueue.pop(out xy))
				break;
			x = xy >> 16;
			y = xy & 0xffff;

			ref Node node = ref Node_get(x, y);
#if DEBUG_DRAW_CLOSE_NODE
			if(sDrafted && node.q != null){
				IntVec3  c = new IntVec3(x, 0, y);
				int v = node.q.f;
				sMap.debugDrawer.FlashCell(c, 0.5f, v.ToString(), 100);
			}
#endif
			cg = node.g;
			node.q = null;// close
			node.closed = true;
			sParentXY = node.p;
		}

		return PawnPath.NotFound;

	found:
		return makePath(x, y);
	}

	private static PawnPath makePath(int x, int y)
	{
		PawnPath path = sMap.pawnPathPool.GetEmptyPawnPath();

		IntVec3 v = new IntVec3(x, 0, y);
		for(;;){
			path.AddNode(v);
			ref Node node = ref Node_get(x, y);
			int xy = node.p;
			if(xy < 0)
				break;
			x = v.x = xy >> 16;
			y = v.z = xy & 0xffff;
		}

		path.SetupFound(0, false);
		return path;
	}

	// 4 2 5
	// 0   1
	// 6 3 7
	private static void processNodes(int x, int y, int cg)
	{
		// BTRL_BxTxRxLx_BxTxRxLx
		int flags;

		int l = x - 1;
		int r = x + 1;
		int t = y - 1;
		int b = y + 1;

		if(l < 0)
			flags  = 0b0001_00000000_00000011;
		else
			flags  = sCell0.setup(l, y);
		if(r >= sMapW)
			flags |= 0b0010_00000000_00001100;
		else
			flags |= sCell1.setup(r, y) << 2;
		if(t < 0)
			flags |= 0b0100_00000000_00110000;
		else
			flags |= sCell2.setup(x, t) << 4;
		if(b >= sMapH)
			flags |= 0b1000_00000000_11000000;
		else
			flags |= sCell3.setup(x, b) << 6;


		if((flags & 0b0101_00000000_00110011) != 0)
			flags |= 0b00000011_00000000;
		else
			flags |= sCell4.setup(l, t) << 8;
		
		if((flags & 0b0110_00000000_00111100) != 0)
			flags |= 0b00001100_00000000;
		else
			flags |= sCell5.setup(r, t) << 10;

		if((flags & 0b1001_00000000_11000011) != 0)
			flags |= 0b00110000_00000000;
		else
			flags |= sCell6.setup(l, b) << 12;

		if((flags & 0b1010_00000000_11001100) != 0)
			flags |= 0b11000000_00000000;
		else
			flags |= sCell7.setup(r, b) << 14;


		int parent_cost;
		int px = sParentXY >> 16;
		int py = sParentXY & 0xffff;
		if(px == x || py == y){
			parent_cost = cg + sCardinal;
			if((flags & 0b00000000_00000001) == 0)
				sCell0.update(parent_cost);
			if((flags & 0b00000000_00000100) == 0)
				sCell1.update(parent_cost);
			if((flags & 0b00000000_00010000) == 0)
				sCell2.update(parent_cost);
			if((flags & 0b00000000_01000000) == 0)
				sCell3.update(parent_cost);

			parent_cost = cg + sDiagonal;
			if((flags & 0b00000001_00000000) == 0)
				sCell4.update(parent_cost);
			if((flags & 0b00000100_00000000) == 0)
				sCell5.update(parent_cost);
			if((flags & 0b00010000_00000000) == 0)
				sCell6.update(parent_cost);
			if((flags & 0b01000000_00000000) == 0)
				sCell7.update(parent_cost);
		}else{
			parent_cost = cg + sDiagonal;
			if((flags & 0b00000001_00000000) == 0)
				sCell4.update(parent_cost);
			if((flags & 0b00000100_00000000) == 0)
				sCell5.update(parent_cost);
			if((flags & 0b00010000_00000000) == 0)
				sCell6.update(parent_cost);
			if((flags & 0b01000000_00000000) == 0)
				sCell7.update(parent_cost);

			parent_cost = cg + sCardinal;
			if((flags & 0b00000000_00000001) == 0)
				sCell0.update(parent_cost);
			if((flags & 0b00000000_00000100) == 0)
				sCell1.update(parent_cost);
			if((flags & 0b00000000_00010000) == 0)
				sCell2.update(parent_cost);
			if((flags & 0b00000000_01000000) == 0)
				sCell3.update(parent_cost);
		}
	}

	private struct Node {
		public PQNode q;
		public int p;
		public int g;
		public int h;
		public short c;
		public sbyte door_bit;
		public bool closed; 
	}

#if true
	public const int NodeSetSize = 8;
	public const int NodeSetBitShift = 3;
	public const int NodeSetBitMask = 7;
#else
	public const int NodeSetSize = 4;
	public const int NodeSetBitShift = 2;
	public const int NodeSetBitMask = 3;
#endif

	private class NodeSet {
		public NodeSet next;
		public readonly Node[] node = new Node[NodeSetSize * NodeSetSize];
		public int back_ref_index;

		public void clear()
		{
			Array.Clear(node, 0, NodeSetSize * NodeSetSize);
		}
	}

	private struct Cell {
		private NodeSet nodeset;
		private int local;
		private ushort x;
		private ushort y;

		public int setup(int x, int y)
		{
			this.x = (ushort)x;
			this.y = (ushort)y;
			int index = xy2node(x, y, out local);
			nodeset = NodeSet_get(index);
			ref Node node = ref nodeset.node[local];

			int cost = node.c;
			if(cost == 0){
				cost = getMapCost(x, y, out bool is_door);
				node.c = (short)cost;
				if(cost > 0){
					node.h = distanceToEnd(x, y);
					if(is_door)
						node.door_bit = 2;
					else
						node.door_bit = 0;
				}else{
					node.door_bit = 3;// = 2 | 1
				}
			}
			return node.door_bit;
		}

		public void update(int parent_cost)
		{
			ref Node node = ref nodeset.node[local];
			int c = node.c;
			if(c < 0){
				return;// impassable
			}

			c += parent_cost;

			PQNode q = node.q;
			if(q == null){
				// new
				if(node.closed){
					if(c >= node.g)
						return;
					node.closed = false;
				}
				node.g = c;
				node.q = sOpenNodeQueue.push(c + node.h, (x << 16) | y);
				node.p = sCurrentXY;
			}else{
				// open
				if(c < node.g){
					node.g = c;
					node.q = sOpenNodeQueue.decrase(q, c + node.h, (x << 16) | y);
					node.p = sCurrentXY;
				}
			}
		}
	}

	private static int xy2node(int x, int y, out int local_index)
	{
		local_index = ((y & NodeSetBitMask) << NodeSetBitShift) | (x & NodeSetBitMask);
		return (sMapW4 * (y >> NodeSetBitShift)) + (x >> NodeSetBitShift);
	}

	private static void Node_setStart(int x, int y)
	{
		ref Node node = ref Node_get(x, y);
		node.p = -1;
		node.c = -1;
	}

	private static ref Node Node_get(int x, int y)
	{
		int offset = ((y & NodeSetBitMask) << NodeSetBitShift) | (x & NodeSetBitMask);
		int index = (sMapW4 * (y >> NodeSetBitShift)) + (x >> NodeSetBitShift);
		NodeSet nodeset = NodeSet_get(index);
		return ref nodeset.node[offset];
	}

	private static void OpenSet_release()
	{
#if DEBUG_DRAW_OPEN_NODE
		if(sDrafted){
			int xy;
			while(sOpenNodeQueue.pop(out xy)){
				int x = xy >> 16;
				int y = xy & 0xffff;
				IntVec3 c = new IntVec3(x, 0, y);			
				sMap.debugDrawer.FlashCell(c, 0f, "open", 100);
			}
		}
#endif
		sOpenNodeQueue.clear(sDrafted);
	}

	private static NodeSet NodeSet_get(int index)
	{
		NodeSet node = sNodeSet[index];
		if(node == null){
			node = sNodeSetPool;
			if(node == null){
				node = new NodeSet();
			}else{
				sNodeSetPool = node.next;
				node.clear();
			}

			node.next = sNodeSetUsed;
			sNodeSetUsed = node;

			node.back_ref_index = index;
			sNodeSet[index] = node;
		}

		return node;
	}

	private static void NodeSet_release()
	{
		NodeSet n = sNodeSetUsed;
		if(n == null)
			return;

		for(;;){
#if DEBUG_DRAW_NODESET
			if(sDrafted){
				int index = n.index;
				int y0 = (index / sMapW4) * NodeSetSize;
				int y1 = y0 + NodeSetSize;
				int x0 = (index % sMapW4) * NodeSetSize;
				int x1 = x0 + NodeSetSize;
				int local = 0;
				for(int y = y0; y < y1; y++){
					for(int x = x0; x < x1; x++){
						int tc = n.node[local++].terrain_cost;
						IntVec3 c = new IntVec3(x, 0, y);
						if(n.target_init)
							sMap.debugDrawer.FlashCell(c, 0.5f, tc.ToString(), 100);
						else
							sMap.debugDrawer.FlashCell(c, 0.8f, tc.ToString(), 100);
						
					}
				}
			}
#endif
#if DEBUG_DRAW_NODESET_TARGET
			if(sDrafted){
				if(n.target_init){
					int index = n.index;
					int y0 = (index / sMapW4) * NodeSetSize;
					int x0 = (index % sMapW4) * NodeSetSize;
					int qx = n.target_x;
					int qy = n.target_y;
					sMap.debugDrawer.FlashLine(new IntVec3(x0, 0, y0),
						new IntVec3(qx, 0, qy), 100, SimpleColor.Yellow);
				}
			}
#endif
			sNodeSet[n.back_ref_index] = null;
			if(n.next == null)
				break;
			n = n.next;
		}

		n.next = sNodeSetPool;
		sNodeSetPool = sNodeSetUsed;
		sNodeSetUsed = null;
	}

	private static int distanceToEnd(int x, int y)
	{
		int dx = Mathf.Abs(sEndX - x);
		int dy = Mathf.Abs(sEndY - y);
		return sCardinal * (dx + dy) + sD2C * Mathf.Min(dx, dy);
	}

	private static NodeSet[] sNodeSet;
	private static NodeSet sNodeSetUsed;
	private static NodeSet sNodeSetPool;
	private static readonly PQ sOpenNodeQueue = new PQ();

	private static int sMapW;
	private static int sMapH;
	private static int sMapW4;
	private static int sEndX;
	private static int sEndY;
	private static int sCardinal;
	private static int sDiagonal;
	private static int sD2C;

	private static int sCurrentXY;
	private static int sCurrentX;
	private static int sCurrentY;
	private static int sParentXY;

	private static Cell sCell0;
	private static Cell sCell1;
	private static Cell sCell2;
	private static Cell sCell3;
	private static Cell sCell4;
	private static Cell sCell5;
	private static Cell sCell6;
	private static Cell sCell7;

	private static Map sMap;
	private static PathGrid sPathGrid;
	private static int[] sPathGridArray;
	private static Building[] sEdifice;
	private static List<Blueprint>[] sBlueprint;
	private static ByteGrid sAvoidGrid;
	private static TerrainDef[] sTopGrid;
	private static Area sAllowedArea;
	private static bool sAllowWater;
	private static bool sDrafted;
	private static bool sShouldCollideWithPawns;
	private static bool sPassAllDestroyableThings;
	private static TraverseParms sTraverseParms;

	public const int DefaultMoveTicksCardinal = 13;
	public const int DefaultMoveTicksDiagonal = 18;

	private static int getMapCost(int x, int y, out bool is_door)
	{
		int cell_index = (sMapW * y) + x;
		is_door = false;

		if(!sAllowWater){
			if(sTopGrid[cell_index].HasTag("Water")){
				return -1;
			}
		}

		Building building = sEdifice[cell_index];

		int cost = sPathGridArray[cell_index];
		if(cost < PathGrid.ImpassableCost){
			if(sDrafted)
				cost += sTopGrid[cell_index].extraDraftedPerceivedPathCost;
			else
				cost += sTopGrid[cell_index].extraNonDraftedPerceivedPathCost;
		}else{
			if(!sPassAllDestroyableThings || building == null || !PathFinder.IsDestroyable(building)){
				return -1;
			}

			cost += (int)((float)building.HitPoints * 0.2f) + 70;
		}

		if(building != null){
			int c = PathFinder.GetBuildingCost(building, sTraverseParms, sTraverseParms.pawn);
			if(c == int.MaxValue){
				return -1;
			}
			if((building as Building_Door) != null){
				if(smallDeadEndRoom(x, y)){
					return -1;
				}
				is_door = true;
			}
			cost += c;
		}

		if(sAvoidGrid != null)
			cost += (int)(sAvoidGrid[cell_index] * 8);
	
		if(sAllowedArea != null && !sAllowedArea[cell_index])
			cost += 600;
	
		if(sShouldCollideWithPawns && PawnUtility.AnyPawnBlockingPathAt(new IntVec3(x, 0, y), sTraverseParms.pawn, false, false, true))
		{
			cost += 175;
		}

		List<Blueprint> list = sBlueprint[cell_index];
		if(list != null){
			int c = 0;
			int j = list.Count;
			while(j-- > 0)
				c = Mathf.Max(c, PathFinder.GetBlueprintCost(list[j], sTraverseParms.pawn));
			if(c == int.MaxValue){
				return -1;
			}
			cost += c;
		}

		return cost + 1;// zero is special value, so always offset + 1.
	}

	private static Area allowedArea(Pawn pawn)
	{
		if(pawn.playerSettings == null || pawn.Drafted || !ForbidUtility.CaresAboutForbidden(pawn, true))
			return null;

		Area area = pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap;
		if(area != null){
			if(area.TrueCount <= 0)
				return null;
		}

		return area;
	}

	private static int sDestX0;
	private static int sDestY0;
	private static int sDestX1;
	private static int sDestY1;
	private static int sDestCornerX00;
	private static int sDestCornerX01;
	private static int sDestCornerX10;
	private static int sDestCornerX11;
	private static CellRect sDestRect;

	private static void setupDestRect(LocalTargetInfo dest, TraverseParms tp, PathEndMode peMode, Map map)
	{
		int x0, y0, x1, y1;

		if(!dest.HasThing || peMode == PathEndMode.OnCell){
			x0 = x1 = dest.Cell.x;
			y0 = y1 = dest.Cell.z;
		}else{
			CellRect cr = dest.Thing.OccupiedRect();
			x0 = cr.minX;
			y0 = cr.minZ;
			x1 = cr.maxX;
			y1 = cr.maxZ;
		}
		if(peMode == PathEndMode.Touch){
			x0--;
			y0--;
			x1++;
			y1++;
		}
		
		sDestX0 = x0;
		sDestY0 = y0;
		sDestX1 = x1;
		sDestY1 = y1;
		sDestCornerX00 = x0;
		sDestCornerX01 = x1;
		sDestCornerX10 = x0;
		sDestCornerX11 = x1;
		sDestRect.minX = x0;
		sDestRect.maxX = x1;
		sDestRect.minZ = y0;
		sDestRect.maxZ = y1;

		if(peMode == PathEndMode.Touch){
			if(!TouchPathEndModeUtility.IsCornerTouchAllowed(x0 + 1, y0 + 1, x0 + 1, y0, x0, y0 + 1, map))
				sDestCornerX00++;
			if(!TouchPathEndModeUtility.IsCornerTouchAllowed(x1 - 1, y0 + 1, x1 - 1, y0, x1, y0 + 1, map))
				sDestCornerX01--;
			if(!TouchPathEndModeUtility.IsCornerTouchAllowed(x0 + 1, y1 - 1, x0 + 1, y1, x0, y1 - 1, map))
				sDestCornerX10++;
			if(!TouchPathEndModeUtility.IsCornerTouchAllowed(x1 - 1, y1 - 1, x1 - 1, y1, x1, y1 - 1, map))
				sDestCornerX11--;
		}
	}

	private static bool insideDest(int x, int y)
	{
		if(x < sDestX0 || sDestX1 < x || y < sDestY0 || sDestY1 < y)
			return false;
		
		if(y == sDestY0){
			if(x < sDestCornerX00 || sDestCornerX01 < x)
				return false;
		}else if(y == sDestY1){
			if(x < sDestCornerX10 || sDestCornerX11 < x)
				return false;
		}

		return true;
	}

	private static bool smallDeadEndRoom(int x, int y)
	{
		Region[] map_regions = sMap.regionGrid.DirectGrid;
		int index = sMap.cellIndices.CellToIndex(x, y);
		Region r_door = map_regions[index];
		if(r_door == null || r_door.door == null)
			return false;// not door

		IntVec3 end = new IntVec3(sEndX, 0, sEndY);
		if(r_door.extentsClose.Contains(end))
			return false;// contain destination

		if(r_door.links.Count != 2)
			return false;// defective door

		int px = sCurrentX;
		int py = sCurrentY;
		Region r_a = map_regions[sMap.cellIndices.CellToIndex(px, py)];
		Region r_b = otherSideRegion(r_door, r_a);
		if(r_b == null)
			return false;// no other side (maybe not come here)
		if(r_b.IsDoorway)
			return false;// anather door...

		Room room = r_b.Room;
		List<Region> regions = room.Regions;
		if(regions.Count > 4)
			return false;// big or complex room

		Pawn pawn = sTraverseParms.pawn;
		foreach(Region region in regions){
			if(region.extentsClose.Overlaps(sDestRect))
				return false;// contain goal
			foreach(RegionLink link in region.links){
				Region r = link.GetOtherRegion(region);
				if(!r.IsDoorway)
					continue;
				if(r == r_door)
					continue;
				if(!r.type.Passable())
					continue;
				int cost = PathFinder.GetBuildingCost(r.door, sTraverseParms, pawn);
				if(cost == int.MaxValue)
					continue;
				r_b = otherSideRegion(r, region);
				if(r_b == null)
					continue;
				if(r_b.Room != room)
					return false;
			}
		}

		return true;
	}

	private static Region otherSideRegion(Region region, Region r_a)
	{
		foreach(RegionLink link in region.links){
			Region r_b = link.GetOtherRegion(region);
			if(r_b != r_a)
				return r_b;
		}
		return null;
	}
}
}
