using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
	/// <summary>OnlyIfToleranceBelow by csh1668</summary>
	/// <see href="https://github.com/csh1668/OnlyIfToleranceBelow"/>
	/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3088210509"/>
	[MpCompatFor("seohyeon.onlyIfToleranceBelow")]
	internal class OnlyIfToleranceBelow
	{
		private static ISyncField toleranceSyncField;
		private static Func<DrugPolicyEntry, float> getTolerance;
		private static Action<DrugPolicyEntry, float> setTolerance;
		private sealed class PolicySnapshot
		{
			public DrugPolicy policy;
			public float[] values;
		}
		public OnlyIfToleranceBelow(ModContentPack mod)
		{
			// 1.7 stores values in a ConditionalWeakTable instead of an injected field.
			var getter = AccessTools.DeclaredMethod("DrugPolicyEnhanced.ExposedPrepatcherFields:Get_OnlyIfToleranceBelow");
			var setter = AccessTools.DeclaredMethod("DrugPolicyEnhanced.ExposedPrepatcherFields:Set_OnlyIfToleranceBelow");
			if (getter != null && setter != null && !typeof(DrugPolicyEntry)
				.GetFields(AccessTools.all).Any(f => f.Name.Contains("OnlyIfToleranceBelow")))
			{
				getTolerance = (Func<DrugPolicyEntry, float>)Delegate.CreateDelegate(typeof(Func<DrugPolicyEntry, float>), getter);
				setTolerance = (Action<DrugPolicyEntry, float>)Delegate.CreateDelegate(typeof(Action<DrugPolicyEntry, float>), setter);
				MP.RegisterSyncMethod(typeof(OnlyIfToleranceBelow), nameof(SetTolerance)).CancelIfAnyArgNull();
				MpCompat.harmony.Patch(AccessTools.Method(typeof(Dialog_ManageDrugPolicies), nameof(Dialog_ManageDrugPolicies.DoPolicyConfigArea)),
					prefix: new HarmonyMethod(typeof(OnlyIfToleranceBelow), nameof(CapturePolicy)),
					postfix: new HarmonyMethod(typeof(OnlyIfToleranceBelow), nameof(SyncPolicyChanges)));
				return;
			}
			Type mpSyncType = AccessTools.TypeByName("Multiplayer.Client.Sync");
			MethodInfo mpSyncField = AccessTools.Method(mpSyncType, "Field",
				[typeof(Type), typeof(string), typeof(string)
			]);

			FieldInfo toleranceField = typeof(DrugPolicyEntry)
				.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.First(f => f.Name.Contains("OnlyIfToleranceBelow"
			));

			toleranceSyncField = (ISyncField)mpSyncField.Invoke(null,
			[
				typeof(DrugPolicy),
				$"{nameof(DrugPolicy.entriesInt)}/[]",
				toleranceField.Name
			]);
			toleranceSyncField.SetBufferChanges();

			MpCompat.harmony.Patch(AccessTools.Method(typeof(Dialog_ManageDrugPolicies), nameof(Dialog_ManageDrugPolicies.DoPolicyConfigArea)),
				prefix: new HarmonyMethod(typeof(OnlyIfToleranceBelow), nameof(PreDoPolicy)),
				postfix: new HarmonyMethod(typeof(OnlyIfToleranceBelow), nameof(PostDoPolicy)));
		}

		static void PreDoPolicy(Dialog_ManageDrugPolicies __instance)
		{
			if (!MP.IsInMultiplayer)
				return;

			MP.WatchBegin();
			DrugPolicy policy = __instance.SelectedPolicy;
			for (int i = 0; i < policy.Count; i++)
				toleranceSyncField.Watch(policy, i);
		}

		static void PostDoPolicy(Dialog_ManageDrugPolicies __instance)
		{
			if (MP.IsInMultiplayer)
				MP.WatchEnd();
		}

		private static void CapturePolicy(Dialog_ManageDrugPolicies __instance, out PolicySnapshot __state)
		{
			__state = null;
			if (!MP.IsInMultiplayer || !MP.InInterface) return;
			var policy = __instance.SelectedPolicy;
			if (policy == null) return;
			__state = new PolicySnapshot { policy = policy, values = Enumerable.Range(0, policy.Count)
				.Select(i => getTolerance(policy[i])).ToArray() };
		}

		private static void SyncPolicyChanges(PolicySnapshot __state)
		{
			if (__state == null) return;
			for (int i = 0; i < Math.Min(__state.values.Length, __state.policy.Count); i++)
			{
				var entry = __state.policy[i];
				float changed = getTolerance(entry);
				if (changed.Equals(__state.values[i])) continue;
				setTolerance(entry, __state.values[i]);
				SetTolerance(__state.policy, i, changed);
			}
		}

		private static void SetTolerance(DrugPolicy policy, int index, float value)
		{
			if (index >= 0 && index < policy.Count && !float.IsNaN(value) && value >= 0f && value <= 1f)
				setTolerance(policy[index], value);
		}

	}

}
