using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TanksRebirth.Enums;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using TanksRebirth.Internals.Common.Utilities;
using TanksRebirth.Internals;
using Microsoft.Xna.Framework.Audio;
using TanksRebirth.Internals.Common;
using TanksRebirth.GameContent.GameMechanics;
using TanksRebirth.Internals.Common.Framework.Audio;
using TanksRebirth.Internals.Common.Framework.Input;
using TanksRebirth.Graphics;
using TanksRebirth.Net;
using TanksRebirth.GameContent.Systems;
using FontStashSharp;
using TanksRebirth.GameContent.Globals;
using TanksRebirth.GameContent.UI;
using TanksRebirth.GameContent.ID;
using TanksRebirth.GameContent.RebirthUtils;
using TanksRebirth.GameContent.UI.MainMenu;
using TanksRebirth.Internals.Common.Framework;
using TanksRebirth.GameContent.UI.LevelEditor;
using TanksRebirth.GameContent.Globals.Assets;
using TanksRebirth.GameContent.Systems.TankSystem;
using TanksRebirth.GameContent.Systems.AI;
using TanksRebirth.Internals.Common.Framework.Collisions;

namespace TanksRebirth.GameContent;

public class PlayerTank : Tank {
    private static bool _justCenteredMouse = false;
    #region The Rest
    public static int MyTeam;
    public static int MyTankType;
    public static int StartingLives = 3;
    public static Dictionary<int, int> TankKills { get; set; } = []; // this campaign only!
    // questioning the validity of this struct but whatever
    public struct CampaignStats {
        public int ShellsShot;
        public int ShellHits;
        public int MinesLaid;
        public int MineHits;
        public int Suicides; // self-damage this campaign?
    }
    public static CampaignStats PlayerStatistics;
    public static bool _drawShotPath;
    public static int[] KillCounts { get; set; } = [0, 0, 0, 0];
    public int PlayerId { get; }
    public int PlayerType { get; }

    private Texture2D? _tankTexture;

    public static Keybind controlUp = new("Up", Keys.W);
    public static Keybind controlDown = new("Down", Keys.S);
    public static Keybind controlLeft = new("Left", Keys.A);
    public static Keybind controlRight = new("Right", Keys.D);
    public static Keybind controlMine = new("Place Mine", Keys.Space);
    public static Keybind controlFirePath = new("Draw Shot Path", Keys.Q);
    public static GamepadBind FireBullet = new("Fire Bullet", Buttons.RightTrigger);
    public static GamepadBind PlaceMine = new("Place Mine", Buttons.A);

    private bool playerControl_isBindPressed;

    public Vector2 oldPosition;

    private bool _isPlayerModel;
    private Texture2D? _shadowTexture;

    // 46 if using keyboard, 10 if using a controller
    //private float _maxTurnInputBased;
    #endregion

    public static bool LastUsedController = false;

    public Vector2 DesiredDirection;
    public static float StickDeadzone { get; set; } = 0.12f;
    public static float StickAntiDeadzone { get; set; } = 0.85f;
    /// <summary>A <see cref="PlayerTank"/> instance which represents the current client's tank they *primarily* control. Will return null in cases where
    /// the tank simply is inexistent (i.e: in the main menu). In a single-player context, this will always be the first player tank.</summary>
    public static PlayerTank ClientTank => GameHandler.AllPlayerTanks[NetPlay.GetMyClientId()];
    /// <summary>The amount of lives for every existing player. Access a certain player's life count via indexing this array with a PlayerID entry.
    /// <para>Note that lives are always synced on multiplayer.</para>
    /// </summary>
    public static int[] Lives { get; set; } = new int[Server.MaxClients];

