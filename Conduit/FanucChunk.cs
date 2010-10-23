using System;

namespace JurmanMetrics
{
    public class FanucChunk
    {
        private string data;
        private FanucChunkState state;
        private readonly int id;
        private object persist;

        public FanucChunk(int id)
        {
            this.id = id;
            this.state = FanucChunkState.UnProcessed;
            this.persist = 0x00;
        }

        public FanucChunk(FanucChunk fc)
        {
            this.id = fc.id + 1;
            this.persist = fc.PersistentExtra;
            this.state = FanucChunkState.UnProcessed;
        }

        public string Data
        {
            get { return data; }
            set { data = value; }
        }

        public FanucChunkState State
        {
            get { return state; }
            set { state = value; }
        }

        public int Id
        {
            get { return id; }
        }

        public object PersistentExtra
        {
            get { return persist; }
            set { persist = value; }
        }
    }
}