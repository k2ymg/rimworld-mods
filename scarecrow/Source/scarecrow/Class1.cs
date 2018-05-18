using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;


namespace Scarecrow
{
	public static class ApparelList
	{
		public static List<ThingDef> allApparels { get; private set; }
		public static List<ThingDef> allHeadwear { get; private set; }

		public static void prepare(List<StuffCategoryDef> stuff)
		{
			if(allApparels != null)
				return;

			allApparels = new List<ThingDef>();

			List<ThingDef> all = DefDatabase<ThingDef>.AllDefsListForReading;
			foreach(ThingDef def in all){
				if(!def.IsApparel)
					continue;
				if(def.apparel.wornGraphicPath.NullOrEmpty())
					continue;// pants, shield-belt, etc...

				if(stuff != null){
					if(def.stuffCategories != null){
						foreach(StuffCategoryDef s in stuff){
							if(def.stuffCategories.Contains(s))
								goto found;
						}
					}
					continue;
				}

			found:
				allApparels.Add(def);
			}
		}
	}


	public class Building_Scarecrow : Building, IAttackTarget
	{
		// see PawnGenerator
		private void generate_pawn()
		{
			Gender gender;
			if(Rand.Value < 0.5f)
				gender = Gender.Male;
			else
				gender = Gender.Female;

			Faction f = Faction.OfPlayer;

			float melanin = PawnSkinColors.RandomMelanin(f);
			skinColor = PawnSkinColors.GetSkinColor(melanin);
			int age = 20;
			hairColor = PawnHairColors.RandomHairColor(skinColor, age);
			hairDef = RandomHairDefFor(gender, f.def);

			crownType = Rand.Value >= 0.5f ? CrownType.Narrow : CrownType.Average;

			headGraphicPath = GraphicDatabaseHeadRecords.GetHeadRandom(
				gender, skinColor, crownType).GraphicPath;

			bodyType = BodyType.Male;
		}

		// see PawnApparelGenerator
		private void generate_apparel()
		{
			ApparelList.prepare(def.stuffCategories);

			List<ThingDef> apparels = new List<ThingDef>();

			ThingDef[] tmp1 = ApparelList.allApparels.ToArray();
			int tmp1_len = tmp1.Length;

			tmp1_len = filterApparels(tmp1, tmp1_len, delegate(ThingDef def)
			{
				if(def.researchPrerequisites != null){
					foreach(ResearchProjectDef r in def.researchPrerequisites){
						if(!r.IsFinished)
							return false;
					}
				}
				return true;
			});

			while(tmp1_len > 0){
				int index = Rand.Range(0, tmp1_len);
				if(index >= tmp1_len)
					break;
				ThingDef def = tmp1[index];
				apparels.Add(def);
				bool def_is_headwear = IsHeadwear(def);
				// remove conflicted apparels
				tmp1_len = filterApparels(tmp1, tmp1_len, delegate(ThingDef d)
				{
					if(def_is_headwear && IsHeadwear(d))
						return false;

					foreach(ApparelLayer layer in def.apparel.layers){
						if(!d.apparel.layers.Contains(layer))
							continue;
						foreach(BodyPartGroupDef group in def.apparel.bodyPartGroups){
							if(d.apparel.bodyPartGroups.Contains(group))
								return false;
						}
					}
					return true;
				});
			}

			apparelDef = apparels;
		}

		private void resolveGraphics()
		{
			if(nakedGraphic != null)
				return;

			nakedGraphic = GraphicGetter_NakedHumanlike.GetNakedBodyGraphic(
				bodyType, ShaderDatabase.CutoutSkin, skinColor);

			headGraphic = GraphicDatabaseHeadRecords.GetHeadNamed(
				headGraphicPath, skinColor);

			hairGraphic = GraphicDatabase.Get<Graphic_Multi>(
				hairDef.texPath, ShaderDatabase.Cutout, Vector2.one, hairColor);

			if(apparelDef != null){
				Color color;
				if(Stuff != null)
					color = Stuff.stuffProps.color;
				else
					color = Color.white;

				apparelGraphic = new Graphic[apparelDef.Count];
				for(int i = 0; i < apparelDef.Count; i++){
					apparelGraphic[i] = TryGetGraphicApparel(apparelDef[i], bodyType, color);
				}
			}
		}

