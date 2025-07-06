using EcgBLEApp.Filtering;
using System;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using System.Windows.Navigation;

namespace EcgViewer.Filtering;

internal class FilteringUtils
{
    public static TransformBlock<ushort, double> ConvertSampleToMillivolts(int ampGain = 1300)
    {
        return new TransformBlock<ushort, double>(sample =>
        {
            return ((sample - 512) * 3.3) / (1024 * ampGain) * 1000;
        });
    }

    public static IPropagatorBlock<ushort, int> HeartRateDetection(int samplingRate, QrsDetector qrsDetector)
    {
        // Downsample to 200 Hz for QRS detection algorithm
        const int QRS_DETECTION_SAMPLING_RATE = 200;

        int finalSamplingRate = samplingRate;
        IPropagatorBlock<ushort, double> source = new TransformBlock<ushort, double>(x => x);

        if (samplingRate > QRS_DETECTION_SAMPLING_RATE)
        {
            finalSamplingRate = QRS_DETECTION_SAMPLING_RATE;
            source = Downsample(samplingRate, QRS_DETECTION_SAMPLING_RATE);
        }

        // Output of the qrs detector - calculates the heart rate from the rr samples interval
        var calculateHeartRate = new TransformBlock<int, int>(rrSamples =>
        {
            return ((int)(60 / ((double)rrSamples / finalSamplingRate)));
        });

        // Invoke the provided callback
        //var processHeartRate = new ActionBlock<int>(heartRateCallback);

        //calculateHeartRate.LinkTo(processHeartRate, new DataflowLinkOptions() { PropagateCompletion = true });

        // Init the QRS detector
        qrsDetector.Init(source, calculateHeartRate, finalSamplingRate);

        return DataflowBlock.Encapsulate(source, calculateHeartRate);
    }

    public static IPropagatorBlock<ushort, double> Downsample(int inputSamplingRate, int outputSamplingRate)
    {
        if (outputSamplingRate > inputSamplingRate)
        {
            throw new ArgumentException("Output sampling rate must be smaller than or equal to input sampling rate for downsampling.");
        }

        // Batch the samples to match output sampling rate (NOTE: only accurate to whole numbers)
        var batchSamples = new BatchBlock<ushort>(inputSamplingRate / outputSamplingRate);

        // Average the batched samples
        var downsample = new TransformBlock<ushort[], double>(samples =>
        {
            return samples.Aggregate(0.0, (sum, sample) => sum + sample) / samples.Length;
        });

        batchSamples.LinkTo(downsample);
        batchSamples.Completion.ContinueWith(completion => downsample.Complete());

        return DataflowBlock.Encapsulate(batchSamples, downsample);
    }

    /// <summary>
    /// Upsamples a stream of ushort audio samples to a higher sampling rate.
    /// Each input sample is repeated to achieve the upsampling effect.
    /// </summary>
    /// <param name="inputSamplingRate">The original sampling rate of the input samples.</param>
    /// <param name="outputSamplingRate">The desired output sampling rate.</param>
    /// <returns>An IPropagatorBlock that transforms ushort samples to ushort samples.</returns>
    public static IPropagatorBlock<ushort, ushort> Upsample(int inputSamplingRate, int outputSamplingRate)
    {
        if (outputSamplingRate < inputSamplingRate)
        {
            throw new ArgumentException("Output sampling rate must be greater than or equal to input sampling rate for upsampling.");
        }

        // Calculate how many times each input sample needs to be repeated
        // We use a TransformManyBlock because one input produces multiple outputs
        var upsamplingFactor = outputSamplingRate / inputSamplingRate;

        var upsample = new TransformManyBlock<ushort, ushort>(sample =>
        {
            // Repeat the single input sample 'upsamplingFactor' times
            return Enumerable.Repeat(sample, upsamplingFactor);
        });

        return upsample;
    }
}
