using Microsoft.ML;

namespace WorkloadProductivity.MlInterfaces
{
    public sealed class MlModelHost
    {
        public MLContext Ml { get; }
        public ITransformer Model { get; }

        public MlModelHost(MLContext ml, ITransformer model)
        {
            Ml = ml;
            Model = model;
        }
    }

}