		private void drawPawn()
		{
			if(nakedGraphic == null)
				return;

			Rot4 facing = Rot4.South;
			bool portrait = false;
			Vector3 pos = DrawPos;
			Quaternion q = Quaternion.identity;

			Mesh body_mesh;

			pos.z += 0.5f;

			// body
			{
				Vector3 p = pos;
				p.y += YOffset_Body;

				body_mesh = MeshPool.humanlikeBodySet.MeshAt(facing);
				{
					Material mat = nakedGraphic.MatAt(facing, null);
					GenDraw.DrawMeshNowOrLater(body_mesh, p, q, mat, portrait);
				}
				
				// apparel
				if(apparelGraphic != null){
					Graphic[] ag = apparelGraphic;
					for(int i = 0; i < ag.Length; i++){
						if(ag[i] == null)
							continue;
						ThingDef d = apparelDef[i];
						if(d.apparel.LastLayer == ApparelLayer.Shell ||
							d.apparel.LastLayer == ApparelLayer.Overhead)
							continue;
						Material mat = ag[i].MatAt(facing, null);
						GenDraw.DrawMeshNowOrLater(body_mesh, p, q, mat, portrait);
					}
				}
			}

			if(headGraphic != null){
				Vector3 head_offset = q * BaseHeadOffsetAt(bodyType, facing);

				// head
				{
					Vector3 p = pos + head_offset;
					if(facing == Rot4.North)
						p.y += YOffset_Shell;
					else
						p.y += YOffset_Head;

					Material mat = headGraphic.MatAt(facing, null);
					Mesh mesh = MeshPool.humanlikeHeadSet.MeshAt(facing);
					GenDraw.DrawMeshNowOrLater(mesh, p, q, mat, portrait);
				}

				// apparel(head) and hair
				{
					bool capped = false;
					Mesh mesh = HairMeshSet(crownType).MeshAt(facing);

					if(apparelGraphic != null){
						Graphic[] ag = apparelGraphic;
						for(int i = 0; i < ag.Length; i++){
							if(ag[i] == null)
								continue;

							ThingDef d = apparelDef[i];
							if(d.apparel.LastLayer != ApparelLayer.Overhead)
								continue;
							Vector3 p = pos + head_offset;
							if(d.apparel.hatRenderedFrontOfFace){
								if(facing == Rot4.North)
									p.y += YOffset_Behind;
								else
									p.y += YOffset_PostHead;
							}else{
								p.y += YOffset_OnHead;
								capped = true;
							}

							Material mat = ag[i].MatAt(facing, null);
							GenDraw.DrawMeshNowOrLater(mesh, p, q, mat, portrait);
						}
					}

					// hair
					if(!capped && hairGraphic != null){
						Vector3 p = pos + head_offset;
						p.y += YOffset_OnHead;

						Material mat = hairGraphic.MatAt(facing, null);
						GenDraw.DrawMeshNowOrLater(mesh, p, q, mat, portrait);
					}
				}	
			}

			// apparel(shell)
			if(apparelGraphic != null){
				Vector3 p = pos;
				if(facing == Rot4.North)
					p.y += YOffset_Head;
				else
					p.y += YOffset_Shell;

				Graphic[] ag = apparelGraphic;
				for(int i = 0; i < ag.Length; i++){
					if(ag[i] == null)
						continue;
					ThingDef d = apparelDef[i];
					if(d.apparel.LastLayer != ApparelLayer.Shell)
						continue;

					Material mat = ag[i].MatAt(facing, null);
					GenDraw.DrawMeshNowOrLater(body_mesh, p, q, mat, portrait);
				}
			}
		}

		// Thing
		/*public override Color DrawColor
		{
			get
			{
				return Color.white;
			}
		}
		public override Color DrawColorTwo
		{
			get
			{
				return Color.white;
			}
		}*/
		public override void PostMake()
		{
			base.PostMake();
			generate_pawn();
			generate_apparel();
		}

		// Building
		/*public override bool TransmitsPowerNow
		{
		}*/

		/*public override int HitPoints
		{
		}*/

		/*public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
		}*/

		/*public override void DeSpawn()
		{
			base.DeSpawn();
		}*/

		/*public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			base.Destroy(mode);
		}*/

		public override void Draw()
		{
			resolveGraphics();
			drawPawn();
		}

		/*public override void SetFaction(Faction newFaction, Pawn recruiter = null)
		{
			base.SetFaction(newFaction, recruiter);
		}*/

		/*public override void PreApplyDamage(DamageInfo dinfo, out bool absorbed)
		{
			base.PreApplyDamage(dinfo, out absorbed);
		}*/

		/*public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			base.PostApplyDamage(dinfo, totalDamageDealt);
		}*/

		/*public override void DrawExtraSelectionOverlays()
		{
			base.DrawExtraSelectionOverlays();
		}*/

		/*public override IEnumerable<Gizmo> GetGizmos()
		{
			return base.GetGizmos();
		}*/

		/*public override bool ClaimableBy(Faction by)
		{
			return base.ClaimableBy(by);
		}*/

		/*public override ushort PathFindCostFor(Pawn p)
		{
			return 0;
		}*/

		/*public override ushort PathWalkCostFor(Pawn p)
		{
			return 0;
		}*/

		/*public override bool IsDangerousFor(Pawn p)
		{
			return false;
		}*/

