using GTA;
using GTA.Math;
using GTA.Native;

/* NOTE
 * I use the sin and cos natives because Math.Sin and Math.Cos produced strange results. Probably just because I'm a fuckin' idiot and doing everything wrong... but still; that's just how it is
 */

namespace FreecamV
{
    internal class Freecam
    {
        static Camera FCamera;
        static Entity AttachedEntity;
        static Vector3 OffsetCoords = Vector3.Zero;
        static Scaleform scaleform;

        static bool SlowMode = true;
        static bool Frozen = false;
        static bool Lock = false;
        static bool HUD = true;
        static bool Attached = false;

        static float OffsetRotX = 0.0f;
        static float OffsetRotY = 0.0f;
        static float OffsetRotZ = 0.0f;
        static float Speed = 5.0f;

        static int FilterIndex = 0; // 0 == None

        public static void Tick()
        {
            // Check if the camera is enabled or not
            if (FCamera == null || !FCamera.Equals(World.RenderingCamera) || Game.IsPaused)
                return;

            Function.Call(Hash._DISABLE_FIRST_PERSON_CAM_THIS_FRAME);
            if (!Lock)
                Game.DisableAllControlsThisFrame();

            if (HUD)
            {
                scaleform.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
                scaleform.Render2D();
            }

            Vector3 CamCoord = FCamera.Position;
            Vector3 NewPos = ProcessNewPos(CamCoord);

            if (!Function.Call<bool>(Hash.IS_RADAR_HIDDEN))
                Function.Call(Hash.DISPLAY_RADAR, false);

            FCamera.Position = NewPos;
            FCamera.Rotation = new Vector3(OffsetRotX, OffsetRotY, OffsetRotZ);
            Function.Call(Hash._SET_FOCUS_AREA, NewPos.X, NewPos.Y, NewPos.Z, 0.0f, 0.0f, 0.0f);

            #region Misc Controls
            if (!Lock)
            {
                // Misc controls
                if (Attached && Game.IsControlJustPressed(Control.CursorCancel))
                {
                    // Attachment cleanup
                    FCamera.Detach();
                    AttachedEntity = null;
                    Attached = false;
                    scaleform.CallFunction("SET_DATA_SLOT", 12, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.CursorAccept, 0), "Attach");
                    scaleform.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
                }
                else if (Game.IsControlJustPressed(Control.CursorAccept))
                {
                    Entity AttachEnt = GetEntityInFrontOfCam(FCamera);
                    if (AttachEnt != null)
                    {
                        AttachedEntity = AttachEnt;
                        OffsetCoords = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS, AttachedEntity, FCamera.Position.X, FCamera.Position.Y, FCamera.Position.Z);
                        FCamera.AttachTo(AttachedEntity, new Vector3(OffsetCoords.X, OffsetCoords.Y, OffsetCoords.Z));
                        Attached = true;
                        scaleform.CallFunction("SET_DATA_SLOT", 12, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.CursorCancel, 0), "Detach");
                        scaleform.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
                    }
                }

