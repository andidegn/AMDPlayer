using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AMD.util.math.QuadraticSliderTest {
    /// <summary>
    /// quadratic equation Ax^2 + Bx + C = 0
    /// </summary>
    public class QuadraticEquation {
        // ******************************************************************
        // A quadratic equation always has two roots
        // ******************************************************************
        private struct Root {
            public double R1_Re;
            public double R1_Im;
            public double R2_Re;
            public double R2_Im;
        }
        private Root MyRoot;
        // ******************************************************************
        // Keep the coefficient private we get and set them via properties
        // ******************************************************************
        private double cA = 0.0;
        private double cB = 0.0;
        private double cC = 0.0;
        // ******************************************************************
        // Constructors
        // ******************************************************************
        public QuadraticEquation() { }
        public QuadraticEquation(double A, double B, double C) {
            cA = A;
            cB = B;
            cC = C;
        }
        // ******************************************************************
        // Properties
        // ******************************************************************
        public double Acoefficient { get { return cA; } set { cA = value; } }
        public double Bcoefficient { get { return cB; } set { cB = value; } }
        public double Ccoefficient { get { return cC; } set { cC = value; } }
        public double Root1RealPart { get { return MyRoot.R1_Re; } }
        public double Root1ImagPart { get { return MyRoot.R1_Im; } }
        public double Root2RealPart { get { return MyRoot.R2_Re; } }
        public double Root2ImagPart { get { return MyRoot.R2_Im; } }
        // ******************************************************************
        // Return the discriminant b*b-4*a*c
        // ******************************************************************
        public double Discriminant() {
            return cB * cB - 4 * cA * cC;
        }
        // ******************************************************************
        // Solve the equation and fill the roots struct with the results
        // ******************************************************************
        public void Solve() {
            if (cA == 0) {
                MyRoot.R1_Re = -cC / cB;
                MyRoot.R2_Re = MyRoot.R1_Re;
                return;
            }

            // Preliminary calculations to avoid numbercrunching overhead
            double D = Discriminant();
            double twoA = cA + cA;
            double B2A = -cB / twoA;
            if (D == 0) // roots are equal and real
            {
                MyRoot.R1_Re = MyRoot.R2_Re = B2A;
                MyRoot.R1_Im = MyRoot.R2_Im = 0.0;
            } else if (D > 0) // roots are distinct and real
            {
                MyRoot.R1_Re = B2A + Math.Sqrt(D) / twoA;
                MyRoot.R2_Re = B2A - Math.Sqrt(D) / twoA;
                MyRoot.R1_Im = 0.0;
                MyRoot.R2_Im = 0.0;
            } else if (D < 0) // no real roots, 2 complex conjugate roots
            {
                MyRoot.R1_Re = MyRoot.R2_Re = B2A;
                D = -D;
                MyRoot.R1_Im = Math.Sqrt(D) / twoA;
                MyRoot.R2_Im = -Math.Sqrt(D) / twoA;
            }
        }
    }
}
