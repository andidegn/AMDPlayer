using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using AMD.util.math.QuadraticSliderTest;

namespace AMD.custom_component.QuadraticSlider {
    public class QuadraticSlider : Slider {
        public static DependencyProperty CenterQuadraticValueProperty =
            DependencyProperty.Register("CenterQuadraticValue", typeof(double), typeof(QuadraticSlider),
                                        new FrameworkPropertyMetadata(0.0, CenterQuadraticValueChanged));
        public static DependencyProperty QuadraticValueProperty =
            DependencyProperty.Register("QuadraticValue", typeof(double), typeof(QuadraticSlider),
                                        new FrameworkPropertyMetadata(0.0, QuadraticValueChanged, CoerceQuadraticValue));

        public double CenterQuadraticValue {
            get { return (double)GetValue(CenterQuadraticValueProperty); }
            set { SetValue(CenterQuadraticValueProperty, value); }
        }
        public double QuadraticValue {
            get { return (double)GetValue(QuadraticValueProperty); }
            set { SetValue(QuadraticValueProperty, value); }
        }
        private static object CoerceQuadraticValue(DependencyObject obj, object baseValue) {
            return Math.Max(0, (double)baseValue);
        }
        private static void CenterQuadraticValueChanged(DependencyObject target, DependencyPropertyChangedEventArgs e) {
            QuadraticSlider quadraticSlider = target as QuadraticSlider;
            quadraticSlider.OnCenterQuadraticValueChanged();
        }
        private static void QuadraticValueChanged(DependencyObject target, DependencyPropertyChangedEventArgs e) {
            QuadraticSlider quadraticSlider = target as QuadraticSlider;
            quadraticSlider.OnQuadraticValueChanged((double)e.OldValue, (double)e.NewValue);
        }

        private double a, b, c;
        protected virtual void OnCenterQuadraticValueChanged() {
            FindEquationAndUpdateQuadraticValue();
        }
        protected override void OnMaximumChanged(double oldMaximum, double newMaximum) {
            base.OnMaximumChanged(oldMaximum, newMaximum);
            FindEquationAndUpdateQuadraticValue();
        }
        protected override void OnMinimumChanged(double oldMaximum, double newMaximum) {
            base.OnMinimumChanged(oldMaximum, newMaximum);
            FindEquationAndUpdateQuadraticValue();
        }
        protected override void OnValueChanged(double oldValue, double newValue) {
            base.OnValueChanged(oldValue, newValue);
            QuadraticValue = a * Math.Pow(Value, 2) + b * Value + c;
        }
        protected virtual void OnQuadraticValueChanged(double oldValue, double newValue) {
            if (QuadraticValue > 0) {
                QuadraticEquation quadraticEquation = new QuadraticEquation(a, b, c - QuadraticValue);
                quadraticEquation.Solve();
                double rootRealPart;
                double derivate = 2 * a * Value + b;
                if (derivate > 0)
                    rootRealPart = quadraticEquation.Root1RealPart;
                else
                    rootRealPart = quadraticEquation.Root2RealPart;

                Debug.WriteLine(derivate);
                if (Math.Abs(Value - rootRealPart) > 0.5) {
                    Value = rootRealPart;
                }
            }
        }
        private void FindEquationAndUpdateQuadraticValue() {
            FindEquation(Minimum, Minimum, ((Maximum - Minimum) / 2.0) + Minimum, CenterQuadraticValue, Maximum, Maximum);
            QuadraticValue = a * Math.Pow(Value, 2) + b * Value + c;
        }

        private void FindEquation(double X1, double Y1,
                                  double X2, double Y2,
                                  double X3, double Y3) {
            // A = [(Y2-Y1)(X1-X3) + (Y3-Y1)(X2-X1)]/[(X1-X3)(X2^2-X1^2) + (X2-X1)(X3^2-X1^2)]
            a = ((Y2 - Y1) * (X1 - X3) + (Y3 - Y1) * (X2 - X1)) /
                ((X1 - X3) * (Math.Pow(X2, 2) - Math.Pow(X1, 2)) + (X2 - X1) * (Math.Pow(X3, 2) - Math.Pow(X1, 2)));

            // B = [(Y2 - Y1) - A(X2^2 - X1^2)] / (X2-X1)
            b = ((Y2 - Y1) - a * (Math.Pow(X2, 2) - Math.Pow(X1, 2))) / (X2 - X1);

            // C = Y1 - AX1 ^ 2 - BX1
            c = Y1 - a * Math.Pow(X1, 2) - b * X1;
        }
    }
}


