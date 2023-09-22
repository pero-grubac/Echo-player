namespace EchoPlayer
{
    class Echo
    {

        public int EchoLength { get; private set; }
        public float EchoFactor { get; private set; }
        private Queue<float> samples;
        public Echo(int echoLength, float echoFactor)
        {
            this.EchoLength = echoLength;
            this.EchoFactor = echoFactor;

            samples = new Queue<float>();
            for (int i = 0; i < echoLength; i++)
            {
                samples.Enqueue(0f);
            }
        }
        public float ApplyEcho(float sample)
        {
            samples.Enqueue(sample);
            return Math.Min(1, Math.Max(-1, sample + EchoFactor * samples.Dequeue()));
        }
    }
}
