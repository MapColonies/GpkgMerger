namespace MergerLogic.Clients
{
    public interface IHeartbeatClient
    {
        public void Start(string taskId);
        public void Stop();
    }
}
