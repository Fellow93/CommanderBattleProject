using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using MathF = TaleWorlds.Library.MathF;

namespace CommanderBattle.View
{
    // General mode (docs/plans/general-mode.md): the side's general presses F1 to look over the battlefield and draws
    // orders on the map for his side, while his soldier stands still. It also draws the side's orders for every
    // player of the side: always in general mode, otherwise while Left Alt is held.
    //
    // The camera is the commander's order camera lifted about three times as far (CommanderBattleOrderCameraView,
    // which owns the camera offsets): the stock follow camera, so the mouse turns it as usual. Holding Left Ctrl
    // stops the turning and shows a cursor for drawing and for the menu.
    //
    // Two layers, which take the mouse only while Left Ctrl is held: this view's own (the paths, labels and the
    // general-mode menu, which takes the mouse only where a button is) and an input layer below it that takes the
    // mouse everywhere else. So a press on the input layer is a press on the ground, never on the menu.
    public class CommanderBattleGeneralModeView : CommanderBattleLayerView<GeneralModeVM>
    {
        private const string MovieName = "CommanderBattleGeneralMode";

        private const string InputMovieName = "CommanderBattleGeneralModeInput";

        // Right below this view's layer.
        private const int InputLayerOrder = 12;

        // ScreenPointToWorldRay cuts the ray at the boundary radius, which a high camera can fall short of.
        private const float RayLength = 3000f;

        // While drawing, a point every few metres (more apart the further the camera is); the path is thinned to
        // GeneralOrders.MaxPoints when it is sent.
        private const float MinPointSpacing = 3f;

        private const float PointSpacingPerDistance = 0.02f;

        private const int MaxRawPoints = 1024;

        // Shorter than this, a press was a click, not a path.
        private const float MinPathLength = 5f;

        private const float LineAboveGround = 0.5f;

        private const float LabelAboveGround = 2f;

        private const float SegmentThickness = 4f;

        private const string HitMessage = "You were hit: general mode closed.";

        private static readonly Color NoticeColor = Color.FromUint(0xFFE8C14Au);

        private enum Tool
        {
            Path,
            Delete
        }

        private GauntletLayer _inputLayer;

        private MissionMainAgentController _agentController;

        // SetDisplayDialog is one flag shared by every view: cleared on leaving only if general mode set it.
        private bool _ownsDisplayDialog;

        private bool _isGeneralMode;

        // Left Ctrl is held: the cursor shows and the two layers take the mouse.
        private bool _isCursorShown;

        private Tool _tool = Tool.Path;

        // What the general and his horse had, to leave when either is hit.
        private float _lastHealth;

        private Agent _lastMount;

        private float _lastMountHealth;

        private bool _wasHit;

        private bool _isDrawing;

        private readonly List<Vec2> _pendingPoints = new List<Vec2>();

        // The label popup is open for a finished path. Each popup gets a number, so a late answer to an old one
        // (closed by leaving general mode) is ignored.
        private bool _isAwaitingLabel;

        private int _labelRequest;

        private bool _dirty = true;

        private bool _hasOrders;

        private bool _wasShown;

        public CommanderBattleGeneralModeView()
        {
            // Below the AI status (14), the order bar (19), the menus (22-24) and the escape menu (50). OnEscape asks
            // the lowest first, so Esc leaves general mode before it would open the escape menu.
            ViewOrderPriority = 13;
        }

        private CommanderBattleFormationSelectionView FormationSelection => Mission.GetMissionBehavior<CommanderBattleFormationSelectionView>();

        private CommanderBattleTroopSelectionView TroopSelection => Mission.GetMissionBehavior<CommanderBattleTroopSelectionView>();

        private CommanderBattleOrderCameraView CameraView => Mission.GetMissionBehavior<CommanderBattleOrderCameraView>();

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            Client.OnGeneralOrdersChanged += SetDirty;
            Client.Slots.OnSlotChanged += OnSlotChanged;
            MissionPeer.OnTeamChanged += OnTeamChanged;
            OpenLayer(new GeneralModeVM(() => SelectTool(Tool.Path), () => SelectTool(Tool.Delete)), MovieName, ViewOrderPriority, captureMouse: false);
        }

