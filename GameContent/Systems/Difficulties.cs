using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;
using tainicom.Aether.Physics2D.Fluids;
using TanksRebirth.GameContent.Globals;
using TanksRebirth.GameContent.ID;
using TanksRebirth.GameContent.RebirthUtils;
using TanksRebirth.GameContent.Systems.Coordinates;
using TanksRebirth.GameContent.UI;
using TanksRebirth.Internals.Common.Utilities;
using TanksRebirth.Net;

namespace TanksRebirth.GameContent.Systems;

public record Difficulties {
    public static int MonochromeValue { get; set; }
    public static int RandomTanksUpper { get; set; }
    public static int RandomTanksLower { get; set; }
    public static int DisguiseValue { get; set; }

    public static readonly Dictionary<string, bool> Types = new() {
        ["TanksAreCalculators"] = false,
        ["PieFactory"] = false,
        ["UltraMines"] = false,
        ["BulletHell"] = false,
        ["AllInvisible"] = false,
        ["AllStationary"] = false,
        ["AllHoming"] = false,
        ["Armored"] = false,
        ["BumpUp"] = false,
        ["Monochrome"] = false,
        ["InfiniteLives"] = false,
        ["MasterModBuff"] = false,
        ["TacticalPlanes"] = false,
        ["MachineGuns"] = false,
        ["RandomizedTanks"] = false,
        ["ThunderMode"] = false,
        ["POV"] = false,
        ["AiCompanion"] = false,
        ["Shotguns"] = false,
        ["Predictions"] = false,
        ["RandomPlayer"] = false,
        ["BulletBlocking"] = false,
        ["FFA"] = false,
        ["LanternMode"] = false,
        ["Disguise"] = false
    };
    public static readonly Dictionary<int, int> VanillaToMasterModeConversions = new() {
        [TankID.Brown] = TankID.Bronze,
        [TankID.Ash] = TankID.Silver,
        [TankID.Marine] = TankID.Sapphire,
        [TankID.Yellow] = TankID.Citrine,
        [TankID.Pink] = TankID.Ruby,
        [TankID.Green] = TankID.Emerald,
        [TankID.Violet] = TankID.Amethyst,
        [TankID.White] = TankID.Gold,
        [TankID.Black] = TankID.Obsidian
    };
    public static TankTemplate[] HijackTanks(TankTemplate[] tanks) {
        for (int i = 0; i < tanks.Length; i++) {
            var t = tanks[i];
            if (t.IsPlayer)
                continue;

            var newTemplate = t;

            newTemplate.AiTier = Server.ServerRandom.Next(RandomTanksLower, RandomTanksUpper + 1);
            tanks[i] = newTemplate;
        }
        return tanks;
    }
    public static Mission Flip(Mission mission, bool x = false, bool y = false) {
        if (!(x && y))
            return mission;

        var newMission = mission;

        var tanks = newMission.Tanks;
        var blocks = newMission.Blocks;

        var tanksWithPlacements = new Dictionary<TankTemplate, PlacementSquare>();
        var blocksWithPlacements = new Dictionary<BlockTemplate, PlacementSquare>();

        PlacementSquare.InitializeLevelEditorSquares();

        for (int i = 0; i < tanks.Length; i++) {
            // bro :sob:
            tanksWithPlacements[tanks[i]] = PlacementSquare.Placements.First(x => Vector2.Distance(x.Position.FlattenZ(), tanks[i].Position) < 5);
        }
        for (int i = 0; i < blocks.Length; i++) {
            blocksWithPlacements[blocks[i]] = PlacementSquare.Placements.First(x => Vector2.Distance(x.Position.FlattenZ(), blocks[i].Position) < 5);
        }

        // TODO: this
        // MaxX - PosX = FlipX
        // MaxY - PosY = FlipY
        if (x) {
            for (int i = 0; i < tanks.Length; i++) {

            }
            for (int i = 0; i < blocks.Length; i++) {

            }
        }
        if (y) {
            for (int i = 0; i < tanks.Length; i++) {

            }
            for (int i = 0; i < blocks.Length; i++) {

            }
        }
        return newMission;
    }

    public static void GlobalManage() {
        ManageAirplanes();
    }

    public static void ManageAirplanes() {
        if (!Client.IsHost()) return;

        if (((DebugManager.DebuggingEnabled && DebugManager.DebugLevel == DebugManager.Id.AirplaneTest) || Types["TacticalPlanes"]) && CampaignGlobals.InMission) {
            if (RuntimeData.RunTime % 300 <= RuntimeData.DeltaTime) {
                // 33% chance every 5 seconds
                if (Client.ClientRandom.Next(3) == 0) {
                    Airplane.SpawnPlaneWithSmokeGrenades();
                }
            }
        }
    }
}
