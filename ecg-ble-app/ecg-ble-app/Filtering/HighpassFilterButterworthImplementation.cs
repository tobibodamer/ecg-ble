namespace ecg_ble_app.Views
{
    public class HighpassFilterButterworthImplementation
    {
        protected HighpassFilterButterworthSection[] section;

        public HighpassFilterButterworthImplementation
        (double cutoffFrequencyHz, int numSections, double Fs)
        {
            this.section = new HighpassFilterButterworthSection[numSections];
            for (int i = 0; i < numSections; i++)
            {
                this.section[i] = new HighpassFilterButterworthSection
                (cutoffFrequencyHz, i + 1, numSections * 2, Fs);
            }
        }
        public double compute(double input)
        {
            double output = input;
            for (int i = 0; i < this.section.Length; i++)
            {
                output = this.section[i].compute(output);
            }
            return output;
        }
    }
}