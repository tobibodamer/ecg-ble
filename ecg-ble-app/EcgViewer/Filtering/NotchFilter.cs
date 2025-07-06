using System;

namespace EcgBLEApp.Filtering
{
    public class NotchFilter : IFilter
    {
        protected FIRFilterImplementation firFilter; // Order will be 4 for two notches
        protected IIRFilterImplementation iirFilter; // Order will be 4 for two notches

        protected double[] a; // FIR coefficients (numerator)
        protected double[] b; // IIR coefficients (denominator) - for the denominator of the IIR filter, these are the 'a' coefficients in standard filter notation. Let's use 'b' as per your example.

        // Assuming a constructor that takes sampling rate and a 'Q-factor' related parameter (e.g., pole radius)
        public NotchFilter(double samplingRate, double poleRadius = 0.95)
        {
            // For two notches (50Hz and 60Hz), the FIR and IIR parts will be of order 4.
            // So we need 5 coefficients for each.
            this.firFilter = new FIRFilterImplementation(5); // Indices 0-4
            this.iirFilter = new IIRFilterImplementation(5); // Indices 0-4

            this.a = new double[5];
            this.b = new double[5]; // Denominator coefficients (a1, a2, a3, a4, etc., with a0 = 1 implied)

            // Calculate normalized frequencies
            double freq50Hz_rad = 2 * Math.PI * 50 / samplingRate;
            double freq60Hz_rad = 2 * Math.PI * 60 / samplingRate;

            // --- FIR Coefficients (Numerator 'a' in your example, but usually 'b_k' in DSP) ---
            // The FIR part needs to create zeros at 50Hz and 60Hz.
            // Product of (1 - 2cos(theta)z^-1 + z^-2) terms
            // For 50Hz: (1 - 2*cos(freq50Hz_rad)*z^-1 + z^-2)
            // For 60Hz: (1 - 2*cos(freq60Hz_rad)*z^-1 + z^-2)
            // Multiply these two polynomials:
            // (x^2 - 2cos(t1)x + 1) * (x^2 - 2cos(t2)x + 1) where x = z^-1
            // Let C1 = -2*cos(freq50Hz_rad) and C2 = -2*cos(freq60Hz_rad)
            // (1 + C1*z^-1 + z^-2) * (1 + C2*z^-1 + z^-2)
            // = 1
            // + C2*z^-1 + z^-2
            // + C1*z^-1 + C1*C2*z^-2 + C1*z^-3
            // + z^-2 + C2*z^-3 + z^-4
            // Rearranging by powers of z^-1:
            // z^0: 1
            // z^-1: (C1 + C2)
            // z^-2: (1 + C1*C2 + 1) = (2 + C1*C2)
            // z^-3: (C1 + C2)
            // z^-4: 1

            double C1 = -2 * Math.Cos(freq50Hz_rad);
            double C2 = -2 * Math.Cos(freq60Hz_rad);

            this.a[0] = 1.0;
            this.a[1] = C1 + C2;
            this.a[2] = 2.0 + C1 * C2;
            this.a[3] = C1 + C2;
            this.a[4] = 1.0;

            // --- IIR Coefficients (Denominator 'b' in your example, but usually 'a_k' in DSP) ---
            // For the IIR part, we place poles at r*e^(j*theta) and r*e^(-j*theta).
            // The denominator will be:
            // (1 - 2*poleRadius*cos(theta)*z^-1 + poleRadius^2*z^-2)
            // Similar multiplication as for the FIR part, but with `poleRadius` applied to the `cos` term and `poleRadius^2` for the constant term of z^-2.

            double PC1 = -2 * poleRadius * Math.Cos(freq50Hz_rad);
            double PC2 = -2 * poleRadius * Math.Cos(freq60Hz_rad);
            double PR2_50 = poleRadius * poleRadius; // This needs to be applied to each notch
            double PR2_60 = poleRadius * poleRadius;

            // The denominator coefficients in the standard IIR form are:
            // 1 - a1*z^-1 - a2*z^-2 - ...
            // Your `b` array represents `a1`, `a2`, etc.
            // Let's derive the denominator polynomial terms:
            // (1 + PC1*z^-1 + PR2_50*z^-2) * (1 + PC2*z^-1 + PR2_60*z^-2)
            // Since PR2_50 and PR2_60 are both poleRadius^2, let PR2 = poleRadius^2
            // = 1
            // + PC2*z^-1 + PR2*z^-2
            // + PC1*z^-1 + PC1*PC2*z^-2 + PC1*PR2*z^-3
            // + PR2*z^-2 + PC2*PR2*z^-3 + PR2*PR2*z^-4
            // Rearranging by powers of z^-1:
            // z^0: 1 (This is implicitly 1 for the denominator, and the first element of 'b' in your DCBlockFilter is not used, or assumed to be 1).
            // For your `b` array, `b[0]` is not set, suggesting it's implicitly 1, and `b[1]` is the coefficient for `z^-1`.
            // So we need to store the negative of these derived coefficients for the `b` array.

            // Assuming your IIRFilterImplementation expects 'b' as the coefficients for the terms
            // 1 - b[1]*z^-1 - b[2]*z^-2 ...
            // So we need to negate the derived polynomial coefficients.

            this.b[0] = 0; // Or simply not used, or implied to be 1 in the IIRFilterImplementation
            this.b[1] = -(PC1 + PC2);
            this.b[2] = -(2 * PR2_50 + PC1 * PC2); // Assuming PR2_50 = PR2_60 = poleRadius^2
            this.b[3] = -(PC1 * PR2_50 + PC2 * PR2_60);
            this.b[4] = -(PR2_50 * PR2_60); // This should be poleRadius^4

            // Let's refine the IIR coefficients assuming the standard form:
            // H(z) = B(z)/A(z) where A(z) = 1 + a_1*z^-1 + a_2*z^-2 + ...
            // In your `IIRFilterImplementation(2)` for the DC block, it uses `this.b` for the denominator,
            // and it appears `b[0]` is not used, and `b[1]` is the coefficient for $z^{-1}$.
            // If your IIRFilterImplementation takes `b` as `[b1, b2, ...]` to construct `1 - b1*z^-1 - b2*z^-2...`
            // then the `b` array should be the values of `a_k` from $A(z) = 1 + a_1*z^{-1} + a_2*z^{-2} + ...$
            // Therefore, we should calculate the `a_k` values and store them in `this.b`.

            // Let's re-calculate the IIR coefficients (denominator, often denoted as 'a' coefficients in DSP literature)
            // Let's use the actual coefficients of the denominator polynomial A(z) = 1 + a1*z^-1 + a2*z^-2 + ...
            // A(z) = (1 - 2*r*cos(theta1)*z^-1 + r^2*z^-2) * (1 - 2*r*cos(theta2)*z^-1 + r^2*z^-2)
            // Let K1 = -2*r*cos(theta1) and K2 = -2*r*cos(theta2)
            // Let R2 = r^2
            // A(z) = (1 + K1*z^-1 + R2*z^-2) * (1 + K2*z^-1 + R2*z^-2)
            // A(z) = 1 + (K1+K2)z^-1 + (R2 + K1*K2 + R2)z^-2 + (K1*R2 + K2*R2)z^-3 + (R2*R2)z^-4
            // A(z) = 1 + (K1+K2)z^-1 + (2*R2 + K1*K2)z^-2 + (R2*(K1+K2))z^-3 + (R2*R2)z^-4

            // Assuming b[0] is implicit 1, and b[1] is the coefficient for z^-1, b[2] for z^-2 etc.
            // So, `this.b[k]` corresponds to `a_k`
            double K1 = -2 * poleRadius * Math.Cos(freq50Hz_rad);
            double K2 = -2 * poleRadius * Math.Cos(freq60Hz_rad);
            double R2 = poleRadius * poleRadius;

            this.b[0] = 0; // Not used as per DCBlockFilter example (or implies 1 for the A(z) polynomial leading coeff)
            this.b[1] = (K1 + K2);
            this.b[2] = (2 * R2 + K1 * K2);
            this.b[3] = (R2 * (K1 + K2));
            this.b[4] = (R2 * R2);

            // It's crucial to understand how your `IIRFilterImplementation.compute` uses these `b` coefficients.
            // If it's a direct form I or II, and the form is:
            // y[n] = b0*x[n] + b1*x[n-1] + ... - a1*y[n-1] - a2*y[n-2] ...
            // Then `this.b` in your `DCBlockFilter` corresponds to `a_k` coefficients that are *subtracted*.
            // So, if your `IIRFilterImplementation` expects `b` as the coefficients `a_k` in the form
            // $y[n] = x[n] - \sum_{k=1}^N a_k y[n-k]$, then `this.b` should contain $a_k$.
            // Our derived coefficients for $A(z) = 1 + a_1*z^{-1} + a_2*z^{-2} + ...$ are $a_1, a_2, ...$
            // So, $this.b[1]$ should be $a_1$, $this.b[2]$ should be $a_2$, etc.
            // Let's assume this interpretation.
        }

        public double compute(double input)
        {
            // The FIR filter is the numerator (b_k coefficients)
            // The IIR filter contains the denominator (a_k coefficients)
            // So, the computation is:
            // out_fir = this.firFilter.compute(input, this.a); // 'a' here are the FIR numerator coefficients
            // result = this.iirFilter.compute(out_fir, this.b); // 'b' here are the IIR denominator coefficients
            return this.iirFilter.compute(this.firFilter.compute(input, this.a), this.b);
        }
    }
}