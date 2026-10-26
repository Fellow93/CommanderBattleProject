using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.VisualOrders.OrderSets;
using TaleWorlds.MountAndBlade.View.VisualOrders.Orders;
using TaleWorlds.MountAndBlade.View.VisualOrders.Orders.ToggleOrders;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual.Default.Orders.FormOrders;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual.Default.Orders.MovementOrders;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual.Default.Orders.ToggleOrders;

namespace CommanderBattle.View
{
    // The order bar of the side's general (docs/plans/general-orders.md): the stock layouts of DefaultVisualOrderProvider
    // (default and legacy), with the Delegate toggle (order_toggle_ai) that the stock hides in multiplayer. Delegate
    // hands a formation he took over back to the AI. Transfer stays out, as in stock multiplayer.
    //
    // VisualOrderFactory asks the last registered provider that is available, and the bar rebuilds its order sets on
    // every opening (MissionOrderVM.OpenToggleOrder), so everyone else, and the general once he stops being one, gets
    // the stock bar from the next opening.
    public class CommanderBattleVisualOrderProvider : VisualOrderProvider
    {
        public override bool IsAvailable()
        {
            Mission mission = Mission.Current;
            return mission != null && !mission.IsFriendlyMission && GameNetwork.IsClient
                && mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>()?.IsGeneralHere == true;
        }

        public override MBReadOnlyList<VisualOrderSet> GetOrders()
        {
            return BannerlordConfig.OrderLayoutType == 1 ? GetLegacyOrders() : GetDefaultOrders();
        }

        private static GenericToggleVisualOrder CreateDelegateOrder()
        {
            return new GenericToggleVisualOrder("order_toggle_ai", OrderType.AIControlOn, OrderType.AIControlOff);
        }

        private static GenericVisualOrderSet CreateMovementSet(bool useActiveOrderForName)
        {
            GenericVisualOrderSet movement = new GenericVisualOrderSet("order_type_movement", new TextObject("{=KiJd6Xik}Movement"), useActiveOrderForIconId: true, useActiveOrderForName: useActiveOrderForName);
            movement.AddOrder(new MoveVisualOrder("order_movement_move"));
            movement.AddOrder(new FollowMeVisualOrder("order_movement_follow"));
            movement.AddOrder(new ChargeVisualOrder("order_movement_charge"));
            movement.AddOrder(new AdvanceVisualOrder("order_movement_advance"));
            movement.AddOrder(new FallbackVisualOrder("order_movement_fallback"));
            movement.AddOrder(new StopVisualOrder("order_movement_stop"));
            movement.AddOrder(new RetreatVisualOrder("order_movement_retreat"));
            movement.AddOrder(new ReturnVisualOrder());
            return movement;
        }

        private static GenericVisualOrderSet CreateFormSet(ArrangementVisualOrder line, ArrangementVisualOrder close)
        {
            GenericVisualOrderSet form = new GenericVisualOrderSet("order_type_form", new TextObject("{=iBk2wbn3}Form"), useActiveOrderForIconId: true, useActiveOrderForName: true);
            form.AddOrder(line);
            form.AddOrder(close);
            form.AddOrder(new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.Loose, "order_form_loose"));
            form.AddOrder(new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.Circle, "order_form_circular"));
            form.AddOrder(new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.Square, "order_form_schiltron"));
            form.AddOrder(new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.Skein, "order_form_v"));
            form.AddOrder(new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.Column, "order_form_column"));
            form.AddOrder(new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.Scatter, "order_form_scatter"));
            form.AddOrder(new ReturnVisualOrder());
            return form;
        }

        private static MBReadOnlyList<VisualOrderSet> GetDefaultOrders()
        {
            MBList<VisualOrderSet> sets = new MBList<VisualOrderSet>();
            ArrangementVisualOrder line = new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.Line, "order_form_line");
            ArrangementVisualOrder close = new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.ShieldWall, "order_form_close");
            GenericVisualOrderSet toggle = new GenericVisualOrderSet("order_type_toggle", new TextObject("{=0HTNYQz2}Toggle"), useActiveOrderForIconId: false, useActiveOrderForName: false);
            ToggleFacingVisualOrder facing = new ToggleFacingVisualOrder("order_toggle_facing");
            GenericToggleVisualOrder fire = new GenericToggleVisualOrder("order_toggle_fire", OrderType.FireAtWill, OrderType.HoldFire);
            GenericToggleVisualOrder mount = new GenericToggleVisualOrder("order_toggle_mount", OrderType.Mount, OrderType.Dismount);
            GenericToggleVisualOrder delegateOrder = CreateDelegateOrder();
            toggle.AddOrder(facing);
            toggle.AddOrder(fire);
            toggle.AddOrder(mount);
            toggle.AddOrder(delegateOrder);
            toggle.AddOrder(new ReturnVisualOrder());
            sets.Add(CreateMovementSet(useActiveOrderForName: true));
            sets.Add(CreateFormSet(line, close));
            sets.Add(toggle);
            if (!Input.IsGamepadActive)
            {
                sets.Add(new SingleVisualOrderSet(fire));
                sets.Add(new SingleVisualOrderSet(mount));
                sets.Add(new SingleVisualOrderSet(delegateOrder));
                sets.Add(new SingleVisualOrderSet(facing));
                sets.Add(new SingleVisualOrderSet(close));
                sets.Add(new SingleVisualOrderSet(line));
            }
            return sets;
        }

        private static MBReadOnlyList<VisualOrderSet> GetLegacyOrders()
        {
            MBList<VisualOrderSet> sets = new MBList<VisualOrderSet>();
            GenericVisualOrderSet facingSet = new GenericVisualOrderSet("order_type_facing", new TextObject("{=psynaDsM}Facing"), useActiveOrderForIconId: true, useActiveOrderForName: false);
            facingSet.AddOrder(new SingleVisualOrder("order_toggle_facing", new TextObject("{=MH9Pi3ao}Face Direction"), OrderType.LookAtDirection, useFormationTarget: false, useWorldPositionTarget: true));
            facingSet.AddOrder(new SingleVisualOrder("order_toggle_facing_active", new TextObject("{=u8j8nN5U}Face Enemy"), OrderType.LookAtEnemy, useFormationTarget: false, useWorldPositionTarget: false));
            ArrangementVisualOrder line = new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.Line, "order_form_line");
            ArrangementVisualOrder close = new ArrangementVisualOrder(ArrangementOrder.ArrangementOrderEnum.ShieldWall, "order_form_close");
            sets.Add(CreateMovementSet(useActiveOrderForName: false));
            sets.Add(facingSet);
            sets.Add(CreateFormSet(line, close));
            if (!Input.IsGamepadActive)
            {
                sets.Add(new SingleVisualOrderSet(new GenericToggleVisualOrder("order_toggle_fire", OrderType.FireAtWill, OrderType.HoldFire)));
                sets.Add(new SingleVisualOrderSet(new GenericToggleVisualOrder("order_toggle_mount", OrderType.Mount, OrderType.Dismount)));
                sets.Add(new SingleVisualOrderSet(CreateDelegateOrder()));
                sets.Add(new SingleVisualOrderSet(new ReturnVisualOrder()));
            }
            return sets;
        }
    }
}
