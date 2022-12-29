using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using System.Linq;
using System.Xml.Serialization;
using Sandbox.Game;

using VRageMath;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.Game.Entity;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.Entity.EntityComponents.Interfaces;
using SupplyDrops.API;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Entities;

namespace SupplyDrops
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class SupplyDrops : MySessionComponentBase
    {
        public static bool isClient => !(isServer && isDedicated);
        public static bool isDedicated => MyAPIGateway.Utilities.IsDedicated;
        public static bool isServer => MyAPIGateway.Multiplayer.IsServer;
        public static bool isActive => MyAPIGateway.Multiplayer.MultiplayerActive;
        public static bool isPlayer => MyAPIGateway.Session.Player != null;
        public static bool isAdmin => (MyAPIGateway.Session.Player.PromoteLevel != MyPromoteLevel.None && MyAPIGateway.Session.Player.PromoteLevel != MyPromoteLevel.Scripter);
        public static MESApi MESApi;

        

        public static Dictionary<string, MyDefinitionId> ConsumableItems;

        bool FinishedSetup = false;

        public static Guid modID = new Guid("6417360e-6f78-4f53-b5c2-a1d5ab9a7b2b");
        public long ticks = 0;
        public MyObjectBuilder_ConsumableItem OreDropLowDefinition = new MyObjectBuilder_ConsumableItem() { SubtypeName = "OreDropLow" };
        public MyObjectBuilder_ConsumableItem StolenJewels = new MyObjectBuilder_ConsumableItem() { SubtypeName = "StolenJewels" };
        public MyObjectBuilder_ConsumableItem RescueRover = new MyObjectBuilder_ConsumableItem() { SubtypeName = "RescueRover" };
        public MyObjectBuilder_ConsumableItem WeldingDrone = new MyObjectBuilder_ConsumableItem() { SubtypeName = "WeldingDrone" };
        public MyObjectBuilder_ConsumableItem PintleTurret = new MyObjectBuilder_ConsumableItem() { SubtypeName = "PintleGatling" };
        public List<MyPlanet> planets = new List<MyPlanet>();
        

        public int OreDropLowChance = 10;
        public int JewelChance = 2;
        public int DataPadChance = 5;
        public int RescueRoverChance = 2;
        public int WeldingDroneChance = 2;
        public int PintleTurretChance = 2;

        
        public override void LoadData()
        {


            //if (isServer)
            //{
                MESApi = new MESApi();
                MyAPIGateway.Players.ItemConsumed += Players_ItemConsumed;
                ConsumableItems = new Dictionary<string, MyDefinitionId>();
                ConsumableItems.Add("OreDropLow", MyDefinitionManager.Static.GetPhysicalItemDefinition(MyDefinitionId.Parse("MyObjectBuilder_ConsumableItemDefinition/OreDropLow")).Id);
                ConsumableItems.Add("RescueRover", MyDefinitionManager.Static.GetPhysicalItemDefinition(MyDefinitionId.Parse("MyObjectBuilder_ConsumableItemDefinition/RescueRover")).Id);
                ConsumableItems.Add("WeldingDrone", MyDefinitionManager.Static.GetPhysicalItemDefinition(MyDefinitionId.Parse("MyObjectBuilder_ConsumableItemDefinition/WeldingDrone")).Id);
                ConsumableItems.Add("PintleTurret", MyDefinitionManager.Static.GetPhysicalItemDefinition(MyDefinitionId.Parse("MyObjectBuilder_ConsumableItemDefinition/PintleGatling")).Id);
                MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
            //}
        }

        private void Entities_OnEntityAdd(IMyEntity obj)
        {
            try
            {
                IMyCubeGrid grid = obj as IMyCubeGrid;
                
                if (grid == null)
                    return;

                IMyGridTerminalSystem gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);

                var owners = grid.BigOwners;
                if (owners == null || owners.Count == 0)
                    return;

                //Only NPCs can spawn with this.
                if (!IsOwnerNPC(owners))
                    return;
                if (grid.CustomName != null)
                {
                    if (grid.CustomName == RegShip.DropPod || grid.CustomName == RegShip.Rover)
                    {
                        grid.ChangeGridOwnership(0, MyOwnershipShareModeEnum.All);
                        grid.UpdateOwnership(0, true);

                    }else if (grid.CustomName == RegShip.WeldDrone)
                    {
                        var gridMatrix = grid.LocalMatrix;
                        Matrix.Rescale(ref gridMatrix, 0.2f);
                        grid.SetLocalMatrix(gridMatrix);
                    }
                }

                
                
                //var ownerFactionTag = MyVisualScriptLogicProvider.GetPlayersFactionTag(owner);
                var factions = MyAPIGateway.Session.Factions.Factions;
                Random random = new Random();

                var padFaction = factions.ElementAt(random.Next(0, factions.Count - 1)).Value;

                List<IMyFaction> enemyFactions = new List<IMyFaction>();
                List<IMyFaction> neutralOrFriendly = new List<IMyFaction>();

                if (padFaction != null)
                {
                    foreach (var kvp in factions)
                    {
                        if (kvp.Value.Tag != padFaction.Tag)
                        {
                            if (kvp.Value.IsEveryoneNpc())
                            {
                                if (MyAPIGateway.Session.Factions.AreFactionsEnemies(kvp.Key, padFaction.FactionId) || kvp.Value.Tag == "SPRT")
                                {
                                    enemyFactions.Add(kvp.Value);
                                }
                                else
                                {
                                    neutralOrFriendly.Add(kvp.Value);
                                }
                            }
                        }
                    }
                }
                //MyLog.Default.WriteLineAndConsole($"Here 2");
                List <IMyCargoContainer> containers = new List<IMyCargoContainer>();
                gts.GetBlocksOfType<IMyCargoContainer>(containers);
                
                if (containers == null || containers.Count == 0)
                    return;

                Random r = new Random();

                foreach (var container in containers)
                {
                    if (!container.IsFunctional)
                        continue;

                    // OreDrop Chance
                    var chance = r.Next(0, 100);
                    if (chance <= OreDropLowChance)
                    {
                        var inventory = container.GetInventory();
                        inventory.AddItems(1, OreDropLowDefinition);
                    }

                    chance = r.Next(0, 100);
                    if (chance <= JewelChance)
                    {
                        var inventory = container.GetInventory();
                        inventory.AddItems(1, StolenJewels);
                    }

                    chance = r.Next(0, 100);
                    if (chance <= RescueRoverChance)
                    {
                        var inventory = container.GetInventory();
                        inventory.AddItems(1, RescueRover);
                    }

                    chance = r.Next(0, 100);
                    if (chance <= WeldingDroneChance)
                    {
                        var inventory = container.GetInventory();
                        inventory.AddItems(1, WeldingDrone);
                    }

                    chance = r.Next(0, 100);
                    if (chance <= PintleTurretChance)
                    {
                        var inventory = container.GetInventory();
                        inventory.AddItems(1, PintleTurret);
                    }

                    if (padFaction != null)
                    {
                        chance = r.Next(0, 100);
                        if (chance <= DataPadChance)
                        {
                            var id = new MyDefinitionId(typeof(MyObjectBuilder_Datapad), "Datapad");
                            var dataPad = MyObjectBuilderSerializer.CreateNewObject(id) as MyObjectBuilder_Datapad;
                            string title;
                            string text;

                            if (FormatDataPad(out title, out text, padFaction.Name, enemyFactions, neutralOrFriendly))
                            {
                                dataPad.Name = title;
                                dataPad.Data = text;
                                var inventory = container.GetInventory();
                                inventory.AddItems(1, dataPad);
                            }
                        }
                    }
                }

                

            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"Error:{e.Message}\n{e.StackTrace}\n\n----\n{e.InnerException}");
            }
        }



        public bool IsOwnerNPC(List<long> Owners)
        {
            //we only really care about the first one.
            var firstOwner = Owners.First();

            var ownerFactionTag = MyVisualScriptLogicProvider.GetPlayersFactionTag(firstOwner);
            var faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(ownerFactionTag);
            if (faction == null)
                return false;

            
            return faction.IsEveryoneNpc();
        }


        protected override void UnloadData()
        {
            MyAPIGateway.Players.ItemConsumed -= Players_ItemConsumed;
            MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
        }



        private void Players_ItemConsumed(IMyCharacter character, MyDefinitionId consumed)
        {

            if (character == null || !character.IsPlayer) //how?!?
                return;

            IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(character as IMyEntity);
            if (!MESApi.MESApiReady)
            {
                MyLog.Default.WriteLineAndConsole("MES not ready");
                return;
            }

            Vector3D setPosition = Vector3D.Zero;
            BoundingSphereD spawnCheck = new BoundingSphereD();
            List<IMyEntity> ent = new List<IMyEntity>();
            bool cubeBlockIntersect = false;
            var gravityVector = character.Physics.Gravity;
  
            switch (consumed.SubtypeName)
            {
                case "OreDropLow":
                    if (!MESApi.SpawnPlanetaryCargoShip(player.GetPosition(), new List<string> { "SupplyDropOre" }))
                    {
                        MyVisualScriptLogicProvider.AddToPlayersInventory(character.EntityId, ConsumableItems["OreDropLow"]); // if it couldn't spawn the ship, refund the item
                    }
                    break;
                case "RescueRover":
                    if (!MESApi.SpawnPlanetaryCargoShip(player.GetPosition(), new List<string> { "RescueRoverDrop" }))
                    {
                        MyVisualScriptLogicProvider.AddToPlayersInventory(player.IdentityId, ConsumableItems["RescueRover"]); // if it couldn't spawn the ship, refund the item
                    }
                    break;
                case "WeldingDrone":
                    
                    if (character.Physics.Gravity != Vector3D.Zero)
                    {
                        
                        setPosition = (-gravityVector) + character.WorldMatrix.Translation;

                        spawnCheck = new BoundingSphereD(setPosition, 5);

                        //MyVisualScriptLogicProvider.AddGPS("Welding Buddy", "Welding Buddy Deployment Area", setPosition, Color.LightBlue, 10, player.IdentityId);
                        ent = MyAPIGateway.Entities.GetEntitiesInSphere(ref spawnCheck);
                        cubeBlockIntersect = false;
                        foreach (var entity in ent)
                        {
                            if (entity is IMyCubeBlock)
                            {
                                cubeBlockIntersect = true;
                                break;
                            }
                        }
                        if (!cubeBlockIntersect)
                        {
                            MyVisualScriptLogicProvider.SpawnPrefabInGravity("Welding Drone", setPosition, character.LocalMatrix.Forward, player.IdentityId);
                        }
                        else
                        {
                            MyVisualScriptLogicProvider.AddToPlayersInventory(player.IdentityId, ConsumableItems["WeldingDrone"]); // if it couldn't spawn the ship, refund the item
                        }
                    }
                    break;
                case "PintleGatling":

                    setPosition = (-gravityVector/10) + character.WorldMatrix.Translation;

                    spawnCheck = new BoundingSphereD(setPosition, 5);
                    //MyVisualScriptLogicProvider.AddGPS("Turret Deployment", "Turret Deployment Area", setPosition, Color.LightBlue, 10, player.IdentityId);
                    ent = MyAPIGateway.Entities.GetEntitiesInSphere(ref spawnCheck);
                    cubeBlockIntersect = false;
                    foreach (var entity in ent)
                    {
                        if (entity is IMyCubeBlock)
                        {
                            cubeBlockIntersect = true;
                            break;
                        }
                    }
                    if (!cubeBlockIntersect)
                    {
                        MyVisualScriptLogicProvider.SpawnPrefabInGravity("Pintle Mounted Gatling Gun", setPosition, character.LocalMatrix.Forward, player.IdentityId);
                    }
                    else
                    {
                        MyVisualScriptLogicProvider.AddToPlayersInventory(player.IdentityId, ConsumableItems["PintleTurret"]); // if it couldn't spawn the ship, refund the item
                    }

                    break;
                case "StolenJewels":
                    long balanceChange = new Random().Next(50000, 1000000);
                    player.RequestChangeBalance(balanceChange);
                    MyVisualScriptLogicProvider.SendChatMessageColored($"Thank you, {player.DisplayName}. A reward of {balanceChange} has been deposited into your account.", Color.Blue, "System Authority", player.IdentityId);
                    break;
            }
        }

        public bool FormatDataPad(out string Title, out string Text, string thisFaction, List<IMyFaction> enemyFactions, List<IMyFaction> neutralFactions)
        {
            Title = string.Empty;
            Text = string.Empty;

            var ThisFaction = thisFaction;
            var EnemyFaction = string.Empty;
            var NeutralFriendly = string.Empty;
            var Planet = string.Empty;

            
            var R = new Random();
            
            if (enemyFactions.Count >= 1)
            {
                EnemyFaction = enemyFactions[R.Next(0, enemyFactions.Count - 1)].Name;
            }else
            {
                EnemyFaction = "The Unknown Forces";
            }


            if (neutralFactions.Count >= 1)
            {
                NeutralFriendly = neutralFactions[R.Next(0, neutralFactions.Count - 1)].Name;
            }else
            {
                NeutralFriendly = "our ally";
            }
          

            if (planets.Count >= 1)
            {
                Planet = planets[R.Next(0, planets.Count - 1)].Name;
            }
            else
            {
               Planet = "our offworld site";
            }

            //this is just the first test
            Title = $"INTEROFFICE MEMO";
            Text = $"INTEROFFICE MEMO\n=================================\n" +
            $"DATE: Today\n" +
            $"TO: {ThisFaction} Security\n" +
            $"CC: {ThisFaction} Executive Team\n" +
            $"FROM: {ThisFaction} Intelligence Director\n" +
            "RE: Enemy Faction Movement\n" +
            "PRIORITY: HIGH\n" +
            "=================================\n" +
            "\n" +
            $"We have received intelligence that {EnemyFaction} is on the move at {Planet}.\n" +
            "Their forces seem to be growing in boldness. Please ask Alliance & Program\n" +
            $"Management to see whether can get some support from {NeutralFriendly}'s\n" +
            $"security forces. We should also decide on a rally point at {Planet} for\n" +
            "a first strike.\n" +
            "\n" +
            "We will update you all with more details as we get them. Thank you.\n";
            //MyLog.Default.WriteLineAndConsole($"{Text}");
            return true;
        }


        public struct DatapadText
        {
            public string Title;
            public string Text;
        }

        public static class RegShip
        {
            public const string DropPod = "Emergency Supply Drop";
            public const string Rover = "Rescue Rover";
            public const string WeldDrone = "Welding Drone";
        }

    }
}