    /// <summary>In multiplayer, gets the lives of the client that this code is currently being called on.</summary>
    public static int GetMyLives() => Lives[NetPlay.GetMyClientId()];
    /// <summary>
    /// Adds lives to the player in Single-Player, adds to the lives of all players in Multiplayer.
    /// </summary>
    /// <param name="num">How many lives to add.</param>
    public static void AddLives(int num) {
        if (Client.IsConnected())
            Lives[NetPlay.GetMyClientId()] += num;
        else
            for (int i = 0; i < Lives.Length; i++)
                Lives[i] += num;
    }
    /// <summary>
    /// Sets the lives of the player in Single-Player, sets the lives of all players in Multiplayer.
    /// </summary>
    /// <param name="num">How many lives to set the player(s) to.</param>
    public static void SetLives(int num) {
        if (Client.IsConnected())
            Lives[NetPlay.GetMyClientId()] = num;
        else
            for (int i = 0; i < Lives.Length; i++)
                Lives[i] = num;
    }
    public void SwapTankTexture(Texture2D texture) => _tankTexture = texture;
    public PlayerTank(int playerType, bool isPlayerModel = true, int copyTier = -1) {
        Model = isPlayerModel ? ModelGlobals.TankPlayer.Asset : ModelGlobals.TankEnemy.Asset;

        Texture2D texAsset;

        if (copyTier == -1)
            texAsset = Assets[$"plrtank_" + PlayerID.Collection.GetKey(playerType)!.ToLower()];
        else {
            texAsset = Assets[$"tank_" + TankID.Collection.GetKey(copyTier)!.ToLower()];

            Properties = AIManager.GetAITankProperties(copyTier);
        }

        _tankTexture = texAsset.Duplicate(TankGame.Instance.GraphicsDevice);

        _isPlayerModel = isPlayerModel;
        PlayerType = playerType;
        Team = TeamID.Red;
        IsDestroyed = true;
        PlayerId = playerType;
        _shadowTexture = GameResources.GetGameResource<Texture2D>("Assets/textures/tank_shadow");

        GameHandler.AllPlayerTanks[PlayerId] = this;

        if (copyTier == -1)
            ApplyDefaults(ref Properties);

        var newTankIndex = Array.IndexOf(GameHandler.AllTanks, null);

        WorldId = newTankIndex;

        GameHandler.AllTanks[newTankIndex] = this;

        base.Initialize();
    }
    public sealed override void ApplyDefaults(ref TankProperties properties) {
        properties.TreadVolume = 0.2f;
        properties.ShellCooldown = 5; // 5
        properties.ShootStun = 5; // 5
        properties.ShellSpeed = 3f; // 3f
        properties.MaxSpeed = 1.8f; // 1.8
        properties.RicochetCount = 1; // 1
        properties.ShellLimit = 5; // 5
        properties.MineCooldown = 6;
        properties.MineLimit = 2; // 2
        properties.MineStun = 1; // 8
        properties.Invisible = false;
        properties.Acceleration = 0.3f;
        properties.Deceleration = 0.6f;
        properties.TurningSpeed = 0.1f;

        // this changes depending on input (or should it?)
        properties.MaximalTurn = MathHelper.ToRadians(InputUtils.CurrentGamePadSnapshot.IsConnected ? 10 : 46); // normally it's 10 degrees, but we want to make it easier for keyboard players.

        Properties.ShootPitch = 0.1f * PlayerType;

        properties.ShellType = ShellID.Player;

        properties.ShellHoming = new();

        properties.DestructionColor = PlayerType switch {
            PlayerID.Blue => Color.Blue,
            PlayerID.Red => Color.Crimson,
            PlayerID.Green => Color.Lime,
            PlayerID.Yellow => Color.Yellow,
            _ => throw new Exception($"The player type with number \"{PlayerType}\" is not mapped to a color!"),
        };

        base.ApplyDefaults(ref properties);
    }
    public override void Update() {
        /*if (Input.KeyJustPressed(Keys.P))
            foreach (var m in TankDeathMark.deathMarks)
                m?.ResurrectTank();*/
        // FIXME: reference?

        // pi/2 = up
        // 0 = down
        // pi/4 = right
        // 3/4pi = left

        DesiredDirection = Vector2.Zero;
        LastUsedController = InputUtils.IsGamepadBeingUsed();

        base.Update();

        if (LevelEditorUI.IsActive || IsDestroyed) return;

        if (IsTurning) {
            if (DesiredChassisRotation - ChassisRotation >= MathHelper.PiOver2) {
                ChassisRotation += MathHelper.Pi;
                Flip = !Flip;
            }
            else if (DesiredChassisRotation - ChassisRotation <= -MathHelper.PiOver2) {
                ChassisRotation -= MathHelper.Pi;
                Flip = !Flip;
            }
        }

        if (NetPlay.IsClientMatched(PlayerId))
            Client.SyncPlayerTank(this);

        // TODO: optimize?
        ProcessPlayerMouse();

        if (!CampaignGlobals.InMission || LevelEditorUI.IsActive || ChatSystem.ActiveHandle) {
            playerControl_isBindPressed = false;
            return;
        }

        if (!Properties.Stationary) {
            if (NetPlay.IsClientMatched(PlayerId)) {
                if (CurShootStun <= 0 && CurMineStun <= 0) {
                    if (LastUsedController)
                        ControlHandle_ConsoleController();
                    else
                        ControlHandle_Keybinding();
                }
            }
        }

        if (NetPlay.IsClientMatched(PlayerId)) {
            if (InputUtils.CanDetectClick()) {
                if (!ChatSystem.ChatBoxHover && !ChatSystem.ActiveHandle && !GameUI.Paused) {
                    Shoot(false);
                }
            }
        }

        oldPosition = Position;
    }
    void ProcessPlayerMouse() {
        if (NetPlay.IsClientMatched(PlayerId)) {
            if (!Difficulties.Types["POV"] || LevelEditorUI.IsActive || MainMenuUI.IsActive) {
                Vector3 mouseWorldPos = MatrixUtils.GetWorldPosition(MouseUtils.MousePosition, -11f);
                if (!LevelEditorUI.IsActive)
                    TurretRotation = -(new Vector2(mouseWorldPos.X, mouseWorldPos.Z) - Position).ToRotation() + MathHelper.PiOver2;
                else
                    TurretRotation = ChassisRotation;
            }
            else if (!GameUI.Paused) {
                // if (DebugManager.IsFreecamEnabled && InputUtils.MouseRight) { } 
                if (!DebugManager.IsFreecamEnabled && !InputUtils.CanDetectClick() && !controlMine.JustPressed) {
                    var mouseState = Mouse.GetState();
                    var screenCenter = new Point(WindowUtils.WindowWidth / 2, WindowUtils.WindowHeight / 2);

                    if (_justCenteredMouse) {
                        // skip to avoid jumps
                        _justCenteredMouse = false;
                        return;
                    }

                    // subtract mouse delta eventually
                    int deltaX = mouseState.X - screenCenter.X;

                    TurretRotation += -deltaX / (312f.ToResolutionX());

                    // recenter
                    Mouse.SetPosition(screenCenter.X, screenCenter.Y);
                    _justCenteredMouse = true;
                }
            }
        }
    }
    public override void Remove(bool nullifyMe) {
        if (nullifyMe) {
            GameHandler.AllPlayerTanks[PlayerId] = null;
            GameHandler.AllTanks[WorldId] = null;
            _tankTexture?.Dispose();
        }
        base.Remove(nullifyMe);
    }
    public override void Shoot(bool fxOnly, bool netSend = true) {
        PlayerStatistics.ShellsShot++;
        base.Shoot(false);
    }
    public override void LayMine() {
        PlayerStatistics.MinesLaid++;
        base.LayMine();
    }
    private void ControlHandle_ConsoleController() {

        var leftStick = InputUtils.CurrentGamePadSnapshot.ThumbSticks.Left;
        var rightStick = InputUtils.CurrentGamePadSnapshot.ThumbSticks.Right;
        var dPad = InputUtils.CurrentGamePadSnapshot.DPad;

        // inverse y because stick down is positive y (which is up z)
        DesiredDirection = new Vector2(leftStick.X, -leftStick.Y);

        var rotationMet = ChassisRotation > DesiredChassisRotation - Properties.MaximalTurn 
            && ChassisRotation < DesiredChassisRotation + Properties.MaximalTurn;

        if (leftStick.Length() > 0) {
            playerControl_isBindPressed = true;
        }

        if (dPad.Down == ButtonState.Pressed) {
            playerControl_isBindPressed = true;
            DesiredDirection.Y = 1;
        }
        if (dPad.Up == ButtonState.Pressed) {
            playerControl_isBindPressed = true;
            DesiredDirection.Y = -1;
        }
        if (dPad.Left == ButtonState.Pressed) {
            playerControl_isBindPressed = true;
            DesiredDirection.X = -1;
        }
        if (dPad.Right == ButtonState.Pressed) {
            playerControl_isBindPressed = true;
            DesiredDirection.X = 1;
        }

        if (!rotationMet) {
            Speed *= Properties.Deceleration * (1f - RuntimeData.DeltaTime);
            IsTurning = true;
            if (Speed < 0)
                Speed = 0;
        }
        else {
            if (Difficulties.Types["POV"])
                DesiredDirection = DesiredDirection.Rotate(-TurretRotation + MathHelper.Pi);

            Speed += Properties.Acceleration * RuntimeData.DeltaTime;
            if (Speed > Properties.MaxSpeed)
                Speed = Properties.MaxSpeed;
        }

        var norm = Vector2.Normalize(DesiredDirection);

        DesiredChassisRotation = norm.ToRotation() - MathHelper.PiOver2;

        ChassisRotation = MathUtils.RoughStep(ChassisRotation, DesiredChassisRotation, Properties.TurningSpeed * RuntimeData.DeltaTime);

        if (rightStick.Length() > 0) {
            var unprojectedPosition = MatrixUtils.ConvertWorldToScreen(new Vector3(0, 11, 0), World, View, Projection);
            Mouse.SetPosition((int)(unprojectedPosition.X + rightStick.X * 250), (int)(unprojectedPosition.Y - rightStick.Y * 250));
            //Mouse.SetPosition((int)(Input.CurrentMouseSnapshot.X + rightStick.X * TankGame.Instance.Settings.ControllerSensitivity), (int)(Input.CurrentMouseSnapshot.Y - rightStick.Y * TankGame.Instance.Settings.ControllerSensitivity));
        }

        Velocity = Vector2.UnitY.Rotate(ChassisRotation) * Speed;

        if (FireBullet.JustPressed)
            Shoot(false);
        if (PlaceMine.JustPressed)
            LayMine();
    }
    private void ControlHandle_Keybinding() {
        if (controlFirePath.JustPressed)
            _drawShotPath = !_drawShotPath;
        if (controlMine.JustPressed)
            LayMine();

        IsTurning = false;

        //var rotationMet = TankRotation > TargetTankRotation - Properties.MaximalTurn && TankRotation < TargetTankRotation + Properties.MaximalTurn;

        ChassisRotation %= MathHelper.Tau;

        if (controlDown.IsPressed) {
            playerControl_isBindPressed = true;
            DesiredDirection.Y = 1;
            LastUsedController = false;
        }
        if (controlUp.IsPressed) {
            playerControl_isBindPressed = true;
            DesiredDirection.Y = -1;
            LastUsedController = false;
        }
        if (controlLeft.IsPressed) {
            playerControl_isBindPressed = true;
            DesiredDirection.X = -1;
            LastUsedController = false;
        }
        if (controlRight.IsPressed) {
            playerControl_isBindPressed = true;
            DesiredDirection.X = 1;
            LastUsedController = false;
        }

        if (Difficulties.Types["POV"])
            DesiredDirection = DesiredDirection.Rotate(-TurretRotation + MathHelper.Pi);

        var norm = Vector2.Normalize(DesiredDirection);

        DesiredChassisRotation = norm.ToRotation() - MathHelper.PiOver2;

        ChassisRotation = MathUtils.RoughStep(ChassisRotation, DesiredChassisRotation, Properties.TurningSpeed * RuntimeData.DeltaTime);

        Velocity = Vector2.UnitY.Rotate(ChassisRotation) * Speed;
    }
    public override void Destroy(ITankHurtContext context, bool netSend) {
        if (Client.IsConnected()) {
            // maybe make a camera transition to said tank.

            //if (context.Source is not null)
                //CameraGlobals.SpectatorId = CameraGlobals.SpectateValidTank(context.Source.WorldId, true);

            // only decements the lives on the destroyed player's system, where lives are synced across everyone at all times
            if (NetPlay.IsClientMatched(PlayerId)) {
                Lives[PlayerId]--;
            }
        }
        else
            AddLives(-1);

        Remove(false);

        var c = PlayerType switch {
            PlayerID.Blue => TankDeathMark.CheckColor.Blue,
            PlayerID.Red => TankDeathMark.CheckColor.Red,
            PlayerID.Green => TankDeathMark.CheckColor.Green,
            PlayerID.Yellow => TankDeathMark.CheckColor.Yellow, // TODO: change these colors.
            _ => throw new Exception($"Player Death Mark for colour {PlayerType} is not supported."),
        };

        var playerDeathMark = new TankDeathMark(c) {
            Position = Position3D + new Vector3(0, 0.1f, 0),
        };

        playerDeathMark.StoredTank = new TankTemplate {
            IsPlayer = true,
            Position = playerDeathMark.Position.FlattenZ(),
            Rotation = ChassisRotation,
            Team = Team,
            PlayerType = PlayerType,
        };

        base.Destroy(context, netSend);

        if (context.Source is not PlayerTank player) return;

        // only increment these data values on the destroyed player's system
        // ensure the source tank is 
        if (NetPlay.IsClientMatched(player.PlayerId)) {
            TankGame.SaveFile.Suicides++;
            PlayerStatistics.Suicides++;
        }
        TankGame.SaveFile.Deaths++;
    }
    private void DrawShootPath() {
        const int MAX_PATH_UNITS = 10000;

        var whitePixel = TextureGlobals.Pixels[Color.White];
        var pathPos = Position + new Vector2(0, 18).Rotate(-TurretRotation);
        var pathDir = Vector2.UnitY.Rotate(TurretRotation - MathHelper.Pi);
        pathDir.Y *= -1;
        pathDir *= Properties.ShellSpeed;

        var pathRicochetCount = 0;


        for (int i = 0; i < MAX_PATH_UNITS; i++) {
            var dummyPos = Vector2.Zero;

            if (pathPos.X < GameScene.MIN_X || pathPos.X > GameScene.MAX_X) {
                pathRicochetCount++;
                pathDir.X *= -1;
            }
            if (pathPos.Y < GameScene.MIN_Z || pathPos.Y > GameScene.MAX_Z) {
                pathRicochetCount++;
                pathDir.Y *= -1;
            }

            var pathHitbox = new Rectangle((int)pathPos.X - 3, (int)pathPos.Y - 3, 6, 6);

            // Why is velocity passed by reference here lol
            Collision.HandleCollisionSimple_ForBlocks(pathHitbox, pathDir, ref dummyPos, out var dir, out var block, out bool corner, false, (c) => c.Properties.IsSolid);

            if (corner)
                return;
            if (block != null) {
                if (block.Properties.AllowShotPathBounce) {
                    switch (dir) {
                        case CollisionDirection.Up:
                        case CollisionDirection.Down:
                            pathDir.Y *= -1;
                            pathRicochetCount += block.Properties.PathBounceCount;
                            break;
                        case CollisionDirection.Left:
                        case CollisionDirection.Right:
                            pathDir.X *= -1;
                            pathRicochetCount += block.Properties.PathBounceCount;
                            break;
                    }
                }
            }

            var cannotBounce = pathRicochetCount > Properties.RicochetCount;
            if (cannotBounce) return;
            var tankInPath = GameHandler.AllTanks.FirstOrDefault(
                tnk => tnk is not null && !tnk.IsDestroyed && tnk.CollisionCircle.Intersects(new Circle { Center = pathPos, Radius = 4 }));
            if (Array.IndexOf(GameHandler.AllTanks, tankInPath) > -1 && tankInPath is not null) {
                TanksSpotted = [tankInPath!];
                return;
            }
            TanksSpotted = [];

            pathPos += pathDir;

            var pathPosScreen = MatrixUtils.ConvertWorldToScreen(Vector3.Zero, Matrix.CreateTranslation(pathPos.X, 11, pathPos.Y), CameraGlobals.GameView, CameraGlobals.GameProjection);
            var off = MathF.Abs(MathF.Sin(i * MathF.PI / 5 - RuntimeData.RunTime * 0.3f));
            var rgbColor = ColorUtils.HsvToRgb(RuntimeData.UpdateCount + i % 255 / 255f * 360, 1, 1);
            var scale = Vector2.One * 4 * off;
            DrawUtils.DrawTextureWithBorder(TankGame.SpriteRenderer, whitePixel, pathPosScreen, Color.Black,
                rgbColor, scale, 0f, Anchor.Center, 1f);
            //TankGame.SpriteRenderer.Draw(whitePixel, pathPosScreen, null, ColorUtils.HsvToRgb(RuntimeData.UpdateCount + i % 255 / 255f * 360, 1, 1), 0, whitePixel.Size() / 2, new Vector2(3 + off).ToResolution(), default, default);
        }
    }
    public override void Render() {
        base.Render();
        if (IsDestroyed)
            return;
        DrawExtras();
        if (Properties.Invisible && CampaignGlobals.InMission)
            return;
        for (int i = 0; i < (Lighting.AccurateShadows ? 2 : 1); i++) {
            foreach (ModelMesh mesh in Model.Meshes) {
                foreach (BasicEffect effect in mesh.Effects) {
                    effect.World = i == 0 ? boneTransforms[mesh.ParentBone.Index] : 
                        boneTransforms[mesh.ParentBone.Index] 
                        * Matrix.CreateShadow(Lighting.AccurateLightingDirection, new(Vector3.UnitY, 0)) * Matrix.CreateTranslation(0, 0.2f, 0);
                    effect.View = View;
                    effect.Projection = Projection;
                    effect.TextureEnabled = true;

                    if (!Properties.HasTurret)
                        if (mesh.Name == "Cannon")
                            return;

                    if (mesh.Name == "Shadow") {
                        if (!Lighting.AccurateShadows) {
                            effect.Alpha = 0.5f;
                            effect.Texture = _shadowTexture;
                            mesh.Draw();
                        }
                        continue;
                    }

                    effect.Alpha = 1f;
                    effect.Texture = _tankTexture;

                    if (IsHoveredByMouse)
                        effect.EmissiveColor = Color.White.ToVector3();
                    else
                        effect.EmissiveColor = Color.Black.ToVector3();
                    if (ShowTeamVisuals) {
                        if (Team != TeamID.NoTeam) {
                            //var ex = new Color[1024];

                            //Array.Fill(ex, new Color(Client.ClientRandom.Next(0, 256), Client.ClientRandom.Next(0, 256), Client.ClientRandom.Next(0, 256)));

                            //effect.Texture.SetData(0, new Rectangle(0, 8, 32, 15), ex, 0, 480);
                            var ex = new Color[1024];

                            Array.Fill(ex, TeamID.TeamColors[Team]);

                            effect.Texture.SetData(0, new Rectangle(0, 0, 32, 9), ex, 0, 288);
                            effect.Texture.SetData(0, new Rectangle(0, 23, 32, 9), ex, 0, 288);
                        }
                    }
                    effect.SetDefaultGameLighting_IngameEntities(specular: _isPlayerModel, ambientMultiplier: _isPlayerModel ? 2f : 0.9f);
                    mesh.Draw();
                }
            }
        }
    }
    private void DrawExtras() {
        if (IsDestroyed)
            return;

        if (!MainMenuUI.IsActive) {
            if (NetPlay.IsClientMatched(PlayerId)) {
                var tex = GameResources.GetGameResource<Texture2D>("Assets/textures/ui/bullet_ui");
                var scale = 0.5f; // the graphic gets smaller for each availiable shell.
                for (int i = 0; i < Properties.ShellLimit; i++) {
                    var scalar = 0.95f + (i * 0.001f); //changetankproperty ShellLimit 
                    scalar = MathHelper.Clamp(scalar, 0f, 0.99f);
                    scale *= scalar;
                }
                var realSize = (tex.Size() * scale).ToResolution();
                for (int i = 1; i <= Properties.ShellLimit; i++) {
                    var colorToUse = i > OwnedShellCount ? Color.White : Color.DimGray;
                    var position = new Vector2(WindowUtils.WindowWidth - realSize.X * scale, 0);
                    TankGame.SpriteRenderer.Draw(tex, position + new Vector2(0, i * (realSize.Y + (scale * 10).ToResolutionY())), null, colorToUse, 0f, new Vector2(tex.Size().X, 0), new Vector2(scale).ToResolution(), default, default);
                }
            }
        }

        // a bit hardcoded but whatever
        bool needClarification = (!MainMenuUI.IsActive && IntermissionHandler.TankFunctionWait > 0) || MainMenuUI.MenuState == MainMenuUI.UIState.Mulitplayer;

        if (needClarification) {
            var playerColor = PlayerID.PlayerTankColors[PlayerType];
            var pos = MatrixUtils.ConvertWorldToScreen(Vector3.Zero, World, View, Projection) - new Vector2(0, 50).ToResolution();

            bool flip = false;

            float rotation = 0f;

            // flip the graphic so it doesn't appear offscreen if it would normally appear too high
            if (pos.Y <= 150) {
                flip = true;
                pos.Y += 75;
                rotation = MathHelper.Pi;
            }
            var tex1 = GameResources.GetGameResource<Texture2D>("Assets/textures/ui/chevron_border");
            var tex2 = GameResources.GetGameResource<Texture2D>("Assets/textures/ui/chevron_inside");

            // var p = GameHandler.AllPlayerTanks;
            //string pText = "nerd";
            var scale = 0.3f;
            string pText = Client.IsConnected() ? Server.ConnectedClients[PlayerId].Name : $"P{PlayerId + 1}"; // heeheeheeha

            TankGame.SpriteRenderer.Draw(tex1, pos, null, Color.White, rotation, Anchor.BottomCenter.GetAnchor(tex1.Size()), scale.ToResolution(), default, default);
            TankGame.SpriteRenderer.Draw(tex2, pos, null, playerColor, rotation, Anchor.BottomCenter.GetAnchor(tex2.Size()), scale.ToResolution(), default, default);

            DrawUtils.DrawStringWithBorder(TankGame.SpriteRenderer, FontGlobals.RebirthFontLarge, pText, new(pos.X, pos.Y + (flip ? 90 : -110).ToResolutionY()), playerColor, Color.White, new Vector2(scale * 2).ToResolution(), 0f, Anchor.Center, 2f);
        }

        if (DebugManager.DebugLevel == 1 || _drawShotPath)
            DrawShootPath();

        if (Properties.Invisible && CampaignGlobals.InMission)
            return;

        Properties.Armor?.Render();
    }
    public override string ToString()
        => $"pos: {Position} | vel: {Velocity} | dead: {IsDestroyed} | rotation: {ChassisRotation} | OwnedBullets: {OwnedShellCount}";
}