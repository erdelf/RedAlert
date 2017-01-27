using RimWorld;
using System.Text;
using System.Linq;
using Verse.Sound;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Verse
{
    [StaticConstructorOnStartup]
    public class Building_AlertSwitch : Building
    {
        public static readonly Graphic GraphicOn;

        public static readonly Graphic GraphicOff;

        public static readonly SoundDef AlarmSound = SoundDef.Named("redAlertAlarm");

        public static Dictionary<String, string> GlowerList;

        private int flick;

        private CompFlickable flickableComp => GetComp<CompFlickable>();

        public override void SpawnSetup(Map map)
        {
            base.SpawnSetup(map);
            if (flick == 0)
            {
                flick = 1;
                flickableComp.DoFlick();
            }
        }

        public override bool TransmitsPowerNow
        {
            get
            {
                return flickableComp!=null?flickableComp.SwitchIsOn:false;
            }
        }

        public override Graphic Graphic
        {
            get
            {
                if (flickableComp == null)
                    return GraphicOff;
                if (this.flickableComp.SwitchIsOn)
                {
                    return Building_AlertSwitch.GraphicOn;
                }
                return Building_AlertSwitch.GraphicOff;
            }
        }

        static Building_AlertSwitch()
        {
            GraphicOn = GraphicDatabase.Get<Graphic_Single>("RedAlert");
            GraphicOff = GraphicDatabase.Get<Graphic_Single>("GreenAlert");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (GlowerList != null)
                Scribe_Collections.LookDictionary(ref GlowerList, "GlowerList", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
                flick = flickableComp.SwitchIsOn ? 2 : 1;
        }

        protected override void ReceiveCompSignal(string signal)
        {
            if ((signal == "FlickedOff" && flick==2) || (signal == "FlickedOn" && flick==1))
            {
                flick = signal.EqualsIgnoreCase("FlickedOff") ? 1 : 2;

                this.UpdatePowerGrid();
                this.UpdateLights();
            }
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            stringBuilder.Append("Alarm: " + (flickableComp.SwitchIsOn?"On":"Off").ToLower());
            return stringBuilder.ToString();
        }

        private void UpdatePowerGrid()
        {
            Map.powerNetManager.Notfiy_TransmitterTransmitsPowerNowChanged(base.PowerComp);
        }

        private void UpdateLights()
        {
            if (flickableComp != null)
                if (flickableComp.SwitchIsOn)
                {
                    TurnLightsRed();
                }
                else
                {
                    ResetLights();
                }
        }

        private void TurnLightsRed()
        {
            if (GlowerList == null)
                GlowerList = new Dictionary<String, string>();
            GlowerList.Clear();
            foreach (CompGlower glowerComp in
                from current in Map.listerThings.AllThings
                where current.TryGetComp<CompGlower>() != null && current.TryGetComp<CompPowerTrader>() != null
                select current.TryGetComp<CompGlower>())
            {
                CompProperties_Glower glower = glowerComp.Props;
                if (!GlowerList.ContainsKey(glowerComp.parent.ThingID))
                {
                    if (glower.glowColor.ToColor != ColorLibrary.Red)
                    {
                        GlowerList.Add(glowerComp.parent.ThingID, glower.glowColor.ToColor32.colorToHex());
                        glower.glowColor = ColorIntUtility.AsColorInt(ColorLibrary.Red);
                    }
                    Map.glowGrid.MarkGlowGridDirty(glowerComp.parent.Position);
                }
            }
            AlarmSound.PlayOneShotOnCamera();
        }

        private void ResetLights(bool clearList = true)
        {
            if (GlowerList != null)
            {
                foreach (IntVec3 intVec in Map.AllCells)
                    foreach (Thing thing in from cur in Map.thingGrid.ThingsListAtFast(intVec) where cur.TryGetComp<CompGlower>()!=null select cur)
                    {
                        if(GlowerList.ContainsKey(thing.ThingID))
                            thing.TryGetComp<CompGlower>().Props.glowColor = GlowerList[thing.ThingID].hexToColor().AsColorInt();
                        Map.glowGrid.MarkGlowGridDirty(thing.Position);
                    }
                if (clearList)
                    GlowerList.Clear();
            }
        }
    }

    public class String : IEquatable<String>, IExposable
    {
        public string s;

        public String()
        {
            s = "";
        }

        public String(string s = "")
        {
            this.s = s;
        }

        public override string ToString()
        {
            return s;
        }

        public bool Equals(String other)
        {
            // First two lines are just optimizations
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return s.Equals(other.s, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            // Again just optimization
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            // Actually check the type, should not throw exception from Equals override
            if (obj.GetType() != this.GetType()) return false;

            // Call the implementation from IEquatable
            return Equals((String)obj);
        }

        public override int GetHashCode()
        {
            // Constant because equals tests mutable member.
            // This will give poor hash performance, but will prevent bugs.
            return 0;
        }

        public void ExposeData()
        {
            Scribe_Values.LookValue(ref s, "Stringstring");
        }

        public static implicit operator String(string s)
        {
            return new String(s);
        }
    }

    public static class ExtensionMethods
    {
        //-------------String-----------------

        /*public static String toStringWrap(this string s)
        {
            return new String(s);
        }*/

        //-------------Color-----------------

        public static string colorToHex(this Color32 color)
        {
            string hex = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
            return hex;
        }

        public static Color32 hexToColor(this string hex)
        {
            hex = hex.Replace("0x", "");//in case the string is formatted 0xFFFFFF
            hex = hex.Replace("#", "");//in case the string is formatted #FFFFFF
            byte a = 255;//assume fully visible unless specified in hex
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            //Only use alpha if the string has enough characters
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return new Color32(r, g, b, a);
        }
    }
}

