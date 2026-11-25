namespace MergerLogic.Batching
{
    // A partly covered tile is filled from the lower zoom already in the target, so with parallel batches a batch
    // must not start until every batch read before it with a lower zoom is written. Same-zoom batches still overlap.
    public class ZoomOrderGate
    {
        private readonly object _lock = new object();
        private readonly List<(long Ticket, int MinZoom, int MaxZoom)> _open = new();
        private long _nextTicket;

        // Call in the order the batches were read.
        public long Register(int minZoom, int maxZoom)
        {
            lock (this._lock)
            {
                long ticket = this._nextTicket++;
                this._open.Add((ticket, minZoom, maxZoom));
                return ticket;
            }
        }

        public void WaitForLowerZooms(long ticket)
        {
            lock (this._lock)
            {
                int maxZoom = this._open.Single(batch => batch.Ticket == ticket).MaxZoom;
                while (this._open.Any(batch => batch.Ticket < ticket && batch.MinZoom < maxZoom))
                {
                    Monitor.Wait(this._lock);
                }
            }
        }

        public void Complete(long ticket)
        {
            lock (this._lock)
            {
                this._open.RemoveAll(batch => batch.Ticket == ticket);
                Monitor.PulseAll(this._lock);
            }
        }
    }
}
