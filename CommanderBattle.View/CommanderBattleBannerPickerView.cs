using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;
using TaleWorlds.TwoDimension;

namespace CommanderBattle.View
{
    // The banner picker, opened from the formation list's "Banner" button on the player's own row and drawn above it.
    // Apply asks the server for the pick and saves it on this machine; Esc or Cancel closes it.
    public class CommanderBattleBannerPickerView : CommanderBattleLayerView<BannerPickerVM>
    {
        private const string MovieName = "CommanderBattleBannerPicker";

        // Drawn above the formation list (24).
        private const int LayerOrder = 26;

        // The banner icons' sprites. Loaded by the screens that show sigils (the lobby's), and never unloaded here:
        // a sprite category has no reference count, so unloading it would take it from them too.
        private const string BannerIconsCategory = "ui_bannericons";

        private static List<int> _drawableSigils;

        private BattleSideEnum _side = BattleSideEnum.None;

        private bool _lastApplyNow;

        public CommanderBattleBannerPickerView()
        {
            // Below the formation list (24): Esc is offered to views in ascending priority, so this one, on top, closes
            // first.
            ViewOrderPriority = 22;
        }

        private static BattleSideEnum MySide => CommanderBattleTeams.MySide;

        public void Open()
        {
            BattleSideEnum side = MySide;
            if (side == BattleSideEnum.None || !GameNetwork.IsMyPeerReady)
            {
                return;
            }
            LoadIconSprites();
            _side = side;
            Client.GetSavedBanner(side, out int sigil, out int shade);
            int profileSigil = CommanderBanners.GetProfileSigil(Client.GetProfileBannerCode(GameNetwork.MyPeer));
            BannerPickerVM dataSource = new BannerPickerVM(side, GetDrawableSigils(), profileSigil, sigil, shade, OnApply, Close);
            _lastApplyNow = Client.CanTroopSelectionApplyNow;
            dataSource.RefreshStatus(_lastApplyNow);
            OpenLayer(dataSource, MovieName, LayerOrder, captureMouse: true);
        }

        public void Close()
        {
            CloseLayer();
            _side = BattleSideEnum.None;
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (!IsOpen)
            {
                return;
            }
            if (MySide != _side)
            {
                Close();
                return;
            }
            bool applyNow = Client.CanTroopSelectionApplyNow;
            if (applyNow != _lastApplyNow)
            {
                _lastApplyNow = applyNow;
                DataSource.RefreshStatus(applyNow);
            }
        }

        public override bool OnEscape()
        {
            if (IsOpen)
            {
                Close();
                return true;
            }
            return base.OnEscape();
        }

        private void OnApply(int sigil, int shade)
        {
            Client.RequestCommanderBanner(sigil, shade);
            Close();
        }

        private static void LoadIconSprites()
        {
            SpriteCategory category = UIResourceManager.GetSpriteCategory(BannerIconsCategory);
            if (category != null && !category.IsLoaded)
            {
                category.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
            }
        }

        // The pickable icons that have a UI sprite to show them by.
        private static IReadOnlyList<int> GetDrawableSigils()
        {
            if (_drawableSigils == null)
            {
                _drawableSigils = new List<int>();
                foreach (int sigil in CommanderBanners.GetAvailableSigils())
                {
                    if (UIResourceManager.SpriteData?.GetSprite(sigil.ToString()) != null)
                    {
                        _drawableSigils.Add(sigil);
                    }
                }
                CommanderBattleLog.Detail($"Banner picker: {_drawableSigils.Count} of {CommanderBanners.GetAvailableSigils().Count} sigils have a sprite");
            }
            return _drawableSigils;
        }
    }
}