        public override void OnMissionScreenFinalize()
        {
            Leave(null);
            Client.OnGeneralOrdersChanged -= SetDirty;
            Client.Slots.OnSlotChanged -= OnSlotChanged;
            MissionPeer.OnTeamChanged -= OnTeamChanged;
            base.OnMissionScreenFinalize();
        }

        private void SetDirty()
        {
            _dirty = true;
        }

        private void OnSlotChanged(FormationSlot slot)
        {
            _dirty = true;
        }

        private void OnTeamChanged(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (peer.IsMine)
            {
                _dirty = true;
            }
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            Leave(null);
            _dirty = true;
        }

        // Handled on the next screen tick rather than here, in the middle of the mission's blow handling.
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            Agent general = Agent.Main;
            if (_isGeneralMode && affectedAgent != null && general != null && (affectedAgent == general || affectedAgent == general.MountAgent))
            {
                _wasHit = true;
            }
        }

        public override bool OnEscape()
        {
            // The formation list and troop screen, drawn on top, close first.
            if (_isGeneralMode && FormationSelection?.IsOpen != true && TroopSelection?.IsOpen != true)
            {
                Leave(null);
                return true;
            }
            return base.OnEscape();
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (!IsOpen)
            {
                return;
            }
            if (_isGeneralMode)
            {
                TickGeneralMode();
            }
            else if (IsTogglePressed() && CanEnter())
            {
                Enter();
            }
            if (_dirty)
            {
                _dirty = false;
                Rebuild();
            }
        }

        // Mission.OnTick updates the camera before the behaviours tick, so the projection uses this frame's camera.
        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (IsOpen)
            {
                UpdateScreenPositions();
            }
        }

        // --- Entering and leaving ---

        // F1 is the stock SelectOrder1 key. CommanderBattleOrderUIHandler ignores it for the general. Its gamepad
        // button is skipped as the stock order bar skips it outside the bar: general mode needs the mouse anyway.
        private bool IsTogglePressed()
        {
            GameKeyContext category = HotKeyManager.GetCategory(MissionOrderHotkeyCategory.CategoryId);
            if (category == null || TaleWorlds.InputSystem.Input.IsGamepadActive)
            {
                return false;
            }
            // The order UI registers it on the scene layer; this only covers a view order where it hasn't yet.
            InputContext sceneInput = MissionScreen.SceneLayer.Input;
            if (!sceneInput.IsCategoryRegistered(category))
            {
                sceneInput.RegisterHotKeyCategory(category);
            }
            return sceneInput.IsGameKeyPressed(MissionOrderHotkeyCategory.SelectOrder1);
        }

        private bool IsGeneral => Client.MySlot is FormationSlot slot && slot.IsGeneral && slot.Side == CommanderBattleTeams.MySide;

        private bool CanEnter()
        {
            Agent agent = Agent.Main;
            return IsGeneral
                && agent != null && agent.IsActive()
                && Client.IsRoundInProgress
                && MissionScreen.CustomCamera == null
                && !MissionScreen.IsRadialMenuActive
                && !InformationManager.IsAnyInquiryActive()
                && FormationSelection?.IsOpen != true
                && TroopSelection?.IsOpen != true;
        }

        private void Enter()
        {
            Agent agent = Agent.Main;
            _agentController = Mission.GetMissionBehavior<MissionMainAgentController>();
            // Silences the views that act on the main agent's keys (cheer, equip, drop).
            _ownsDisplayDialog = MissionScreen.SetDisplayDialog(true);
            _lastHealth = agent.Health;
            _lastMount = agent.MountAgent;
            _lastMountHealth = _lastMount?.Health ?? 0f;
            _wasHit = false;
            FreezeAgent();
            // Kept for the whole of general mode; it takes the mouse only while the cursor shows.
            _inputLayer = new GauntletLayer(InputMovieName, InputLayerOrder);
            _inputLayer.LoadMovie(InputMovieName, DataSource);
            MissionScreen.AddLayer(_inputLayer);
            _isCursorShown = false;
            _isGeneralMode = true;
            DataSource.IsGeneralMode = true;
            CommanderBattleOrderCameraView cameraView = CameraView;
            if (cameraView != null)
            {
                cameraView.IsGeneralMode = true;
            }
            SelectTool(_tool);
        }

        // message: shown in the message log; null or empty for none.
        private void Leave(string message)
        {
            if (!_isGeneralMode)
            {
                return;
            }
            _isGeneralMode = false;
            CancelDrawing();
            if (_isAwaitingLabel)
            {
                _isAwaitingLabel = false;
                InformationManager.HideInquiry();
            }
            CommanderBattleOrderCameraView cameraView = CameraView;
            if (cameraView != null)
            {
                cameraView.IsGeneralMode = false;
            }
            if (_agentController != null)
            {
                _agentController.IsDisabled = false;
                _agentController = null;
            }
            if (_ownsDisplayDialog)
            {
                MissionScreen.SetDisplayDialog(false);
                _ownsDisplayDialog = false;
            }
            SetCursor(false);
            if (_inputLayer != null)
            {
                MissionScreen.RemoveLayer(_inputLayer);
                _inputLayer = null;
            }
            if (DataSource != null)
            {
                DataSource.IsGeneralMode = false;
                UpdateLabelsDeletable();
            }
            if (!string.IsNullOrEmpty(message))
            {
                InformationManager.DisplayMessage(new InformationMessage(message, NoticeColor));
            }
        }

        private void TickGeneralMode()
        {
            string leaveReason = GetLeaveReason();
            if (leaveReason != null)
            {
                Leave(leaveReason);
                return;
            }
            FreezeAgent();
            // The label popup has the keys and the mouse; the formation list and troop screen (N) sit on top.
            if (InformationManager.IsAnyInquiryActive() || FormationSelection?.IsOpen == true || TroopSelection?.IsOpen == true)
            {
                return;
            }
            if (IsTogglePressed())
            {
                Leave(null);
                return;
            }
            bool wantsCursor = Input.IsKeyDown(InputKey.LeftControl);
            // Letting go of Ctrl mid-drag ends the path as letting go of the button would: the label popup comes
            // up, and its Cancel discards the path.
            if (!wantsCursor && _isDrawing)
            {
                FinishDrawing();
            }
            if (wantsCursor != _isCursorShown)
            {
                SetCursor(wantsCursor);
            }
            Zoom();
            HandleMouse();
        }

        // Null to stay; empty to leave without a message.
        private string GetLeaveReason()
        {
            Agent agent = Agent.Main;
            if (agent == null || !agent.IsActive() || !IsGeneral || !Client.IsRoundInProgress)
            {
                return "";
            }
            // He stands still in the open. Hits arrive through OnAgentHit when the client runs the blow, and as a
            // health drop (health is synced to every client) otherwise; losing the horse counts too.
            Agent mount = agent.MountAgent;
            if (_wasHit || agent.Health < _lastHealth || mount != _lastMount || (mount != null && mount.Health < _lastMountHealth))
            {
                return HitMessage;
            }
            _lastHealth = agent.Health;
            _lastMountHealth = mount?.Health ?? 0f;
            return null;
        }

        // MissionScreen.UpdateCamera turns the controller back on every frame for an agent the camera follows, so
        // it is turned off again each tick: the screen tick runs between that and the controller's next tick. The
        // camera's bearing and elevation come from MissionScreen's own input, so the mouse still turns the view.
        // The agent also keeps the last movement it was given.
        private void FreezeAgent()
        {
            if (_agentController != null)
            {
                _agentController.IsDisabled = true;
            }
            Agent agent = Agent.Main;
            if (agent != null && agent.IsActive())
            {
                agent.MovementFlags = 0;
                agent.MovementInputVector = Vec2.Zero;
            }
        }

        // A visible cursor stops MissionScreen's mouse look (it then only turns the view at the screen's edge, as in
        // the stock order view), so showing it is all it takes to hold the view still.
        private void SetCursor(bool show)
        {
            _isCursorShown = show;
            if (show)
            {
                _inputLayer?.InputRestrictions.SetInputRestrictions(isMouseVisible: true, InputUsageMask.Mouse);
                Layer?.InputRestrictions.SetInputRestrictions(isMouseVisible: true, InputUsageMask.Mouse);
            }
            else
            {
                _inputLayer?.InputRestrictions.ResetInputRestrictions();
                Layer?.InputRestrictions.ResetInputRestrictions();
            }
        }

        // The wheel goes to the scene, or, with the cursor, to whichever of the two layers is under it.
        private void Zoom()
        {
            float scroll = MissionScreen.SceneLayer.Input.GetDeltaMouseScroll() + Layer.Input.GetDeltaMouseScroll() + (_inputLayer?.Input.GetDeltaMouseScroll() ?? 0f);
            if (scroll != 0f)
            {
                CameraView?.ZoomGeneralMode(scroll / 120f);
            }
        }

        // --- Tools ---

        private void SelectTool(Tool tool)
        {
            _tool = tool;
            CancelDrawing();
            DataSource.IsPathTool = tool == Tool.Path;
            DataSource.IsDeleteTool = tool == Tool.Delete;
            DataSource.HintText = tool == Tool.Path
                ? "Path: hold Ctrl, then hold the left mouse button and drag along the ground, and name it. Your side sees it while holding Left Alt."
                : "Delete: hold Ctrl and click a path's label to remove it.";
            UpdateLabelsDeletable();
        }

        private void UpdateLabelsDeletable()
        {
            bool canDelete = _isGeneralMode && _tool == Tool.Delete;
            foreach (GeneralOrderLabelVM label in DataSource.Labels)
            {
                label.CanDelete = canDelete;
            }
        }

        private void OnDeleteClicked(int orderId)
        {
            if (_isGeneralMode && _tool == Tool.Delete && !InformationManager.IsAnyInquiryActive())
            {
                Client.RequestDeleteGeneralOrder(orderId);
            }
        }

        private void HandleMouse()
        {
            if (_tool != Tool.Path || _isAwaitingLabel || !_isCursorShown || _inputLayer == null)
            {
                return;
            }
            IInputContext input = _inputLayer.Input;
            if (!_isDrawing)
            {
                if (input.IsKeyPressed(InputKey.LeftMouseButton) && TryGetGroundPoint(input.GetMousePositionRanged(), out Vec2 start))
                {
                    _isDrawing = true;
                    _pendingPoints.Clear();
                    DataSource.PendingSegments.Clear();
                    AddPendingPoint(start, isLast: true);
                }
                return;
            }
            bool released = input.IsKeyReleased(InputKey.LeftMouseButton) || !input.IsKeyDown(InputKey.LeftMouseButton);
            if (TryGetGroundPoint(input.GetMousePositionRanged(), out Vec2 point))
            {
                AddPendingPoint(point, released);
            }
            if (released)
            {
                FinishDrawing();
            }
        }

        // The ground under the mouse, inside the battlefield.
        private bool TryGetGroundPoint(Vec2 mousePosition, out Vec2 point)
        {
            point = Vec2.Invalid;
            MissionScreen.ScreenPointToWorldRay(mousePosition, out Vec3 rayBegin, out Vec3 rayEnd);
            Vec3 direction = rayEnd - rayBegin;
            if (direction.Normalize() <= 0f)
            {
                return false;
            }
            if (!Mission.Scene.RayCastForClosestEntityOrTerrain(rayBegin, rayBegin + direction * RayLength, out float distance, out Vec3 _, out WeakGameEntity _, 0.3f, BodyFlags.CommonFocusRayCastExcludeFlags | BodyFlags.BodyOwnerFlora))
            {
                return false;
            }
            point = (rayBegin + direction * distance).AsVec2;
            return Mission.IsPositionInsideBoundaries(point);
        }

        // isLast: the release point, kept unless it is right on the previous one.
        private void AddPendingPoint(Vec2 point, bool isLast)
        {
            if (_pendingPoints.Count >= MaxRawPoints)
            {
                return;
            }
            if (_pendingPoints.Count > 0)
            {
                Vec2 previous = _pendingPoints[_pendingPoints.Count - 1];
                float spacing = isLast ? 1f : Math.Max(MinPointSpacing, (MissionScreen.CombatCamera.Position - OnGround(point, 0f)).Length * PointSpacingPerDistance);
                if (previous.Distance(point) < spacing)
                {
                    return;
                }
                DataSource.PendingSegments.Add(new GeneralOrderSegmentVM(OnGround(previous, LineAboveGround), OnGround(point, LineAboveGround)));
            }
            _pendingPoints.Add(point);
        }

        private void FinishDrawing()
        {
            _isDrawing = false;
            if (_pendingPoints.Count < 2 || GetLength(_pendingPoints) < MinPathLength)
            {
                CancelDrawing();
                return;
            }
            Vec2[] points = Thin(_pendingPoints);
            int request = ++_labelRequest;
            _isAwaitingLabel = true;
            // The stock popup is a global layer that takes the keys and shows its own cursor while open, so typing
            // never reaches the agent or the camera, and it confirms on the key's release, so the Enter that closes
            // it doesn't open the chat.
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "Path order",
                $"Name this path (up to {GeneralOrders.MaxLabelLength} characters). Your side sees the name with the path.",
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: true,
                "Draw",
                "Cancel",
                text => OnLabelConfirmed(request, points, text),
                () => OnLabelCancelled(request),
                shouldInputBeObfuscated: false,
                CheckLabel));
        }

        private void OnLabelConfirmed(int request, Vec2[] points, string text)
        {
            if (request != _labelRequest || !_isAwaitingLabel)
            {
                return;
            }
            _isAwaitingLabel = false;
            CancelDrawing();
            // The server's echo (GeneralOrderAdded) draws it.
            Client.RequestGeneralOrder(points, GeneralOrders.SanitizeLabel(text));
        }

        private void OnLabelCancelled(int request)
        {
            if (request != _labelRequest || !_isAwaitingLabel)
            {
                return;
            }
            _isAwaitingLabel = false;
            CancelDrawing();
        }

        private static Tuple<bool, string> CheckLabel(string text)
        {
            if (GeneralOrders.SanitizeLabel(text).Length == 0)
            {
                return new Tuple<bool, string>(false, "Type a name for the path.");
            }
            if ((text ?? "").Trim().Length > GeneralOrders.MaxLabelLength)
            {
                return new Tuple<bool, string>(false, $"At most {GeneralOrders.MaxLabelLength} characters.");
            }
            return new Tuple<bool, string>(true, "");
        }

        private void CancelDrawing()
        {
            _isDrawing = false;
            _pendingPoints.Clear();
            DataSource?.PendingSegments.Clear();
        }

        // At most GeneralOrders.MaxPoints: every other point goes until it fits, the last one always stays.
        private static Vec2[] Thin(List<Vec2> raw)
        {
            List<Vec2> points = new List<Vec2>(raw);
            while (points.Count > GeneralOrders.MaxPoints)
            {
                List<Vec2> thinned = new List<Vec2>(points.Count / 2 + 2);
                for (int i = 0; i < points.Count; i += 2)
                {
                    thinned.Add(points[i]);
                }
                if ((points.Count - 1) % 2 != 0)
                {
                    thinned.Add(points[points.Count - 1]);
                }
                points = thinned;
            }
            return points.ToArray();
        }

        private static float GetLength(IReadOnlyList<Vec2> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += points[i - 1].Distance(points[i]);
            }
            return length;
        }

        // The point halfway along the path.
        private static Vec2 GetMidpoint(IReadOnlyList<Vec2> points)
        {
            float half = GetLength(points) / 2f;
            for (int i = 1; i < points.Count; i++)
            {
                float piece = points[i - 1].Distance(points[i]);
                if (piece >= half && piece > 0f)
                {
                    return Vec2.Lerp(points[i - 1], points[i], half / piece);
                }
                half -= piece;
            }
            return points[points.Count - 1];
        }

        private Vec3 OnGround(Vec2 point, float above)
        {
            return point.ToVec3(Mission.Scene.GetTerrainHeight(point) + above);
        }

        // --- Drawing the orders ---

        private void Rebuild()
        {
            DataSource.Segments.Clear();
            DataSource.Labels.Clear();
            IReadOnlyList<GeneralOrder> orders = Client.GetGeneralOrders(CommanderBattleTeams.MySide);
            bool canDelete = _isGeneralMode && _tool == Tool.Delete;
            foreach (GeneralOrder order in orders)
            {
                if (order.Points == null || order.Points.Length < 2)
                {
                    continue;
                }
                Vec3 previous = OnGround(order.Points[0], LineAboveGround);
                for (int i = 1; i < order.Points.Length; i++)
                {
                    Vec3 next = OnGround(order.Points[i], LineAboveGround);
                    DataSource.Segments.Add(new GeneralOrderSegmentVM(previous, next));
                    previous = next;
                }
                GeneralOrderLabelVM label = new GeneralOrderLabelVM(order.Id, order.Label, OnGround(GetMidpoint(order.Points), LabelAboveGround), OnDeleteClicked)
                {
                    CanDelete = canDelete
                };
                DataSource.Labels.Add(label);
            }
            _hasOrders = DataSource.Labels.Count > 0;
            DataSource.CountText = $"Orders: {orders.Count} of {GeneralOrders.MaxOrdersPerSide}";
        }

        private void UpdateScreenPositions()
        {
            bool show = _isGeneralMode || (_hasOrders && Input.IsKeyDown(InputKey.LeftAlt));
            if (!show)
            {
                if (_wasShown)
                {
                    _wasShown = false;
                    HideAll();
                }
                return;
            }
            _wasShown = true;
            // The follow camera (lifted in general mode) is what is rendered.
            Camera camera = MissionScreen.CombatCamera;
            if (camera == null)
            {
                return;
            }
            float inverseScale = Layer.UIContext.InverseScale;
            foreach (GeneralOrderSegmentVM segment in DataSource.Segments)
            {
                PlaceSegment(segment, camera, inverseScale);
            }
            foreach (GeneralOrderSegmentVM segment in DataSource.PendingSegments)
            {
                PlaceSegment(segment, camera, inverseScale);
            }
            foreach (GeneralOrderLabelVM label in DataSource.Labels)
            {
                label.IsOnScreen = Project(camera, label.WorldPosition, inverseScale, out Vec2 point);
                if (label.IsOnScreen)
                {
                    label.ScreenX = point.x;
                    label.ScreenY = point.y;
                }
            }
        }

        private static void PlaceSegment(GeneralOrderSegmentVM segment, Camera camera, float inverseScale)
        {
            // A segment with an end behind the camera is left out rather than drawn across the screen.
            if (!Project(camera, segment.Start, inverseScale, out Vec2 start) || !Project(camera, segment.End, inverseScale, out Vec2 end))
            {
                segment.IsOnScreen = false;
                return;
            }
            Vec2 along = end - start;
            float length = along.Length;
            segment.IsOnScreen = length > 0.5f;
            if (!segment.IsOnScreen)
            {
                return;
            }
            // The bar's pivot is its left middle (PivotX 0, PivotY 0.5): half its thickness above the start.
            segment.ScreenX = start.x;
            segment.ScreenY = start.y - SegmentThickness / 2f;
            segment.Length = length;
            segment.Angle = MathF.Atan2(along.y, along.x) * (180f / MathF.PI);
        }

        // Real pixels to UI units; false behind the camera.
        private static bool Project(Camera camera, Vec3 worldPosition, float inverseScale, out Vec2 point)
        {
            float screenX = -5000f;
            float screenY = -5000f;
            float w = 0f;
            MBWindowManager.WorldToScreenInsideUsableArea(camera, worldPosition, ref screenX, ref screenY, ref w);
            point = new Vec2(screenX * inverseScale, screenY * inverseScale);
            return w > 0f;
        }

        private void HideAll()
        {
            foreach (GeneralOrderSegmentVM segment in DataSource.Segments)
            {
                segment.IsOnScreen = false;
            }
            foreach (GeneralOrderSegmentVM segment in DataSource.PendingSegments)
            {
                segment.IsOnScreen = false;
            }
            foreach (GeneralOrderLabelVM label in DataSource.Labels)
            {
                label.IsOnScreen = false;
            }
        }
    }
}
