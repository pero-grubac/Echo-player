using NAudio.Wave;

namespace EchoPlayer
{
    class EffectStream : NAudio.Wave.WaveStream
    {
        public WaveStream source;
        public List<Echo> Echo { get; private set; }
        private int channels = 0;
        public EffectStream(WaveStream source)
        {
            this.source = source;
            Echo = new List<Echo>();
        }
        public override WaveFormat WaveFormat
        {
            get
            {
                return source.WaveFormat;
            }
        }

        public override long Length
        {
            get { return source.Length; }
        }

        public override long Position
        {
            get
            {
                return source.Position;
            }
            set
            {
                source.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            // 4 bytes per sample, read koliko smo bita procitali
            for (int i = 0; i < read / 4; i++)
            {
                float sample = BitConverter.ToSingle(buffer, i * 4);

                if (Echo.Count == WaveFormat.Channels)
                {
                    sample = Echo[channels].ApplyEcho(sample);
                    channels = (channels + 1) % WaveFormat.Channels;
                }

                byte[] bytes = BitConverter.GetBytes(sample);
                buffer[i * 4 + 0] = bytes[0];
                buffer[i * 4 + 1] = bytes[1];
                buffer[i * 4 + 2] = bytes[2];
                buffer[i * 4 + 3] = bytes[3];

            }
            return read;
        }
    }
}
