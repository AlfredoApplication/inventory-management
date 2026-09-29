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
        private readonly IAuthorizationService _authorization;

        public WorkerService(IAuthorizationService authorization = null)
        {
            _authorization = authorization ?? AppServices.Authorization;
        }
        public void Save(Worker worker)
        {
            _authorization.RequireEdit();
            if (worker == null) throw new ArgumentNullException(nameof(worker));
            AppData.SaveAndRefreshWorker(worker);
        }

        public void Delete(Worker worker)
        {
            _authorization.RequireDelete();
            if (worker == null) return;
            AppData.DeleteAndRefreshWorker(worker);
        }

        public int SetActiveState(IEnumerable<int> workerIds, bool isActive)
        {
            _authorization.RequireEdit();
            return AppData.BulkSetWorkersActiveState(workerIds, isActive);
        }

        public async Task<int> SynchronizeFromHrAsync()
        {
            _authorization.RequireEdit();
            int affectedRows = await DataAccess.SynchronizeWorkersFromRemoteAsync();
            AppData.RefreshWorkersFromDatabase();
            return affectedRows;
        }
    }
}
