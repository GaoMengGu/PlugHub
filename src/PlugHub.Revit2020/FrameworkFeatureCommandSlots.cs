using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace PlugHub.Revit2020
{
    [Transaction(TransactionMode.Manual)]
    public abstract class FrameworkFeatureCommandSlot : IExternalCommand
    {
        protected abstract int SlotId { get; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return FeatureCommandDispatcher.ExecuteSlot(SlotId, commandData, ref message, elements);
        }
    }

    public sealed class FrameworkFeatureCommandSlot001 : FrameworkFeatureCommandSlot { protected override int SlotId => 1; }
    public sealed class FrameworkFeatureCommandSlot002 : FrameworkFeatureCommandSlot { protected override int SlotId => 2; }
    public sealed class FrameworkFeatureCommandSlot003 : FrameworkFeatureCommandSlot { protected override int SlotId => 3; }
    public sealed class FrameworkFeatureCommandSlot004 : FrameworkFeatureCommandSlot { protected override int SlotId => 4; }
    public sealed class FrameworkFeatureCommandSlot005 : FrameworkFeatureCommandSlot { protected override int SlotId => 5; }
    public sealed class FrameworkFeatureCommandSlot006 : FrameworkFeatureCommandSlot { protected override int SlotId => 6; }
    public sealed class FrameworkFeatureCommandSlot007 : FrameworkFeatureCommandSlot { protected override int SlotId => 7; }
    public sealed class FrameworkFeatureCommandSlot008 : FrameworkFeatureCommandSlot { protected override int SlotId => 8; }
    public sealed class FrameworkFeatureCommandSlot009 : FrameworkFeatureCommandSlot { protected override int SlotId => 9; }
    public sealed class FrameworkFeatureCommandSlot010 : FrameworkFeatureCommandSlot { protected override int SlotId => 10; }
    public sealed class FrameworkFeatureCommandSlot011 : FrameworkFeatureCommandSlot { protected override int SlotId => 11; }
    public sealed class FrameworkFeatureCommandSlot012 : FrameworkFeatureCommandSlot { protected override int SlotId => 12; }
    public sealed class FrameworkFeatureCommandSlot013 : FrameworkFeatureCommandSlot { protected override int SlotId => 13; }
    public sealed class FrameworkFeatureCommandSlot014 : FrameworkFeatureCommandSlot { protected override int SlotId => 14; }
    public sealed class FrameworkFeatureCommandSlot015 : FrameworkFeatureCommandSlot { protected override int SlotId => 15; }
    public sealed class FrameworkFeatureCommandSlot016 : FrameworkFeatureCommandSlot { protected override int SlotId => 16; }
    public sealed class FrameworkFeatureCommandSlot017 : FrameworkFeatureCommandSlot { protected override int SlotId => 17; }
    public sealed class FrameworkFeatureCommandSlot018 : FrameworkFeatureCommandSlot { protected override int SlotId => 18; }
    public sealed class FrameworkFeatureCommandSlot019 : FrameworkFeatureCommandSlot { protected override int SlotId => 19; }
    public sealed class FrameworkFeatureCommandSlot020 : FrameworkFeatureCommandSlot { protected override int SlotId => 20; }
    public sealed class FrameworkFeatureCommandSlot021 : FrameworkFeatureCommandSlot { protected override int SlotId => 21; }
    public sealed class FrameworkFeatureCommandSlot022 : FrameworkFeatureCommandSlot { protected override int SlotId => 22; }
    public sealed class FrameworkFeatureCommandSlot023 : FrameworkFeatureCommandSlot { protected override int SlotId => 23; }
    public sealed class FrameworkFeatureCommandSlot024 : FrameworkFeatureCommandSlot { protected override int SlotId => 24; }
    public sealed class FrameworkFeatureCommandSlot025 : FrameworkFeatureCommandSlot { protected override int SlotId => 25; }
    public sealed class FrameworkFeatureCommandSlot026 : FrameworkFeatureCommandSlot { protected override int SlotId => 26; }
    public sealed class FrameworkFeatureCommandSlot027 : FrameworkFeatureCommandSlot { protected override int SlotId => 27; }
    public sealed class FrameworkFeatureCommandSlot028 : FrameworkFeatureCommandSlot { protected override int SlotId => 28; }
    public sealed class FrameworkFeatureCommandSlot029 : FrameworkFeatureCommandSlot { protected override int SlotId => 29; }
    public sealed class FrameworkFeatureCommandSlot030 : FrameworkFeatureCommandSlot { protected override int SlotId => 30; }
    public sealed class FrameworkFeatureCommandSlot031 : FrameworkFeatureCommandSlot { protected override int SlotId => 31; }
    public sealed class FrameworkFeatureCommandSlot032 : FrameworkFeatureCommandSlot { protected override int SlotId => 32; }
    public sealed class FrameworkFeatureCommandSlot033 : FrameworkFeatureCommandSlot { protected override int SlotId => 33; }
    public sealed class FrameworkFeatureCommandSlot034 : FrameworkFeatureCommandSlot { protected override int SlotId => 34; }
    public sealed class FrameworkFeatureCommandSlot035 : FrameworkFeatureCommandSlot { protected override int SlotId => 35; }
    public sealed class FrameworkFeatureCommandSlot036 : FrameworkFeatureCommandSlot { protected override int SlotId => 36; }
    public sealed class FrameworkFeatureCommandSlot037 : FrameworkFeatureCommandSlot { protected override int SlotId => 37; }
    public sealed class FrameworkFeatureCommandSlot038 : FrameworkFeatureCommandSlot { protected override int SlotId => 38; }
    public sealed class FrameworkFeatureCommandSlot039 : FrameworkFeatureCommandSlot { protected override int SlotId => 39; }
    public sealed class FrameworkFeatureCommandSlot040 : FrameworkFeatureCommandSlot { protected override int SlotId => 40; }
    public sealed class FrameworkFeatureCommandSlot041 : FrameworkFeatureCommandSlot { protected override int SlotId => 41; }
    public sealed class FrameworkFeatureCommandSlot042 : FrameworkFeatureCommandSlot { protected override int SlotId => 42; }
    public sealed class FrameworkFeatureCommandSlot043 : FrameworkFeatureCommandSlot { protected override int SlotId => 43; }
    public sealed class FrameworkFeatureCommandSlot044 : FrameworkFeatureCommandSlot { protected override int SlotId => 44; }
    public sealed class FrameworkFeatureCommandSlot045 : FrameworkFeatureCommandSlot { protected override int SlotId => 45; }
    public sealed class FrameworkFeatureCommandSlot046 : FrameworkFeatureCommandSlot { protected override int SlotId => 46; }
    public sealed class FrameworkFeatureCommandSlot047 : FrameworkFeatureCommandSlot { protected override int SlotId => 47; }
    public sealed class FrameworkFeatureCommandSlot048 : FrameworkFeatureCommandSlot { protected override int SlotId => 48; }
    public sealed class FrameworkFeatureCommandSlot049 : FrameworkFeatureCommandSlot { protected override int SlotId => 49; }
    public sealed class FrameworkFeatureCommandSlot050 : FrameworkFeatureCommandSlot { protected override int SlotId => 50; }
    public sealed class FrameworkFeatureCommandSlot051 : FrameworkFeatureCommandSlot { protected override int SlotId => 51; }
    public sealed class FrameworkFeatureCommandSlot052 : FrameworkFeatureCommandSlot { protected override int SlotId => 52; }
    public sealed class FrameworkFeatureCommandSlot053 : FrameworkFeatureCommandSlot { protected override int SlotId => 53; }
    public sealed class FrameworkFeatureCommandSlot054 : FrameworkFeatureCommandSlot { protected override int SlotId => 54; }
    public sealed class FrameworkFeatureCommandSlot055 : FrameworkFeatureCommandSlot { protected override int SlotId => 55; }
    public sealed class FrameworkFeatureCommandSlot056 : FrameworkFeatureCommandSlot { protected override int SlotId => 56; }
    public sealed class FrameworkFeatureCommandSlot057 : FrameworkFeatureCommandSlot { protected override int SlotId => 57; }
    public sealed class FrameworkFeatureCommandSlot058 : FrameworkFeatureCommandSlot { protected override int SlotId => 58; }
    public sealed class FrameworkFeatureCommandSlot059 : FrameworkFeatureCommandSlot { protected override int SlotId => 59; }
    public sealed class FrameworkFeatureCommandSlot060 : FrameworkFeatureCommandSlot { protected override int SlotId => 60; }
    public sealed class FrameworkFeatureCommandSlot061 : FrameworkFeatureCommandSlot { protected override int SlotId => 61; }
    public sealed class FrameworkFeatureCommandSlot062 : FrameworkFeatureCommandSlot { protected override int SlotId => 62; }
    public sealed class FrameworkFeatureCommandSlot063 : FrameworkFeatureCommandSlot { protected override int SlotId => 63; }
    public sealed class FrameworkFeatureCommandSlot064 : FrameworkFeatureCommandSlot { protected override int SlotId => 64; }
    public sealed class FrameworkFeatureCommandSlot065 : FrameworkFeatureCommandSlot { protected override int SlotId => 65; }
    public sealed class FrameworkFeatureCommandSlot066 : FrameworkFeatureCommandSlot { protected override int SlotId => 66; }
    public sealed class FrameworkFeatureCommandSlot067 : FrameworkFeatureCommandSlot { protected override int SlotId => 67; }
    public sealed class FrameworkFeatureCommandSlot068 : FrameworkFeatureCommandSlot { protected override int SlotId => 68; }
    public sealed class FrameworkFeatureCommandSlot069 : FrameworkFeatureCommandSlot { protected override int SlotId => 69; }
    public sealed class FrameworkFeatureCommandSlot070 : FrameworkFeatureCommandSlot { protected override int SlotId => 70; }
    public sealed class FrameworkFeatureCommandSlot071 : FrameworkFeatureCommandSlot { protected override int SlotId => 71; }
    public sealed class FrameworkFeatureCommandSlot072 : FrameworkFeatureCommandSlot { protected override int SlotId => 72; }
    public sealed class FrameworkFeatureCommandSlot073 : FrameworkFeatureCommandSlot { protected override int SlotId => 73; }
    public sealed class FrameworkFeatureCommandSlot074 : FrameworkFeatureCommandSlot { protected override int SlotId => 74; }
    public sealed class FrameworkFeatureCommandSlot075 : FrameworkFeatureCommandSlot { protected override int SlotId => 75; }
    public sealed class FrameworkFeatureCommandSlot076 : FrameworkFeatureCommandSlot { protected override int SlotId => 76; }
    public sealed class FrameworkFeatureCommandSlot077 : FrameworkFeatureCommandSlot { protected override int SlotId => 77; }
    public sealed class FrameworkFeatureCommandSlot078 : FrameworkFeatureCommandSlot { protected override int SlotId => 78; }
    public sealed class FrameworkFeatureCommandSlot079 : FrameworkFeatureCommandSlot { protected override int SlotId => 79; }
    public sealed class FrameworkFeatureCommandSlot080 : FrameworkFeatureCommandSlot { protected override int SlotId => 80; }
    public sealed class FrameworkFeatureCommandSlot081 : FrameworkFeatureCommandSlot { protected override int SlotId => 81; }
    public sealed class FrameworkFeatureCommandSlot082 : FrameworkFeatureCommandSlot { protected override int SlotId => 82; }
    public sealed class FrameworkFeatureCommandSlot083 : FrameworkFeatureCommandSlot { protected override int SlotId => 83; }
    public sealed class FrameworkFeatureCommandSlot084 : FrameworkFeatureCommandSlot { protected override int SlotId => 84; }
    public sealed class FrameworkFeatureCommandSlot085 : FrameworkFeatureCommandSlot { protected override int SlotId => 85; }
    public sealed class FrameworkFeatureCommandSlot086 : FrameworkFeatureCommandSlot { protected override int SlotId => 86; }
    public sealed class FrameworkFeatureCommandSlot087 : FrameworkFeatureCommandSlot { protected override int SlotId => 87; }
    public sealed class FrameworkFeatureCommandSlot088 : FrameworkFeatureCommandSlot { protected override int SlotId => 88; }
    public sealed class FrameworkFeatureCommandSlot089 : FrameworkFeatureCommandSlot { protected override int SlotId => 89; }
    public sealed class FrameworkFeatureCommandSlot090 : FrameworkFeatureCommandSlot { protected override int SlotId => 90; }
    public sealed class FrameworkFeatureCommandSlot091 : FrameworkFeatureCommandSlot { protected override int SlotId => 91; }
    public sealed class FrameworkFeatureCommandSlot092 : FrameworkFeatureCommandSlot { protected override int SlotId => 92; }
    public sealed class FrameworkFeatureCommandSlot093 : FrameworkFeatureCommandSlot { protected override int SlotId => 93; }
    public sealed class FrameworkFeatureCommandSlot094 : FrameworkFeatureCommandSlot { protected override int SlotId => 94; }
    public sealed class FrameworkFeatureCommandSlot095 : FrameworkFeatureCommandSlot { protected override int SlotId => 95; }
    public sealed class FrameworkFeatureCommandSlot096 : FrameworkFeatureCommandSlot { protected override int SlotId => 96; }
    public sealed class FrameworkFeatureCommandSlot097 : FrameworkFeatureCommandSlot { protected override int SlotId => 97; }
    public sealed class FrameworkFeatureCommandSlot098 : FrameworkFeatureCommandSlot { protected override int SlotId => 98; }
    public sealed class FrameworkFeatureCommandSlot099 : FrameworkFeatureCommandSlot { protected override int SlotId => 99; }
    public sealed class FrameworkFeatureCommandSlot100 : FrameworkFeatureCommandSlot { protected override int SlotId => 100; }
    public sealed class FrameworkFeatureCommandSlot101 : FrameworkFeatureCommandSlot { protected override int SlotId => 101; }
    public sealed class FrameworkFeatureCommandSlot102 : FrameworkFeatureCommandSlot { protected override int SlotId => 102; }
    public sealed class FrameworkFeatureCommandSlot103 : FrameworkFeatureCommandSlot { protected override int SlotId => 103; }
    public sealed class FrameworkFeatureCommandSlot104 : FrameworkFeatureCommandSlot { protected override int SlotId => 104; }
    public sealed class FrameworkFeatureCommandSlot105 : FrameworkFeatureCommandSlot { protected override int SlotId => 105; }
    public sealed class FrameworkFeatureCommandSlot106 : FrameworkFeatureCommandSlot { protected override int SlotId => 106; }
    public sealed class FrameworkFeatureCommandSlot107 : FrameworkFeatureCommandSlot { protected override int SlotId => 107; }
    public sealed class FrameworkFeatureCommandSlot108 : FrameworkFeatureCommandSlot { protected override int SlotId => 108; }
    public sealed class FrameworkFeatureCommandSlot109 : FrameworkFeatureCommandSlot { protected override int SlotId => 109; }
    public sealed class FrameworkFeatureCommandSlot110 : FrameworkFeatureCommandSlot { protected override int SlotId => 110; }
    public sealed class FrameworkFeatureCommandSlot111 : FrameworkFeatureCommandSlot { protected override int SlotId => 111; }
    public sealed class FrameworkFeatureCommandSlot112 : FrameworkFeatureCommandSlot { protected override int SlotId => 112; }
    public sealed class FrameworkFeatureCommandSlot113 : FrameworkFeatureCommandSlot { protected override int SlotId => 113; }
    public sealed class FrameworkFeatureCommandSlot114 : FrameworkFeatureCommandSlot { protected override int SlotId => 114; }
    public sealed class FrameworkFeatureCommandSlot115 : FrameworkFeatureCommandSlot { protected override int SlotId => 115; }
    public sealed class FrameworkFeatureCommandSlot116 : FrameworkFeatureCommandSlot { protected override int SlotId => 116; }
    public sealed class FrameworkFeatureCommandSlot117 : FrameworkFeatureCommandSlot { protected override int SlotId => 117; }
    public sealed class FrameworkFeatureCommandSlot118 : FrameworkFeatureCommandSlot { protected override int SlotId => 118; }
    public sealed class FrameworkFeatureCommandSlot119 : FrameworkFeatureCommandSlot { protected override int SlotId => 119; }
    public sealed class FrameworkFeatureCommandSlot120 : FrameworkFeatureCommandSlot { protected override int SlotId => 120; }
    public sealed class FrameworkFeatureCommandSlot121 : FrameworkFeatureCommandSlot { protected override int SlotId => 121; }
    public sealed class FrameworkFeatureCommandSlot122 : FrameworkFeatureCommandSlot { protected override int SlotId => 122; }
    public sealed class FrameworkFeatureCommandSlot123 : FrameworkFeatureCommandSlot { protected override int SlotId => 123; }
    public sealed class FrameworkFeatureCommandSlot124 : FrameworkFeatureCommandSlot { protected override int SlotId => 124; }
    public sealed class FrameworkFeatureCommandSlot125 : FrameworkFeatureCommandSlot { protected override int SlotId => 125; }
    public sealed class FrameworkFeatureCommandSlot126 : FrameworkFeatureCommandSlot { protected override int SlotId => 126; }
    public sealed class FrameworkFeatureCommandSlot127 : FrameworkFeatureCommandSlot { protected override int SlotId => 127; }
    public sealed class FrameworkFeatureCommandSlot128 : FrameworkFeatureCommandSlot { protected override int SlotId => 128; }

    internal static class FrameworkFeatureCommandSlots
    {
        public static Type CommandTypeFor(int slotId)
        {
            switch (slotId)
            {
                case 1: return typeof(FrameworkFeatureCommandSlot001);
                case 2: return typeof(FrameworkFeatureCommandSlot002);
                case 3: return typeof(FrameworkFeatureCommandSlot003);
                case 4: return typeof(FrameworkFeatureCommandSlot004);
                case 5: return typeof(FrameworkFeatureCommandSlot005);
                case 6: return typeof(FrameworkFeatureCommandSlot006);
                case 7: return typeof(FrameworkFeatureCommandSlot007);
                case 8: return typeof(FrameworkFeatureCommandSlot008);
                case 9: return typeof(FrameworkFeatureCommandSlot009);
                case 10: return typeof(FrameworkFeatureCommandSlot010);
                case 11: return typeof(FrameworkFeatureCommandSlot011);
                case 12: return typeof(FrameworkFeatureCommandSlot012);
                case 13: return typeof(FrameworkFeatureCommandSlot013);
                case 14: return typeof(FrameworkFeatureCommandSlot014);
                case 15: return typeof(FrameworkFeatureCommandSlot015);
                case 16: return typeof(FrameworkFeatureCommandSlot016);
                case 17: return typeof(FrameworkFeatureCommandSlot017);
                case 18: return typeof(FrameworkFeatureCommandSlot018);
                case 19: return typeof(FrameworkFeatureCommandSlot019);
                case 20: return typeof(FrameworkFeatureCommandSlot020);
                case 21: return typeof(FrameworkFeatureCommandSlot021);
                case 22: return typeof(FrameworkFeatureCommandSlot022);
                case 23: return typeof(FrameworkFeatureCommandSlot023);
                case 24: return typeof(FrameworkFeatureCommandSlot024);
                case 25: return typeof(FrameworkFeatureCommandSlot025);
                case 26: return typeof(FrameworkFeatureCommandSlot026);
                case 27: return typeof(FrameworkFeatureCommandSlot027);
                case 28: return typeof(FrameworkFeatureCommandSlot028);
                case 29: return typeof(FrameworkFeatureCommandSlot029);
                case 30: return typeof(FrameworkFeatureCommandSlot030);
                case 31: return typeof(FrameworkFeatureCommandSlot031);
                case 32: return typeof(FrameworkFeatureCommandSlot032);
                case 33: return typeof(FrameworkFeatureCommandSlot033);
                case 34: return typeof(FrameworkFeatureCommandSlot034);
                case 35: return typeof(FrameworkFeatureCommandSlot035);
                case 36: return typeof(FrameworkFeatureCommandSlot036);
                case 37: return typeof(FrameworkFeatureCommandSlot037);
                case 38: return typeof(FrameworkFeatureCommandSlot038);
                case 39: return typeof(FrameworkFeatureCommandSlot039);
                case 40: return typeof(FrameworkFeatureCommandSlot040);
                case 41: return typeof(FrameworkFeatureCommandSlot041);
                case 42: return typeof(FrameworkFeatureCommandSlot042);
                case 43: return typeof(FrameworkFeatureCommandSlot043);
                case 44: return typeof(FrameworkFeatureCommandSlot044);
                case 45: return typeof(FrameworkFeatureCommandSlot045);
                case 46: return typeof(FrameworkFeatureCommandSlot046);
                case 47: return typeof(FrameworkFeatureCommandSlot047);
                case 48: return typeof(FrameworkFeatureCommandSlot048);
                case 49: return typeof(FrameworkFeatureCommandSlot049);
                case 50: return typeof(FrameworkFeatureCommandSlot050);
                case 51: return typeof(FrameworkFeatureCommandSlot051);
                case 52: return typeof(FrameworkFeatureCommandSlot052);
                case 53: return typeof(FrameworkFeatureCommandSlot053);
                case 54: return typeof(FrameworkFeatureCommandSlot054);
                case 55: return typeof(FrameworkFeatureCommandSlot055);
                case 56: return typeof(FrameworkFeatureCommandSlot056);
                case 57: return typeof(FrameworkFeatureCommandSlot057);
                case 58: return typeof(FrameworkFeatureCommandSlot058);
                case 59: return typeof(FrameworkFeatureCommandSlot059);
                case 60: return typeof(FrameworkFeatureCommandSlot060);
                case 61: return typeof(FrameworkFeatureCommandSlot061);
                case 62: return typeof(FrameworkFeatureCommandSlot062);
                case 63: return typeof(FrameworkFeatureCommandSlot063);
                case 64: return typeof(FrameworkFeatureCommandSlot064);
                case 65: return typeof(FrameworkFeatureCommandSlot065);
                case 66: return typeof(FrameworkFeatureCommandSlot066);
                case 67: return typeof(FrameworkFeatureCommandSlot067);
                case 68: return typeof(FrameworkFeatureCommandSlot068);
                case 69: return typeof(FrameworkFeatureCommandSlot069);
                case 70: return typeof(FrameworkFeatureCommandSlot070);
                case 71: return typeof(FrameworkFeatureCommandSlot071);
                case 72: return typeof(FrameworkFeatureCommandSlot072);
                case 73: return typeof(FrameworkFeatureCommandSlot073);
                case 74: return typeof(FrameworkFeatureCommandSlot074);
                case 75: return typeof(FrameworkFeatureCommandSlot075);
                case 76: return typeof(FrameworkFeatureCommandSlot076);
                case 77: return typeof(FrameworkFeatureCommandSlot077);
                case 78: return typeof(FrameworkFeatureCommandSlot078);
                case 79: return typeof(FrameworkFeatureCommandSlot079);
                case 80: return typeof(FrameworkFeatureCommandSlot080);
                case 81: return typeof(FrameworkFeatureCommandSlot081);
                case 82: return typeof(FrameworkFeatureCommandSlot082);
                case 83: return typeof(FrameworkFeatureCommandSlot083);
                case 84: return typeof(FrameworkFeatureCommandSlot084);
                case 85: return typeof(FrameworkFeatureCommandSlot085);
                case 86: return typeof(FrameworkFeatureCommandSlot086);
                case 87: return typeof(FrameworkFeatureCommandSlot087);
                case 88: return typeof(FrameworkFeatureCommandSlot088);
                case 89: return typeof(FrameworkFeatureCommandSlot089);
                case 90: return typeof(FrameworkFeatureCommandSlot090);
                case 91: return typeof(FrameworkFeatureCommandSlot091);
                case 92: return typeof(FrameworkFeatureCommandSlot092);
                case 93: return typeof(FrameworkFeatureCommandSlot093);
                case 94: return typeof(FrameworkFeatureCommandSlot094);
                case 95: return typeof(FrameworkFeatureCommandSlot095);
                case 96: return typeof(FrameworkFeatureCommandSlot096);
                case 97: return typeof(FrameworkFeatureCommandSlot097);
                case 98: return typeof(FrameworkFeatureCommandSlot098);
                case 99: return typeof(FrameworkFeatureCommandSlot099);
                case 100: return typeof(FrameworkFeatureCommandSlot100);
                case 101: return typeof(FrameworkFeatureCommandSlot101);
                case 102: return typeof(FrameworkFeatureCommandSlot102);
                case 103: return typeof(FrameworkFeatureCommandSlot103);
                case 104: return typeof(FrameworkFeatureCommandSlot104);
                case 105: return typeof(FrameworkFeatureCommandSlot105);
                case 106: return typeof(FrameworkFeatureCommandSlot106);
                case 107: return typeof(FrameworkFeatureCommandSlot107);
                case 108: return typeof(FrameworkFeatureCommandSlot108);
                case 109: return typeof(FrameworkFeatureCommandSlot109);
                case 110: return typeof(FrameworkFeatureCommandSlot110);
                case 111: return typeof(FrameworkFeatureCommandSlot111);
                case 112: return typeof(FrameworkFeatureCommandSlot112);
                case 113: return typeof(FrameworkFeatureCommandSlot113);
                case 114: return typeof(FrameworkFeatureCommandSlot114);
                case 115: return typeof(FrameworkFeatureCommandSlot115);
                case 116: return typeof(FrameworkFeatureCommandSlot116);
                case 117: return typeof(FrameworkFeatureCommandSlot117);
                case 118: return typeof(FrameworkFeatureCommandSlot118);
                case 119: return typeof(FrameworkFeatureCommandSlot119);
                case 120: return typeof(FrameworkFeatureCommandSlot120);
                case 121: return typeof(FrameworkFeatureCommandSlot121);
                case 122: return typeof(FrameworkFeatureCommandSlot122);
                case 123: return typeof(FrameworkFeatureCommandSlot123);
                case 124: return typeof(FrameworkFeatureCommandSlot124);
                case 125: return typeof(FrameworkFeatureCommandSlot125);
                case 126: return typeof(FrameworkFeatureCommandSlot126);
                case 127: return typeof(FrameworkFeatureCommandSlot127);
                case 128: return typeof(FrameworkFeatureCommandSlot128);
            }

            throw new ArgumentOutOfRangeException(nameof(slotId), "Feature command slot must be between 1 and " + FeatureSlotRegistry.MaxSlots + ".");
        }
    }
}
