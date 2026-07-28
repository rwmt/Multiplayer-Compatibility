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
		public OnlyIfToleranceBelow(ModContentPack mod)
		{
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

	}

}
