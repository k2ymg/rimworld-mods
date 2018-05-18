using System;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;
using Verse.AI;


namespace SolarPoweredCeilingLight
{
	public class PlaceWorker_Ceiling : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null)
		{
			Thing thing = map.thingGrid.ThingAt(loc, ThingCategory.Building);
			if(thing != null && thing.def.holdsRoof){
				return new AcceptanceReport("RequireRoof".Translate());
			}

			if(map.roofGrid.Roofed(loc)){
				RoofDef roof = map.roofGrid.RoofAt(loc);
				if(roof.isThickRoof){
					return new AcceptanceReport("CannotOnThickRoof".Translate());
				}
			}else{
				return new AcceptanceReport("RequireRoof".Translate());
			}

			return true;
		}

		/*public override void PostPlace(Map map, BuildableDef def, IntVec3 loc, Rot4 rot)
		{
		}*/

		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot)
		{
			GenUI.RenderMouseoverBracket();
		}

		/*public override bool ForceAllowPlaceOver(BuildableDef other)
		{
			return true;
		}*/
	}

	public class CompProperties_SolarAndBatteryGlower : CompProperties_Glower
	{
		public CompProperties_SolarAndBatteryGlower()
		{
			this.compClass = typeof(CompSolarAndBatteryGlower);
		}
	}

	// Why inherit?: because RegisterGlower only accept CompGlower
	public class CompSolarAndBatteryGlower : Verse.CompGlower
	{
		private bool ShouldBeLitNow
		{
			get
			{
				if(!parent.Spawned)
				{
					return false;
				}

				if(!FlickUtility.WantsToBeOn(parent))
				{
					return false;
				}

				CompSolarAndBattery comp = parent.TryGetComp<CompSolarAndBattery>();
				if(comp != null && !comp.PowerOn)
				{
					return false;
				}

				return true;
			}
		}

		public new void UpdateLit(Map map)
		{
			bool shouldBeLitNow = ShouldBeLitNow;
			if(glowOnInt == shouldBeLitNow)
				return;

			glowOnInt = shouldBeLitNow;

			if(shouldBeLitNow)
				map.glowGrid.RegisterGlower(this);
			else
				map.glowGrid.DeRegisterGlower(this);
			map.mapDrawer.MapMeshDirty(parent.Position, MapMeshFlag.Things);
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			if(ShouldBeLitNow)
			{
				UpdateLit(this.parent.Map);
				parent.Map.glowGrid.RegisterGlower(this);
			}
			else
			{
				UpdateLit(this.parent.Map);
			}
		}

		public override void ReceiveCompSignal(string signal)
		{
			if(
				signal == CompPowerTrader.PowerTurnedOnSignal ||
				signal == CompPowerTrader.PowerTurnedOffSignal ||
				signal == CompFlickable.FlickedOnSignal ||
				signal == CompFlickable.FlickedOffSignal ||
				signal == CompSchedule.ScheduledOnSignal ||
				signal == CompSchedule.ScheduledOffSignal)
			{
				UpdateLit(parent.Map);
			}
		}

		public override void PostExposeData()
		{
			Scribe_Values.Look<bool>(ref glowOnInt, "glowOn", false, false);
		}

		public override void PostDeSpawn(Map map)
		{
			UpdateLit(map);
		}

		private bool glowOnInt;
	}


	public class CompProperties_SolarAndBattery : CompProperties
	{
		public CompProperties_SolarAndBattery()
		{
			this.compClass = typeof(CompSolarAndBattery);
		}

		public float storedEnergyMax;
		public float powerMax;
		public float powerConsumption;
		public bool roof;
	}


	public class CompSolarAndBattery : ThingComp
	{
		public CompProperties_SolarAndBattery Props
		{
			get
			{
				return (CompProperties_SolarAndBattery)props;
			}
		}

		public bool PowerOn
		{
			get
			{
				return condition == 0;
			}
		}

		private void UpdateCondition(int cond)
		{
			if((condition ^ cond) == 0)
				return;

			int old_cond = condition;
			condition = cond;

			if(old_cond == 0)
			{
				if(cond == 0)
					return;

				parent.BroadcastCompSignal(CompPowerTrader.PowerTurnedOnSignal);

				SoundDef sound = SoundDefOf.PowerOnSmall;
				sound.PlayOneShot(new TargetInfo(this.parent.Position, this.parent.Map, false));
			}
			else
			{
				if(cond != 0)
					return;

				parent.BroadcastCompSignal(CompPowerTrader.PowerTurnedOffSignal);

				if(parent.Spawned)
				{
					SoundDef sound = SoundDefOf.PowerOffSmall;
					sound.PlayOneShot(new TargetInfo(this.parent.Position, this.parent.Map, false));
				}
			}
		}

		private float roofedFactor()
		{
			int num1 = 0;
			int num2 = 0;
			foreach(IntVec3 c in parent.OccupiedRect()){
				num1++;
				if(parent.Map.roofGrid.Roofed(c))
					num2++;
			}
			if(num1 == 0)
				return 1.0f;
			return (float)(num1 - num2) / (float)num1;
		}

		protected void DoTick(int interval)
		{
			// Breakdownable not send a repair notify, so we check every frame...faq
			int cond = condition;

			if((cond & BROKENDOWN) != 0){
				if(breakdownableComp != null && breakdownableComp.BrokenDown)
					return;
				else
					cond &= ~BROKENDOWN;
			}

			if(parent.Map.gameConditionManager.ConditionIsActive(GameConditionDefOf.SolarFlare))
				cond |= SOLAR_FLARE;
			else
				cond &= ~SOLAR_FLARE;

			if((cond & SOLAR_FLARE) == 0){
				float energy = Mathf.Lerp(0f, Props.powerMax, parent.Map.skyManager.CurSkyGlow);

				if(Props.roof)
					energy *= roofedFactor();
							
				if((cond & (SWITCH_OFF | SCHEDULE_OFF)) == 0)
					energy -= Props.powerConsumption;

				float stored_energy = storedEnergy + energy * CompPower.WattsToWattDaysPerTick * interval;
				if(stored_energy > Props.storedEnergyMax){
					stored_energy = Props.storedEnergyMax;
				}

				if(stored_energy <= 0){
					stored_energy = 0;
					cond |= NO_ENERGY;
				}else{
					cond &= ~NO_ENERGY;
				}

				storedEnergy = stored_energy;
			}

			UpdateCondition(cond);
		}

		/*public virtual void Initialize(CompProperties props)
		{
			this.props = props;
		}*/

		public override void ReceiveCompSignal(string signal)
		{
			switch(signal){
			case CompFlickable.FlickedOffSignal:
				UpdateCondition(condition | SWITCH_OFF);
				break;

			case CompFlickable.FlickedOnSignal:
				UpdateCondition(condition & ~SWITCH_OFF);
				break;

			case CompSchedule.ScheduledOffSignal:
				UpdateCondition(condition | SCHEDULE_OFF);
				break;

			case CompSchedule.ScheduledOnSignal:
				UpdateCondition(condition & ~SCHEDULE_OFF);
				break;

			case CompBreakdownable.BreakdownSignal:
				UpdateCondition(condition | BROKENDOWN);
				break;
			}
		}

		public override void PostExposeData()
		{
			Scribe_Values.Look<int>(ref condition, "condition", 0, false);
			Scribe_Values.Look<float>(ref storedEnergy, "storedPower", 0f, false);
			if(storedEnergy > Props.storedEnergyMax)
				storedEnergy = Props.storedEnergyMax;
			if(storedEnergy < 0)
				storedEnergy = 0;
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			breakdownableComp = parent.GetComp<CompBreakdownable>();
		}

		/*public override void PostDeSpawn(Map map)
		{
		}*/

		/*public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
		}*/

		public override void CompTick()
		{
			DoTick(1);
		}

		public override void CompTickRare()
		{
			DoTick(GenTicks.TickRareInterval);
		}

		/*public override void PostPreApplyDamage(DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
		}*/

		/*public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
		}*/

		public override void PostDraw()
		{
			if((condition & BROKENDOWN) != 0)
				return;

			if((condition & SWITCH_OFF) != 0){
				parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.PowerOff);
				return;
			}

			if((condition & (NO_ENERGY | SOLAR_FLARE)) != 0){
				parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.NeedsPower);
				return;
			}
		}

		/*public override void PostDrawExtraSelectionOverlays()
		{
		}*/

		/*public override void PostPrintOnto(SectionLayer layer)
		{
		}*/

		/*public override void CompPrintForPowerGrid(SectionLayer layer)
		{
		}*/

		/*public override void PreAbsorbStack(Thing otherStack, int count)
		{
		}*/

		/*public override void PostSplitOff(Thing piece)
		{
		}*/

		/*public override string TransformLabel(string label)
		{
			return label;
		}*/

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if(Prefs.DevMode){
				yield return new Command_Action
				{
					defaultLabel = "DEBUG: Fill",
					action = delegate()
					{
						storedEnergy = Props.storedEnergyMax;
					}
				};
				yield return new Command_Action
				{
					defaultLabel = "DEBUG: Empty",
					action = delegate()
					{
						storedEnergy = 0;
					}
				};
			}
			yield break;
		}

		/*public override bool AllowStackWith(Thing other)
		{
			return true;
		}*/

		public override string CompInspectStringExtra()
		{
			CompProperties_SolarAndBattery props = this.Props;
			float consumption;

			if((condition & ~(NO_ENERGY)) == 0 && parent.Spawned)
				consumption = props.powerConsumption;
			else
				consumption = 0;

			string str = string.Format("{0}: {1} W\n{2}: {3:f0} / {4:f0} Wd",
				"PowerNeeded".Translate(),
				consumption,
				"PowerBatteryStored".Translate(),
				storedEnergy,
				props.storedEnergyMax
				);

			return str;
		}

		/*public override string GetDescriptionPart()
		{
			return null;
		}*/

		/*public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			yield break;
		}*/

		/*public override void PrePreTraded(TradeAction action, Pawn playerNegotiator, ITrader trader)
		{
		}*/

		/*public override void PostIngested(Pawn ingester)
		{
		}*/

		/*public override void PostPostGeneratedForTrader(TraderKindDef trader, int forTile, Faction forFaction)
		{
		}*/

		/*public override void Notify_SignalReceived(Signal signal)
		{
		}*/

		
		private CompBreakdownable breakdownableComp;

		private float storedEnergy;
		private int condition;

		private const int NO_ENERGY    =  1;
		private const int BROKENDOWN   =  2;
		private const int SWITCH_OFF   =  4;
		private const int SCHEDULE_OFF =  8;
		private const int SOLAR_FLARE  = 16;
	}

	public class CompProperties_Ceiling : CompProperties
	{
		public CompProperties_Ceiling()
		{
			this.compClass = typeof(CompCeiling);
		}
	}

	public class CompCeiling : ThingComp
	{
		/*public virtual void Initialize(CompProperties props)
		{
			this.props = props;
		}*/

		/*public override void ReceiveCompSignal(string signal)
		{
		}*/

		/*public override void PostExposeData()
		{
		}*/

		/*public override void PostSpawnSetup(bool respawningAfterLoad)
		{
		}*/

		/*public override void PostDeSpawn(Map map)
		{
		}*/

		/*public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
		}*/

		/*public override void CompTick()
		{
		}*/

		public override void CompTickRare()
		{
			IntVec3 pos = parent.Position;
			if(!parent.Map.roofGrid.Roofed(pos)){
				if(!parent.Destroyed && parent.def.destroyable){
					parent.Destroy(DestroyMode.Deconstruct);
				}
			}
		}

		/*public override void PostPreApplyDamage(DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
		}*/

		/*public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
		}*/

		/*public override void PostDraw()
		{
		}*/

		/*public override void PostDrawExtraSelectionOverlays()
		{
		}*/

		/*public override void PostPrintOnto(SectionLayer layer)
		{
		}*/

		/*public override void CompPrintForPowerGrid(SectionLayer layer)
		{
		}*/

		/*public override void PreAbsorbStack(Thing otherStack, int count)
		{
		}*/

		/*public override void PostSplitOff(Thing piece)
		{
		}*/

		/*public override string TransformLabel(string label)
		{
			return label;
		}*/

		/*public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield break;
		}*/

		/*public override bool AllowStackWith(Thing other)
		{
			return true;
		}*/

		/*public override string CompInspectStringExtra()
		{
			return null;
		}*/

		/*public override string GetDescriptionPart()
		{
			return null;
		}*/

		/*public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			yield break;
		}*/

		/*public override void PrePreTraded(TradeAction action, Pawn playerNegotiator, ITrader trader)
		{
		}*/

		/*public override void PostIngested(Pawn ingester)
		{
		}*/

		/*public override void PostPostGeneratedForTrader(TraderKindDef trader, int forTile, Faction forFaction)
		{
		}*/

		/*public override void Notify_SignalReceived(Signal signal)
		{
		}*/
	}
}