		// IExposable
		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look<BodyType>(ref bodyType, "bodyType");
			Scribe_Values.Look<CrownType>(ref crownType, "crownType");
			Scribe_Values.Look<Color>(ref skinColor, "skinColor");
			Scribe_Values.Look<Color>(ref hairColor, "hairColor");
			Scribe_Defs.Look<HairDef>(ref hairDef, "hairDef");
			Scribe_Values.Look<string>(ref headGraphicPath, "headGraphicPath");
			Scribe_Collections.Look<ThingDef>(ref apparelDef, "apparelDef");
		}

		// IAttackTarget
		Thing IAttackTarget.Thing
		{
			get
			{
				return this;
			}
		}

		public LocalTargetInfo TargetCurrentlyAimingAt
		{
			get
			{
				return null;
			}
		}

		public bool ThreatDisabled()
		{
			return false;
		}


		private BodyType bodyType;
		private CrownType crownType;
		private Color skinColor;
		private Color hairColor;
		private HairDef hairDef;
		private string headGraphicPath;
		private List<ThingDef> apparelDef;

		private Graphic nakedGraphic;
		private Graphic headGraphic;
		private Graphic hairGraphic;
		private Graphic[] apparelGraphic;


		// see PawnRenderer
		private const float YOffset_Body = 0.0078125f;
		private const float YOffsetInterval_Clothes = 0.00390625f;
		private const float YOffset_Head = 0.02734375f;
		private const float YOffset_Shell = 0.0234375f;
		private const float YOffset_OnHead = 0.03125f;
		private const float YOffset_PostHead = 0.03515625f;
		private const float YOffset_Behind = 0.00390625f;
		private static readonly float[] HorHeadOffsets = new float[]
		{
			0f,
			0.04f,
			0.1f,
			0.09f,
			0.1f,
			0.09f
		};

		static private Vector3 BaseHeadOffsetAt(BodyType bodyType, Rot4 rotation)
		{
			float num = HorHeadOffsets[(int)bodyType];
			switch(rotation.AsInt){
			case 0: return new Vector3(0f, 0f, 0.34f);
			case 1: return new Vector3(num, 0f, 0.34f);
			case 2: return new Vector3(0f, 0f, 0.34f);
			case 3: return new Vector3(-num, 0f, 0.34f);
			default: return Vector3.zero;
			}
		}


		// see PawnHairChooser
		private static HairDef RandomHairDefFor(Gender gender, FactionDef factionDef)
		{
			IEnumerable<HairDef> source =
				from hair
				in DefDatabase<HairDef>.AllDefs
				where hair.hairTags.SharesElementWith(factionDef.hairTags)
				select hair;
			return source.RandomElementByWeight((HairDef hair)
				=> HairChoiceLikelihoodFor(hair, gender));
		}

		private static float HairChoiceLikelihoodFor(HairDef hair, Gender gender)
		{
			switch(gender){
			case Gender.None:
				return 100f;

			case Gender.Male:
				switch(hair.hairGender){
				case HairGender.Male: return 70f;
				case HairGender.MaleUsually: return 30f;
				case HairGender.Any: return 60f;
				case HairGender.FemaleUsually: return 5f;
				case HairGender.Female: return 1f;
				}
				break;

			case Gender.Female:
				switch(hair.hairGender){
				case HairGender.Male: return 1f;
				case HairGender.MaleUsually: return 5f;
				case HairGender.Any: return 60f;
				case HairGender.FemaleUsually: return 30f;
				case HairGender.Female: return 70f;
				}
				break;
			}
			return 0f;
		}


		// see PawnGraphicSet
		static private GraphicMeshSet HairMeshSet(CrownType crownType)
		{
			if(crownType == CrownType.Average)
				return MeshPool.humanlikeHairSetAverage;
			return MeshPool.humanlikeHairSetNarrow;
		}


		// see ApparelGraphicRecordGetter
		private static Graphic TryGetGraphicApparel(ThingDef def, BodyType bodyType, Color color)
		{
			if(def == null)
				return null;

			string path = def.apparel.wornGraphicPath;

			if(path.NullOrEmpty())
				return null;

			if(def.apparel.LastLayer != ApparelLayer.Overhead)
				path = string.Format("{0}_{1}", path, bodyType.ToString());

			Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(path,
				ShaderDatabase.Cutout, def.graphicData.drawSize,
				color);

			return graphic;
		}

		private static int filterApparels(ThingDef[] defs, int n, Predicate<ThingDef> validator)
		{
			for(int i = 0; i < n; ){
				ThingDef def = defs[i];
				if(validator(def))
					goto next;

				// reject
				n--;
				if(i >= n)
					break;
				defs[i] = defs[n];
				continue;

			next:
				i++;
			}

			return n;
		}

		private static bool IsHeadwear(ThingDef td)
		{
			return td.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead)
				|| td.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead);
		}
	}
}
