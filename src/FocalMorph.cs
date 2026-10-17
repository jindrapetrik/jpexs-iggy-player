using System;
using System.Collections.Generic;

namespace IggyViewer {
    // Compatibility adapter for the supplied 1.2.30 DLL. Stored definitions
    // remain untouched; only a copy of the current GDraw style is adjusted.
    internal sealed class FocalMorph {
        public IntPtr Definition;
        public int CharacterId;
        public readonly float[] Start=new float[4],End=new float[4];
        public float StartFocus,EndFocus;
        public readonly List<float> Ratios=new List<float>();

        public void AddRatio(float ratio) {
            if(float.IsNaN(ratio)||ratio<0||ratio>1)return;
            foreach(float value in Ratios)if(Math.Abs(value-ratio)<0.000001f)return;
            Ratios.Add(ratio);
        }
        public float Focus(float ratio) {return (StartFocus+(EndFocus-StartFocus)*ratio)*0.999f;}

        public bool TryTransform(float ratio,out double[] correction) {
            correction=null;
            double[] native=new double[4],desired=new double[4];
            for(int i=0;i<4;i++) {
                native[i]=Start[i]+((double)End[i]-Start[i])*ratio;
                // This layout stores focal morph end matrix linear values
                // in SWF units, while the start matrix uses pixel units.
                desired[i]=Start[i]+((double)End[i]/20-Start[i])*ratio;
            }
            double det=desired[0]*desired[3]-desired[1]*desired[2];
            if(double.IsNaN(det)||double.IsInfinity(det)||Math.Abs(det)<1e-20)return false;
            correction=new double[]{
                (desired[3]*native[0]-desired[1]*native[2])/det,
                (desired[3]*native[1]-desired[1]*native[3])/det,
                (desired[0]*native[2]-desired[2]*native[0])/det,
                (desired[0]*native[3]-desired[2]*native[1])/det
            };
            foreach(double value in correction)if(double.IsNaN(value)||double.IsInfinity(value))return false;
            return true;
        }

        public bool TryRatio(float u0,float u1,float v0,float v1,out float ratio) {
            ratio=0;
            if(Ratios.Count==1){ratio=Ratios[0];return true;}
            // Several instances can share the same fill and texture owner.
            // Match the matrix actually supplied for this batch, never use
            // the last instance visited in the display list.
            double scale=16384,det=((double)u0*v1-(double)u1*v0)*scale;
            if(Math.Abs(det)<1e-20||double.IsNaN(det)||double.IsInfinity(det))return false;
            double[] matrix={v1/det,-u1/det,-v0/det,u0/det};
            double best=double.PositiveInfinity;float candidate=0;
            foreach(float value in Ratios) {
                double error=0,norm=0;
                for(int i=0;i<4;i++) {
                    double expected=Start[i]+((double)End[i]-Start[i])*value;
                    error+=(matrix[i]-expected)*(matrix[i]-expected);norm+=expected*expected;
                }
                double relative=error/Math.Max(norm,1e-30);
                if(relative<best){best=relative;candidate=value;}
            }
            if(best>1e-8)return false;
            // When identical matrices correspond to different focal values,
            // GDraw carries insufficient information to choose the instance.
            foreach(float other in Ratios) {
                if(Math.Abs(other-candidate)<0.000001f)continue;
                double delta=0,norm=0;
                for(int i=0;i<4;i++) {
                    double a=Start[i]+((double)End[i]-Start[i])*candidate;
                    double b=Start[i]+((double)End[i]-Start[i])*other;
                    delta+=(a-b)*(a-b);norm+=a*a;
                }
                if(delta<=Math.Max(norm,1e-30)*1e-12)return false;
            }
            ratio=candidate;return true;
        }
    }
}
