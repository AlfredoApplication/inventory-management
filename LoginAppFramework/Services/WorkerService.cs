using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LoginAppFramework
{
    public interface IWorkerService
    {
        void Save(Worker worker);
        void Delete(Worker worker);
        int SetActiveState(IEnumerable<int> workerIds, bool isActive);
        Task<int> SynchronizeFromHrAsync();
    }

    public sealed class WorkerService : IWorkerService
    {
        public void Save(Worker worker)
        {
            if (worker == null) throw new ArgumentNullException(nameof(worker));
            AppData.SaveAndRefreshWorker(worker);
        }

        public void Delete(Worker worker)
        {
            if (worker == null) return;
            AppData.DeleteAndRefreshWorker(worker);
        }

        public int SetActiveState(IEnumerable<int> workerIds, bool isActive)
            => AppData.BulkSetWorkersActiveState(workerIds, isActive);

        public async Task<int> SynchronizeFromHrAsync()
        {
            int affectedRows = await DataAccess.SynchronizeWorkersFromRemoteAsync();
            AppData.RefreshWorkersFromDatabase();
            return affectedRows;
        }
    }
}
