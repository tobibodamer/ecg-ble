namespace EcgBLEApp.Filtering
{
    public class DCBlockFilter
    {
        protected FIRFilterImplementation firFilter = new FIRFilterImplementation(2);
        protected IIRFilterImplementation iirFilter = new IIRFilterImplementation(2);

        protected double[] a = new double[2];
        protected double[] b = new double[2];
        public DCBlockFilter()
        {
            this.a[0] = 1;
            this.a[1] = -1;
            this.b[1] = 0.995;
        }

        public double compute(double input)
        {
            // compute the result as the cascade of the fir and iir filters
            return this.iirFilter.compute
                   (this.firFilter.compute(input, this.a), this.b);
        }
    }
}