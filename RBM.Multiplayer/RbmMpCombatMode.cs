using System;
using System.Reflection;
using HarmonyLib;

namespace RBM.Multiplayer
{
    // Commander Battle's combat mode (CommanderBattle.CombatModeSetting): RBM Combat runs only in RBM mode. Read by
    // name, so this module does not depend on CommanderBattle.dll; without Commander Battle it is always Light.
    // On the server the mode is set at module load; a client's changes when it joins a server.
    public static class RbmMpCombatMode
    {
        private const string SettingType = "CommanderBattle.CombatModeSetting";

        private static Func<bool> _isRbm;

        public static bool IsRbm
        {
            get
            {
                if (_isRbm == null)
                {
                    MethodInfo getter = AccessTools.PropertyGetter(AccessTools.TypeByName(SettingType), "IsRbm");
                    _isRbm = getter != null ? (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter) : () => false;
                    if (getter == null)
                    {
                        RbmMpLog.Write($"Combat mode: {SettingType} not found, Light");
                    }
                }
                return _isRbm();
            }
        }
    }
}
