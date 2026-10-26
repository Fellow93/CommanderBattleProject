using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // What travels with an order the general gives with the order bar (RequestGeneralCommand).
    public enum GeneralCommandPayload
    {
        None,
        // A point on the ground: a move order's position, or the point a facing order looks at.
        Position,
        // A facing direction, from a move line's sub-events.
        Direction,
        // A form order's width, and how much a move line changed the unit spacing to fit.
        Width,
        // A target formation (charge or advance at it).
        Formation,
        // An agent to follow: only the general himself.
        Agent
    }

    // What a player-led formation is told when the general orders it (docs/plans/general-orders.md, v1 kinds).
    public enum GeneralSuggestionKind
    {
        Move,
        Charge,
        Hold,
        Follow,
        Advance,
        FallBack,
        Retreat
    }

    // The general orders formations with the stock order bar: the server applies these orders to the formations the AI
    // leads, and turns the movement ones into suggestions for formations a player leads.
    public static class GeneralCommands
    {
        // A move line sends three commands (move, facing, width), so this leaves room for a quick hand.
        public const int MaxCommandsPerSecond = 40;

        // The stock form order takes any width; the formation clamps it to what its men can fill.
        public const float MaxWidth = 1000f;

        // A move line that had to squeeze the men in lowers the unit spacing by a few steps at most.
        public const int MaxSpacingChange = 8;

        // Orders the order bar can give a formation in multiplayer, by what they carry. Siege orders, transfers and
        // the stock line order (MoveToLineSegment, sent as its per-formation sub-events) are not among them.
        public static bool IsAllowed(OrderType orderType, GeneralCommandPayload payload)
        {
            switch (payload)
            {
                case GeneralCommandPayload.None:
                    switch (orderType)
                    {
                        case OrderType.Charge:
                        case OrderType.StandYourGround:
                        case OrderType.Advance:
                        case OrderType.FallBack:
                        case OrderType.AdvanceTenPaces:
                        case OrderType.FallBackTenPaces:
                        case OrderType.Retreat:
                        case OrderType.LookAtEnemy:
                        case OrderType.HoldFire:
                        case OrderType.FireAtWill:
                        case OrderType.Mount:
                        case OrderType.Dismount:
                        case OrderType.AIControlOn:
                        case OrderType.AIControlOff:
                        case OrderType.ArrangementLine:
                        case OrderType.ArrangementCloseOrder:
                        case OrderType.ArrangementLoose:
                        case OrderType.ArrangementCircular:
                        case OrderType.ArrangementSchiltron:
                        case OrderType.ArrangementVee:
                        case OrderType.ArrangementColumn:
                        case OrderType.ArrangementScatter:
                        case OrderType.FormDeep:
                        case OrderType.FormWide:
                        case OrderType.FormWider:
                            return true;
                        default:
                            return false;
                    }
                case GeneralCommandPayload.Position:
                    return orderType == OrderType.Move || orderType == OrderType.LookAtDirection;
                case GeneralCommandPayload.Direction:
                    return orderType == OrderType.LookAtDirection;
                case GeneralCommandPayload.Width:
                    return orderType == OrderType.FormCustom;
                case GeneralCommandPayload.Formation:
                    return orderType == OrderType.Charge || orderType == OrderType.Advance;
                case GeneralCommandPayload.Agent:
                    return orderType == OrderType.FollowMe;
                default:
                    return false;
            }
        }

        // The orders a player-led formation is told about on their own. A move line's facing and width only come
        // along with its move.
        public static bool TryGetSuggestionKind(OrderType orderType, out GeneralSuggestionKind kind)
        {
            switch (orderType)
            {
                case OrderType.Move:
                    kind = GeneralSuggestionKind.Move;
                    return true;
                case OrderType.Charge:
                case OrderType.ChargeWithTarget:
                    kind = GeneralSuggestionKind.Charge;
                    return true;
                case OrderType.StandYourGround:
                    kind = GeneralSuggestionKind.Hold;
                    return true;
                case OrderType.FollowMe:
                    kind = GeneralSuggestionKind.Follow;
                    return true;
                case OrderType.Advance:
                case OrderType.AdvanceTenPaces:
                    kind = GeneralSuggestionKind.Advance;
                    return true;
                case OrderType.FallBack:
                case OrderType.FallBackTenPaces:
                    kind = GeneralSuggestionKind.FallBack;
                    return true;
                case OrderType.Retreat:
                    kind = GeneralSuggestionKind.Retreat;
                    return true;
                default:
                    kind = GeneralSuggestionKind.Move;
                    return false;
            }
        }

        // The suggestion worded as the general's order to the commander it is for, by name: "Fellow, move here". The
        // pop-up and the Alt marker both use it, for the commander and the general alike. The formation's name stands
        // in if its commander has left since.
        public static string GetSuggestionLine(FormationSlot slot, GeneralSuggestionKind kind, bool hasPlace)
        {
            string name = slot?.Commander?.UserName;
            if (string.IsNullOrEmpty(name))
            {
                name = slot?.DisplayName ?? "";
            }
            return $"{name}, {GetSuggestionText(kind, hasPlace)}";
        }

        // "move here", "charge"; hasPlace: the suggestion has a marker of its own (the move's point or the target).
        public static string GetSuggestionText(GeneralSuggestionKind kind, bool hasPlace)
        {
            switch (kind)
            {
                case GeneralSuggestionKind.Move:
                    return "move here";
                case GeneralSuggestionKind.Charge:
                    return hasPlace ? "charge them" : "charge";
                case GeneralSuggestionKind.Hold:
                    return "hold your ground";
                case GeneralSuggestionKind.Follow:
                    return "follow me";
                case GeneralSuggestionKind.Advance:
                    return hasPlace ? "advance on them" : "advance";
                case GeneralSuggestionKind.FallBack:
                    return "fall back";
                default:
                    return "retreat";
            }
        }

        public const string NotSuggestedNotice = "Formations a player leads are only told movement orders (move, charge, hold, follow, advance, fall back, retreat).";
    }
}
