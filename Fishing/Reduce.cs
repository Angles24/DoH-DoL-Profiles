using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using ff14bot;
using ff14bot.AClasses;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.RemoteAgents;
using ff14bot.RemoteWindows;
using LlamaLibrary.Extensions;
using LlamaLibrary.Memory;
using TreeSharp;

namespace LlamaLibrary.Reduce
{
    public class Reduce : BotBase
    {
        private static readonly string botName = "Aetherial Reduction";

        private static bool done;

        private static readonly Version v = new Version(1, 0, 3);

        private static readonly InventoryBagId[] inventoryBagIds = new InventoryBagId[4]
        {
            InventoryBagId.Bag1,
            InventoryBagId.Bag2,
            InventoryBagId.Bag3,
            InventoryBagId.Bag4
        };

        private static readonly InventoryBagId[] armoryBagIds = new InventoryBagId[12]
        {
            InventoryBagId.Armory_MainHand,
            InventoryBagId.Armory_OffHand,
            InventoryBagId.Armory_Helmet,
            InventoryBagId.Armory_Chest,
            InventoryBagId.Armory_Glove,
            InventoryBagId.Armory_Belt,
            InventoryBagId.Armory_Pants,
            InventoryBagId.Armory_Boots,
            InventoryBagId.Armory_Earrings,
            InventoryBagId.Armory_Necklace,
            InventoryBagId.Armory_Writs,
            InventoryBagId.Armory_Rings
        };

        private static readonly List<string> desynthList = new List<string>
        {
            "Warg",
            "Amaurotine",
            "Lakeland",
            "Voeburtite",
            "Fae",
            "Ravel",
            "Nabaath",
            "Anamnesis"
        };

        //private IntPtr offset => offsetInt;

        private Composite _root;

        private Settings _settings;

        public override string Name
        {
            get
            {
#if RB_CN
                        return "精炼&分解";
#else
                return "Reduce/Desynth";
#endif
            }
        }

#if RB_CN

                private IntPtr offset = Core.Memory.GetAbsolute(new IntPtr(0xA6E170)) ; //0xA90fd0;
#else

        //static GreyMagic.PatternFinder  patternFinder = new GreyMagic.PatternFinder(Core.Memory);
        //private readonly IntPtr offset = patternFinder.Find("48 85 D2 0F 84 ? ? ? ? 55 56 57 48 81 EC ? ? ? ? 48 8B 05 ? ? ? ? 48 33 C4 48 89 84 24 ? ? ? ? 80 7A ? ? 41 8B E8 48 8B FA 48 8B F1 74 ? 48 8B CA E8 ? ? ? ? 48 8B C8 E8 ? ? ? ? EB ? 0F B6 42 ? A8 ? 74 ? 8B 42 ? 05 ? ? ? ? EB ? A8 ? 8B 42 ? 74 ? 05 ? ? ? ? 85 C0 0F 84 ? ? ? ? 48 89 9C 24 ? ? ? ? 48 8B CE 4C 89 B4 24 ? ? ? ? E8 ? ? ? ? 8B 9E ? ? ? ?");
        //private IntPtr offsetInt = Core.Memory.GetAbsolute(new IntPtr(0xA6E170)); //0xA90fd0;
#endif
        //private const int offsetInt = 0xa910c0;

        public override bool WantButton => true;

        public override string EnglishName => "Reduce/Desynth";

        public override PulseFlags PulseFlags => PulseFlags.All;

        public override bool RequiresProfile => false;

        public override Composite Root => _root;

        //Change here if you want to look for items that start with another string
        private string NameStarts => "Warg";

        private string NameStarts1 => "Amaurotine";

        private static bool ShouldDesynth(string name)
        {
            return desynthList.Any(name.Contains);
        }