                if (Game.IsControlJustPressed(Control.VehicleHeadlight))
                    HUD = !HUD;
                if (Game.IsControlPressed(Control.FrontendUp))
                    FCamera.FieldOfView -= 1;
                else if (Game.IsControlPressed(Control.FrontendDown))
                    FCamera.FieldOfView += 1;
                if (Game.IsControlJustPressed(Control.FrontendLeft))
                {
                    if (FilterIndex == 0) FilterIndex = Config.Filters.Count - 1;
                    else FilterIndex--;
                    Function.Call(Hash.SET_TIMECYCLE_MODIFIER, Config.Filters[FilterIndex]);
                    Function.Call(Hash.SET_TIMECYCLE_MODIFIER_STRENGTH, Config.FilterIntensity);
                    scaleform.CallFunction("SET_DATA_SLOT", 8, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.FrontendLeft, 0), $"Filter: [{Config.Filters[FilterIndex]}]");
                    scaleform.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
                }
                else if (Game.IsControlJustPressed(Control.FrontendRight))
                {
                    if (FilterIndex == Config.Filters.Count - 1) FilterIndex = 0;
                    else FilterIndex++;
                    Function.Call(Hash.SET_TIMECYCLE_MODIFIER, Config.Filters[FilterIndex]);
                    Function.Call(Hash.SET_TIMECYCLE_MODIFIER_STRENGTH, Config.FilterIntensity);
                    scaleform.CallFunction("SET_DATA_SLOT", 8, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.FrontendLeft, 0), $"Filter: [{Config.Filters[FilterIndex]}]");
                    scaleform.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
                }
                else if (Game.IsControlJustPressed(Control.Reload))
                {
                    FilterIndex = 0;
                    Function.Call(Hash.SET_TIMECYCLE_MODIFIER, "None");
                    scaleform.CallFunction("SET_DATA_SLOT", 8, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.FrontendLeft, 0), $"Filter: [{Config.Filters[FilterIndex]}]");
                    scaleform.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
                }
                if (Game.IsControlJustPressed(Control.Detonate))
                {
                    if (!SlowMode) Game.TimeScale /= Config.SlowMotionMultiplier;
                    else Game.TimeScale = 1;
                    SlowMode = !SlowMode;
                }
                if (Game.IsControlJustPressed(Control.VehicleExit))
                {
                    SlowMode = !SlowMode;
                    Frozen = !Frozen;
                    Game.Pause(Frozen);
                }
            }
            if (Game.IsControlJustPressed(Control.FrontendAccept))
            {
                Lock = !Lock;

                if (Lock)
                {
                    Game.Pause(false);
                    HUD = false;
                }
                else
                    HUD = true;
            }
            #endregion
        }


        #region Movement
        public static Vector3 ProcessNewPos(Vector3 CurrentPos)
        {
            Vector3 Return = CurrentPos;

            if (Function.Call<bool>(Hash._IS_INPUT_DISABLED, 0) && !Lock)
            {
                // Basic movement--- WASD
                if (Game.IsControlPressed(Control.MoveUpOnly)) // Forwards
                {
                    float multX = Function.Call<float>(Hash.SIN, OffsetRotZ);
                    float multY = Function.Call<float>(Hash.COS, OffsetRotZ);
                    float multZ = Function.Call<float>(Hash.SIN, OffsetRotX);

                    Return.X -= (float)(0.1 * Speed * multX);
                    Return.Y += (float)(0.1 * Speed * multY);
                    Return.Z += (float)(0.1 * Speed * multZ);
                }
                if (Game.IsControlPressed(Control.MoveDownOnly)) // Backwards
                {
                    float multX = Function.Call<float>(Hash.SIN, OffsetRotZ);
                    float multY = Function.Call<float>(Hash.COS, OffsetRotZ);
                    float multZ = Function.Call<float>(Hash.SIN, OffsetRotX);

                    Return.X += (float)(0.1 * Speed * multX);
                    Return.Y -= (float)(0.1 * Speed * multY);
                    Return.Z -= (float)(0.1 * Speed * multZ);
                }
                if (Game.IsControlPressed(Control.MoveLeftOnly)) // Left
                {
                    float multX = Function.Call<float>(Hash.SIN, OffsetRotZ + 90.0f);
                    float multY = Function.Call<float>(Hash.COS, OffsetRotZ + 90.0f);

                    Return.X -= (float)(0.1 * Speed * multX);
                    Return.Y += (float)(0.1 * Speed * multY);
                }
                if (Game.IsControlPressed(Control.MoveRightOnly)) // Right
                {
                    float multX = Function.Call<float>(Hash.SIN, OffsetRotZ + 90.0f);
                    float multY = Function.Call<float>(Hash.COS, OffsetRotZ + 90.0f);

                    Return.X += (float)(0.1 * Speed * multX);
                    Return.Y -= (float)(0.1 * Speed * multY);
                }

                // Up/Down
                if (Game.IsControlPressed(Control.Jump)) // Up
                    Return.Z += (float)(0.1 * Speed);
                if (Game.IsControlPressed(Control.Duck)) // Down
                    Return.Z -= (float)(0.1 * Speed);

                // Speed-- Shift
                if (Game.IsControlPressed(Control.Sprint))
                    Speed = Config.ShiftSpeed;
                else
                    Speed = Config.DefaultSpeed;

                // Rotation-- Q/E
                OffsetRotX -= (Game.GetDisabledControlValueNormalized(Control.LookUpDown) * Config.Precision * 8.0f);
                OffsetRotZ -= (Game.GetDisabledControlValueNormalized(Control.LookLeftRight) * Config.Precision * 8.0f);
                if (Game.IsControlPressed(Control.Cover))
                    OffsetRotY -= Config.Precision;
                if (Game.IsControlPressed(Control.Pickup))
                    OffsetRotY += Config.Precision;
            }

            if (OffsetRotX > 90.0) OffsetRotX = 90.0f;
            else if (OffsetRotX < -90.0f) OffsetRotX = -90.0f;

            return Return;
        }
        #endregion

        public static void Enable()
        {
            FCamera = World.CreateCamera(GameplayCamera.Position, GameplayCamera.Rotation, GameplayCamera.FieldOfView);
            FCamera.Direction = GameplayCamera.Direction;
            Function.Call(Hash.DISPLAY_RADAR, false);
            HUD = true;
            Function.Call(Hash.SET_TIMECYCLE_MODIFIER_STRENGTH, Config.FilterIntensity);
            Function.Call(Hash.SET_TIMECYCLE_MODIFIER, Config.Filters[FilterIndex]);
            World.RenderingCamera = FCamera;
            Init();
            if (SlowMode) Game.TimeScale /= Config.SlowMotionMultiplier;
        }

        public static void Disable()
        {
            FCamera.Delete();
            Game.Player.Character.IsCollisionEnabled = true; // Just a quick and easy workaround for collision issues
            Function.Call(Hash.DISPLAY_RADAR, true);
            Function.Call(Hash.CLEAR_FOCUS);
            Function.Call(Hash.SET_TIMECYCLE_MODIFIER, "None");
            World.RenderingCamera = null;
            Game.Pause(false);
            Frozen = false;
            Attached = false;
            Lock = false;
            if (SlowMode) Game.TimeScale = 1;
        }

        public static void Init()
        {
            scaleform = new Scaleform("instructional_buttons");
            scaleform.CallFunction("CLEAR_ALL", new object[0]);
            scaleform.CallFunction("TOGGLE_MOUSE_BUTTONS", 0);

            // Movement/Rotation
            scaleform.CallFunction("SET_DATA_SLOT", 0, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.MoveLeftRight, 0), "");
            scaleform.CallFunction("SET_DATA_SLOT", 1, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.MoveUpDown, 0), "Move");
            scaleform.CallFunction("SET_DATA_SLOT", 2, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.LookLeftRight, 0), "Look");
            scaleform.CallFunction("SET_DATA_SLOT", 3, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.Pickup, 0), "");
            scaleform.CallFunction("SET_DATA_SLOT", 4, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.Cover, 0), "Roll");

            // Misc
            scaleform.CallFunction("SET_DATA_SLOT", 5, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.FrontendDown, 0), "");
            scaleform.CallFunction("SET_DATA_SLOT", 6, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.FrontendUp, 0), "FOV");
            scaleform.CallFunction("SET_DATA_SLOT", 7, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.FrontendRight, 0), "");
            scaleform.CallFunction("SET_DATA_SLOT", 8, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.FrontendLeft, 0), $"Filter: [{Config.Filters[FilterIndex]}]");
            scaleform.CallFunction("SET_DATA_SLOT", 9, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.Reload, 0), $"Reset Filter");
            scaleform.CallFunction("SET_DATA_SLOT", 10, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.Detonate, 0), "Slow Motion");
            scaleform.CallFunction("SET_DATA_SLOT", 11, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.VehicleExit, 0), "Freeze");
            scaleform.CallFunction("SET_DATA_SLOT", 11, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.FrontendAccept, 0), "Control Lock");
            if (!Attached) scaleform.CallFunction("SET_DATA_SLOT", 12, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.CursorAccept, 0), "Attach");
            else scaleform.CallFunction("SET_DATA_SLOT", 12, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, Control.CursorCancel, 0), "Detach");
            // HUD Toggle
            scaleform.CallFunction("SET_DATA_SLOT", 13, Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTON, 2, 74, 0), "Toggle HUD");
        }

        public static void Toggle()
        {
            if (FCamera == null || !FCamera.Equals(World.RenderingCamera) || Game.IsPaused)
                Enable();
            else
                Disable();
        }

        public static Entity GetEntityInFrontOfCam(Camera Cam)
        {
            Vector3 CamCoords = Function.Call<Vector3>(Hash.GET_CAM_COORD, Cam);
            Vector3 Offset = new Vector3()
            {
                // Honestly I have no fucking idea what any of this does. I'm just copying it 
                X = CamCoords.X - Function.Call<float>(Hash.SIN, OffsetRotZ) * 100.0f,
                Y = CamCoords.Y + Function.Call<float>(Hash.COS, OffsetRotZ) * 100.0f,
                Z = CamCoords.Z + Function.Call<float>(Hash.SIN, OffsetRotX) * 100.0f
            };

            //int RayHandle = StartShapeTestRay(CamCoords.X, CamCoords.Y, CamCoords.Z, Offset.X, Offset.Y, Offset.Z, 10, 0, 0);
            var Cast = World.Raycast(CamCoords, Offset, IntersectFlags.Everything);
            if (Cast.DidHit) return Cast.HitEntity;
            else return null;
        }
    }
}