        public override void OnButtonPress()
        {
            if (_settings == null)
            {
                _settings = new Settings
                {
                    Text = "Reduce and Desynth v" + v //title
                };

                _settings.Closed += (o, e) => { _settings = null; };
            }

            try
            {
                _settings.Show();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private static void Log(string text, params object[] args)
        {
            var msg = string.Format("[" + botName + "] " + text, args);
            Logging.Write(Colors.Green, msg);
        }

        private static void LogVerbose(string text, params object[] args)
        {
            var msg = string.Format("[" + botName + "] " + text, args);
            Logging.WriteVerbose(msg);
        }
        
        public override void Initialize()
        {
            OffsetManager.Init();
        }
        
        private async Task<bool> CofferTask()
        {
            foreach (var bagslot in InventoryManager.FilledSlots.Where(bagslot => bagslot.Item.ItemAction == 388))
            {
                Log(string.Format("Opening Coffer {0}", bagslot.Name));
                bagslot.UseItem();
                await Coroutine.Sleep(5000);
            }

            return true;
        }

        public override void Start()
        {
            Log("Settings:");
            Log($"Armory: {ReduceSettings.Instance.IncludeArmory}");
            Log($"Include DE index < 10000: {ReduceSettings.Instance.IncludeDE10000}");
            Log($"Stay running: {ReduceSettings.Instance.StayRunning}");
            Log($"Zone: {ReduceSettings.Instance.AEZoneCheck} {ReduceSettings.Instance.AEZone}");
            Log($"Offset Desynth: {Offsets.SalvageAgent.ToInt64():x}");
            _root = new ActionRunCoroutine(r => Run());
            done = false;
        }

        public override void Stop()
        {
            ReduceSettings.Instance.Save();
        }

        private async Task<bool> Run()
        {
            await Reduction();
            await Desynth();
            if (ReduceSettings.Instance.OpenCoffers) await CofferTask();
            //ReduceSettings.Instance.Save();
            if (!ReduceSettings.Instance.StayRunning)
                TreeRoot.Stop("Stop Requested");
            return true;
        }

        private async Task<bool> Reduction()
        {
            //Reduce
            if (ReduceSettings.Instance.AEZoneCheck && ReduceSettings.Instance.AEZone != 0)
            {
                if (WorldManager.ZoneId != (ushort) ReduceSettings.Instance.AEZone) return false;
                await Coroutine.Sleep(5000);
            }

            if (MovementManager.IsOccupied) return false;

            if (Core.Me.IsMounted)
            {
                ActionManager.Dismount();
            }
            await Coroutine.Wait(5000, () => !MovementManager.IsOccupied && !Core.Me.IsMounted);

            Console.WriteLine("{" + string.Join($"", Core.Me.Name) + "}");

            while (InventoryManager.FilledSlots.Any(x => inventoryBagIds.Contains(x.BagId) && x.CanReduce))//&& WorldManager.ZoneId == (ushort) ReduceSettings.Instance.AEZone )
            {
                var item = InventoryManager.FilledSlots.FirstOrDefault(x => inventoryBagIds.Contains(x.BagId) && x.CanReduce);

                if (item == null) break;
                Log($"Reducing - Name: {item.Item.CurrentLocaleName}");
                await CommonTasks.AetherialReduction(item);
            }

            return true;
        }

        public static async Task<bool> Desynth()
        {
            //Desynthesis
            var agentSalvageInterface = AgentInterface<AgentSalvage>.Instance;
            var agentSalvage = Offsets.SalvageAgent;

            //if (MovementManager.IsOccupied) return false;
  //          if (!InventoryManager.GetBagsByInventoryBagId(BagsToCheck()).Any(bag => bag.FilledSlots.Any(bs => bs.IsDesynthesizable)))
            if (!InventoryManager.GetBagsByInventoryBagId(BagsToCheck()).Any(bag => bag.FilledSlots.Any(bs => bs.IsDesynthesizable && (ShouldDesynth(bs.Item.EnglishName)  || ExtraCheck(bs)))))
            {
                Log($"None found");
                return false;
            }

/*            var itemsToDesynth = InventoryManager.GetBagsByInventoryBagId(BagsToCheck())
                .SelectMany(bag => bag.FilledSlots
                    .FindAll(bs => bs.IsDesynthesizable && (ShouldDesynth(bs.Item.EnglishName) || ExtraCheck(bs))));*/

            var itemsToDesynth = InventoryManager.GetBagsByInventoryBagId(BagsToCheck())
                .SelectMany(bag => bag.FilledSlots
                    .FindAll(bs => bs.IsDesynthesizable && (ShouldDesynth(bs.Item.EnglishName)  || ExtraCheck(bs))));

            Log($"{itemsToDesynth.Count()}");
            foreach (var item in itemsToDesynth)
            {
				while (item.IsFilled && item.CanDesynthesize)
				{
				   // Log($"Desynthesize Item - Name: {item.Item.CurrentLocaleName}");

					Log($"Desynthesize Item - Name: {item.Item.CurrentLocaleName}");

					lock (Core.Memory.Executor.AssemblyLock)
					{
						Core.Memory.CallInjected64<int>(agentSalvage, agentSalvageInterface.Pointer, item.Pointer, 14);
					}

				   // await Coroutine.Sleep(500);


					await Coroutine.Wait(5000, () => SalvageDialog.IsOpen);

					if (SalvageDialog.IsOpen)
					{
						RaptureAtkUnitManager.GetWindowByName("SalvageDialog").SendAction(1, 3, 0);
						await Coroutine.Sleep(1000);
						await Coroutine.Wait(10000, () => SalvageResult.IsOpen);

						if (SalvageResult.IsOpen)
						{
							SalvageResult.Close();
							await Coroutine.Sleep(1000);
							await Coroutine.Wait(5000, () => !SalvageResult.IsOpen);
						}
						else
						{
							Log("Result didn't open");
							break;
						}
					}
					else
					{
						Log("SalvageDialog didn't open");
						break;
					}
				}
            }
            
            

            return true;
        }

        private static bool ExtraCheck(BagSlot bs)
        {
            //return ReduceSettings.Instance.IncludeDE10000 && (bs.Item.RequiredLevel < 70 && bs.Item.DesynthesisIndex < 10000);
			if (bs.Item.EquipmentCatagory == ItemUiCategory.Seafood) return true;
            return false;
        }

        private static InventoryBagId[] BagsToCheck()
        {
            return ReduceSettings.Instance.IncludeArmory ? inventoryBagIds.Concat(armoryBagIds).ToArray() : inventoryBagIds;
            //return inventoryBagIds;
        }
    }
}